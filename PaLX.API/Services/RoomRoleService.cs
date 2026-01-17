using Npgsql;
using PaLX.API.DTOs;

namespace PaLX.API.Services
{
    // Cache keys for RoomRoleService
    public static class RoomRoleCacheKeys
    {
        public const string RoleDefinitions = "roomroles:definitions";
        public const string Permissions = "roomroles:permissions";
        public static string RoleById(int id) => $"roomroles:role:{id}";
    }

    public interface IRoomRoleService
    {
        // Role Definitions CRUD
        Task<List<RoomRoleDefinitionDto>> GetRoleDefinitionsAsync();
        Task<RoomRoleDefinitionDto?> GetRoleDefinitionByIdAsync(int id);
        Task<RoomRoleOperationResult> CreateRoleDefinitionAsync(CreateRoomRoleDto dto, int adminId);
        Task<RoomRoleOperationResult> UpdateRoleDefinitionAsync(int id, UpdateRoomRoleDto dto, int adminId);
        Task<RoomRoleOperationResult> DeleteRoleDefinitionAsync(int id, int adminId);

        // Permissions
        Task<List<PermissionListDto>> GetPermissionsGroupedAsync();
        Task<List<RoomPermissionDto>> GetAllPermissionsAsync();
    }

    public class RoomRoleService : IRoomRoleService
    {
        private readonly string _connectionString;
        private readonly ICacheService _cache;
        private readonly ILogger<RoomRoleService> _logger;

        public RoomRoleService(IConfiguration configuration, ICacheService cacheService, ILogger<RoomRoleService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found");
            _cache = cacheService;
            _logger = logger;
        }

        // ============================================
        // Role Definitions - READ
        // ============================================

        public async Task<List<RoomRoleDefinitionDto>> GetRoleDefinitionsAsync()
        {
            return await _cache.GetOrSetAsync(
                RoomRoleCacheKeys.RoleDefinitions,
                async () => await FetchRoleDefinitionsFromDatabaseAsync(),
                CacheOptions.MediumTerm // 15 minutes
            );
        }

        private async Task<List<RoomRoleDefinitionDto>> FetchRoleDefinitionsFromDatabaseAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Vérifier et créer les tables si elles n'existent pas
            await EnsureTablesExistAsync(conn);

            var roles = new List<RoomRoleDefinitionDto>();

            // Récupérer les rôles
            var roleSql = @"
                SELECT ""Id"", ""RoleLevel"", ""RoleName"", ""DisplayName"", ""Description"", 
                       ""Icon"", ""Color"", ""IsSystem"", ""IsActive"", ""CreatedAt"", ""UpdatedAt""
                FROM ""RoomRoleDefinitions""
                WHERE ""IsActive"" = TRUE
                ORDER BY ""RoleLevel"" ASC";

            using (var cmd = new NpgsqlCommand(roleSql, conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    roles.Add(new RoomRoleDefinitionDto
                    {
                        Id = reader.GetInt32(0),
                        RoleLevel = reader.GetInt32(1),
                        RoleName = reader.GetString(2),
                        DisplayName = reader.GetString(3),
                        Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Icon = reader.GetString(5),
                        Color = reader.GetString(6),
                        IsSystem = reader.GetBoolean(7),
                        IsActive = reader.GetBoolean(8),
                        CreatedAt = reader.GetDateTime(9),
                        UpdatedAt = reader.GetDateTime(10),
                        Permissions = new List<RoomPermissionDto>()
                    });
                }
            }

            // Récupérer les permissions pour chaque rôle
            foreach (var role in roles)
            {
                var permSql = @"
                    SELECT p.""Id"", p.""PermissionKey"", p.""DisplayName"", p.""Description"", p.""Category"", p.""IsActive""
                    FROM ""RoomPermissions"" p
                    INNER JOIN ""RoomRolePermissions"" rp ON rp.""PermissionId"" = p.""Id""
                    WHERE rp.""RoleId"" = @RoleId AND p.""IsActive"" = TRUE
                    ORDER BY p.""Category"", p.""DisplayName""";

                using var permCmd = new NpgsqlCommand(permSql, conn);
                permCmd.Parameters.AddWithValue("@RoleId", role.Id);

                using var permReader = await permCmd.ExecuteReaderAsync();
                while (await permReader.ReadAsync())
                {
                    role.Permissions.Add(new RoomPermissionDto
                    {
                        Id = permReader.GetInt32(0),
                        PermissionKey = permReader.GetString(1),
                        DisplayName = permReader.GetString(2),
                        Description = permReader.IsDBNull(3) ? null : permReader.GetString(3),
                        Category = permReader.GetString(4),
                        IsActive = permReader.GetBoolean(5),
                        IsEnabled = true // Si on est ici, la permission est attribuée
                    });
                }
            }

            return roles;
        }

        public async Task<RoomRoleDefinitionDto?> GetRoleDefinitionByIdAsync(int id)
        {
            var roles = await GetRoleDefinitionsAsync();
            return roles.FirstOrDefault(r => r.Id == id);
        }

        // ============================================
        // Role Definitions - CREATE
        // ============================================

        public async Task<RoomRoleOperationResult> CreateRoleDefinitionAsync(CreateRoomRoleDto dto, int adminId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = await conn.BeginTransactionAsync();

                // Vérifier que le RoleLevel n'est pas déjà utilisé
                var checkSql = @"SELECT COUNT(*) FROM ""RoomRoleDefinitions"" WHERE ""RoleLevel"" = @Level OR ""RoleName"" = @Name";
                using (var checkCmd = new NpgsqlCommand(checkSql, conn, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@Level", dto.RoleLevel);
                    checkCmd.Parameters.AddWithValue("@Name", dto.RoleName);
                    var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                    if (exists)
                    {
                        return new RoomRoleOperationResult
                        {
                            Success = false,
                            Message = "Un rôle avec ce niveau ou ce nom existe déjà"
                        };
                    }
                }

                // Créer le rôle
                var insertSql = @"
                    INSERT INTO ""RoomRoleDefinitions"" 
                        (""RoleLevel"", ""RoleName"", ""DisplayName"", ""Description"", ""Icon"", ""Color"", ""IsSystem"", ""IsActive"", ""CreatedBy"")
                    VALUES 
                        (@Level, @Name, @DisplayName, @Description, @Icon, @Color, FALSE, TRUE, @CreatedBy)
                    RETURNING ""Id""";

                int roleId;
                using (var insertCmd = new NpgsqlCommand(insertSql, conn, transaction))
                {
                    insertCmd.Parameters.AddWithValue("@Level", dto.RoleLevel);
                    insertCmd.Parameters.AddWithValue("@Name", dto.RoleName);
                    insertCmd.Parameters.AddWithValue("@DisplayName", dto.DisplayName);
                    insertCmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Icon", dto.Icon);
                    insertCmd.Parameters.AddWithValue("@Color", dto.Color);
                    insertCmd.Parameters.AddWithValue("@CreatedBy", adminId);

                    roleId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
                }

                // Ajouter les permissions
                if (dto.PermissionIds.Any())
                {
                    var permSql = @"INSERT INTO ""RoomRolePermissions"" (""RoleId"", ""PermissionId"") VALUES (@RoleId, @PermId)";
                    foreach (var permId in dto.PermissionIds)
                    {
                        using var permCmd = new NpgsqlCommand(permSql, conn, transaction);
                        permCmd.Parameters.AddWithValue("@RoleId", roleId);
                        permCmd.Parameters.AddWithValue("@PermId", permId);
                        await permCmd.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();

                // Invalider le cache
                await _cache.RemoveAsync(RoomRoleCacheKeys.RoleDefinitions);

                _logger.LogInformation("Admin {AdminId} created room role {RoleId}: {RoleName}", adminId, roleId, dto.RoleName);

                var createdRole = await GetRoleDefinitionByIdAsync(roleId);
                return new RoomRoleOperationResult
                {
                    Success = true,
                    Message = "Rôle créé avec succès",
                    Role = createdRole
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating room role");
                return new RoomRoleOperationResult
                {
                    Success = false,
                    Message = $"Erreur lors de la création: {ex.Message}"
                };
            }
        }

        // ============================================
        // Role Definitions - UPDATE
        // ============================================

        public async Task<RoomRoleOperationResult> UpdateRoleDefinitionAsync(int id, UpdateRoomRoleDto dto, int adminId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var transaction = await conn.BeginTransactionAsync();

                // Vérifier que le rôle existe et n'est pas système (pour certaines modifications)
                var checkSql = @"SELECT ""IsSystem"", ""RoleName"" FROM ""RoomRoleDefinitions"" WHERE ""Id"" = @Id";
                bool isSystem;
                string roleName;
                using (var checkCmd = new NpgsqlCommand(checkSql, conn, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@Id", id);
                    using var reader = await checkCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        return new RoomRoleOperationResult
                        {
                            Success = false,
                            Message = "Rôle non trouvé"
                        };
                    }
                    isSystem = reader.GetBoolean(0);
                    roleName = reader.GetString(1);
                }

                // Mise à jour du rôle (certains champs protégés pour rôles système)
                var updateSql = @"
                    UPDATE ""RoomRoleDefinitions"" 
                    SET ""DisplayName"" = @DisplayName, 
                        ""Description"" = @Description, 
                        ""Icon"" = @Icon, 
                        ""Color"" = @Color,
                        ""IsActive"" = @IsActive,
                        ""UpdatedAt"" = NOW()
                    WHERE ""Id"" = @Id";

                using (var updateCmd = new NpgsqlCommand(updateSql, conn, transaction))
                {
                    updateCmd.Parameters.AddWithValue("@Id", id);
                    updateCmd.Parameters.AddWithValue("@DisplayName", dto.DisplayName);
                    updateCmd.Parameters.AddWithValue("@Description", (object?)dto.Description ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@Icon", dto.Icon);
                    updateCmd.Parameters.AddWithValue("@Color", dto.Color);
                    updateCmd.Parameters.AddWithValue("@IsActive", dto.IsActive);
                    await updateCmd.ExecuteNonQueryAsync();
                }

                // Mise à jour des permissions (même pour les rôles système, on peut modifier les permissions)
                // Supprimer les anciennes permissions
                var deleteSql = @"DELETE FROM ""RoomRolePermissions"" WHERE ""RoleId"" = @RoleId";
                using (var deleteCmd = new NpgsqlCommand(deleteSql, conn, transaction))
                {
                    deleteCmd.Parameters.AddWithValue("@RoleId", id);
                    await deleteCmd.ExecuteNonQueryAsync();
                }

                // Ajouter les nouvelles permissions
                if (dto.PermissionIds.Any())
                {
                    var permSql = @"INSERT INTO ""RoomRolePermissions"" (""RoleId"", ""PermissionId"") VALUES (@RoleId, @PermId)";
                    foreach (var permId in dto.PermissionIds)
                    {
                        using var permCmd = new NpgsqlCommand(permSql, conn, transaction);
                        permCmd.Parameters.AddWithValue("@RoleId", id);
                        permCmd.Parameters.AddWithValue("@PermId", permId);
                        await permCmd.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();

                // Invalider le cache
                await _cache.RemoveAsync(RoomRoleCacheKeys.RoleDefinitions);

                _logger.LogInformation("Admin {AdminId} updated room role {RoleId}: {RoleName}", adminId, id, roleName);

                var updatedRole = await GetRoleDefinitionByIdAsync(id);
                return new RoomRoleOperationResult
                {
                    Success = true,
                    Message = "Rôle mis à jour avec succès",
                    Role = updatedRole
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating room role {RoleId}", id);
                return new RoomRoleOperationResult
                {
                    Success = false,
                    Message = $"Erreur lors de la mise à jour: {ex.Message}"
                };
            }
        }

        // ============================================
        // Role Definitions - DELETE
        // ============================================

        public async Task<RoomRoleOperationResult> DeleteRoleDefinitionAsync(int id, int adminId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // Vérifier que le rôle existe et n'est pas système
                var checkSql = @"SELECT ""IsSystem"", ""RoleName"" FROM ""RoomRoleDefinitions"" WHERE ""Id"" = @Id";
                using (var checkCmd = new NpgsqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@Id", id);
                    using var reader = await checkCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        return new RoomRoleOperationResult
                        {
                            Success = false,
                            Message = "Rôle non trouvé"
                        };
                    }
                    var isSystem = reader.GetBoolean(0);
                    var roleName = reader.GetString(1);

                    if (isSystem)
                    {
                        return new RoomRoleOperationResult
                        {
                            Success = false,
                            Message = "Impossible de supprimer un rôle système"
                        };
                    }
                }

                // Supprimer le rôle (CASCADE supprimera les permissions liées)
                var deleteSql = @"DELETE FROM ""RoomRoleDefinitions"" WHERE ""Id"" = @Id AND ""IsSystem"" = FALSE";
                using (var deleteCmd = new NpgsqlCommand(deleteSql, conn))
                {
                    deleteCmd.Parameters.AddWithValue("@Id", id);
                    var rowsAffected = await deleteCmd.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        return new RoomRoleOperationResult
                        {
                            Success = false,
                            Message = "Impossible de supprimer ce rôle"
                        };
                    }
                }

                // Invalider le cache
                await _cache.RemoveAsync(RoomRoleCacheKeys.RoleDefinitions);

                _logger.LogInformation("Admin {AdminId} deleted room role {RoleId}", adminId, id);

                return new RoomRoleOperationResult
                {
                    Success = true,
                    Message = "Rôle supprimé avec succès"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting room role {RoleId}", id);
                return new RoomRoleOperationResult
                {
                    Success = false,
                    Message = $"Erreur lors de la suppression: {ex.Message}"
                };
            }
        }

        // ============================================
        // Permissions
        // ============================================

        public async Task<List<RoomPermissionDto>> GetAllPermissionsAsync()
        {
            return await _cache.GetOrSetAsync(
                RoomRoleCacheKeys.Permissions,
                async () => await FetchPermissionsFromDatabaseAsync(),
                CacheOptions.LongTerm // 1 heure - les permissions changent rarement
            );
        }

        private async Task<List<RoomPermissionDto>> FetchPermissionsFromDatabaseAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Vérifier et créer les tables si elles n'existent pas
            await EnsureTablesExistAsync(conn);

            var permissions = new List<RoomPermissionDto>();
            var sql = @"
                SELECT ""Id"", ""PermissionKey"", ""DisplayName"", ""Description"", ""Category"", ""IsActive""
                FROM ""RoomPermissions""
                WHERE ""IsActive"" = TRUE
                ORDER BY ""Category"", ""DisplayName""";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                permissions.Add(new RoomPermissionDto
                {
                    Id = reader.GetInt32(0),
                    PermissionKey = reader.GetString(1),
                    DisplayName = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Category = reader.GetString(4),
                    IsActive = reader.GetBoolean(5),
                    IsEnabled = false // Par défaut non activé, sera mis à jour selon le contexte
                });
            }

            return permissions;
        }

        public async Task<List<PermissionListDto>> GetPermissionsGroupedAsync()
        {
            var allPermissions = await GetAllPermissionsAsync();

            var categoryNames = new Dictionary<string, string>
            {
                { "general", "Général" },
                { "roles", "Gestion des Rôles" },
                { "moderation", "Modération" },
                { "members", "Membres" },
                { "media", "Média" },
                { "base", "Permissions de Base" }
            };

            return allPermissions
                .GroupBy(p => p.Category)
                .Select(g => new PermissionListDto
                {
                    Category = g.Key,
                    CategoryDisplayName = categoryNames.TryGetValue(g.Key, out var name) ? name : g.Key,
                    Permissions = g.ToList()
                })
                .OrderBy(g => GetCategoryOrder(g.Category))
                .ToList();
        }

        private int GetCategoryOrder(string category)
        {
            return category switch
            {
                "general" => 1,
                "roles" => 2,
                "moderation" => 3,
                "members" => 4,
                "media" => 5,
                "base" => 6,
                _ => 99
            };
        }

        // ============================================
        // Database Initialization
        // ============================================

        private async Task EnsureTablesExistAsync(NpgsqlConnection conn)
        {
            // Vérifier si la table existe
            var checkSql = @"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'RoomRoleDefinitions')";
            using var checkCmd = new NpgsqlCommand(checkSql, conn);
            var exists = (bool)(await checkCmd.ExecuteScalarAsync() ?? false);

            if (!exists)
            {
                _logger.LogInformation("Creating RoomRoleDefinitions tables...");
                await CreateTablesAsync(conn);
            }
        }

        private async Task CreateTablesAsync(NpgsqlConnection conn)
        {
            var createTablesSql = @"
                -- Table des définitions de rôles
                CREATE TABLE IF NOT EXISTS ""RoomRoleDefinitions"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""RoleLevel"" INTEGER NOT NULL UNIQUE,
                    ""RoleName"" VARCHAR(50) NOT NULL UNIQUE,
                    ""DisplayName"" VARCHAR(100) NOT NULL,
                    ""Description"" TEXT,
                    ""Icon"" VARCHAR(50) NOT NULL DEFAULT 'user',
                    ""Color"" VARCHAR(20) NOT NULL DEFAULT '#95A5A6',
                    ""IsSystem"" BOOLEAN NOT NULL DEFAULT FALSE,
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                    ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                    ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                    ""CreatedBy"" INTEGER NULL
                );

                -- Table des permissions disponibles
                CREATE TABLE IF NOT EXISTS ""RoomPermissions"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""PermissionKey"" VARCHAR(100) NOT NULL UNIQUE,
                    ""DisplayName"" VARCHAR(150) NOT NULL,
                    ""Description"" TEXT,
                    ""Category"" VARCHAR(50) NOT NULL DEFAULT 'general',
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE
                );

                -- Table de liaison Rôles <-> Permissions
                CREATE TABLE IF NOT EXISTS ""RoomRolePermissions"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""RoleId"" INTEGER NOT NULL REFERENCES ""RoomRoleDefinitions""(""Id"") ON DELETE CASCADE,
                    ""PermissionId"" INTEGER NOT NULL REFERENCES ""RoomPermissions""(""Id"") ON DELETE CASCADE,
                    ""GrantedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                    CONSTRAINT ""UQ_RolePermissions"" UNIQUE (""RoleId"", ""PermissionId"")
                );

                -- Index
                CREATE INDEX IF NOT EXISTS ""IX_RoomRoleDefinitions_Level"" ON ""RoomRoleDefinitions""(""RoleLevel"");
                CREATE INDEX IF NOT EXISTS ""IX_RoomRolePermissions_Role"" ON ""RoomRolePermissions""(""RoleId"");
            ";

            using var createCmd = new NpgsqlCommand(createTablesSql, conn);
            await createCmd.ExecuteNonQueryAsync();

            // Insérer les permissions par défaut
            await InsertDefaultPermissionsAsync(conn);
            
            // Insérer les rôles par défaut
            await InsertDefaultRolesAsync(conn);
        }

        private async Task InsertDefaultPermissionsAsync(NpgsqlConnection conn)
        {
            var insertSql = @"
                INSERT INTO ""RoomPermissions"" (""PermissionKey"", ""DisplayName"", ""Description"", ""Category"") VALUES
                ('manage_settings', 'Modifier les paramètres du salon', 'Peut modifier le nom, description, et autres paramètres', 'general'),
                ('delete_room', 'Supprimer le salon', 'Peut supprimer définitivement le salon', 'general'),
                ('manage_subscriptions', 'Gérer les abonnements', 'Peut gérer les abonnements du salon', 'general'),
                ('configure_bot', 'Configurer le bot', 'Peut configurer le bot IA du salon', 'general'),
                ('access_studio', 'Accès au studio', 'Peut accéder au studio de diffusion', 'general'),
                ('view_stats', 'Voir les statistiques', 'Peut consulter les statistiques du salon', 'general'),
                ('assign_all_roles', 'Attribuer tous les rôles', 'Peut attribuer tous les rôles du salon', 'roles'),
                ('assign_admin_roles', 'Attribuer les rôles Admin et inférieurs', 'Peut attribuer Admin, PowerUser, Mod, Member', 'roles'),
                ('assign_mod_roles', 'Attribuer les rôles Mod et inférieurs', 'Peut attribuer Mod et Member', 'roles'),
                ('kick_users', 'Kicker des utilisateurs', 'Peut expulser temporairement des utilisateurs', 'moderation'),
                ('ban_users', 'Bannir des utilisateurs', 'Peut bannir définitivement des utilisateurs', 'moderation'),
                ('mute_users', 'Muter des utilisateurs', 'Peut couper le micro des utilisateurs', 'moderation'),
                ('warn_users', 'Avertir des utilisateurs', 'Peut envoyer des avertissements', 'moderation'),
                ('delete_messages', 'Supprimer des messages', 'Peut supprimer les messages du chat', 'moderation'),
                ('report_to_owner', 'Signaler au propriétaire', 'Peut signaler des problèmes au propriétaire', 'moderation'),
                ('invite_members', 'Inviter des membres', 'Peut inviter des utilisateurs dans le salon', 'members'),
                ('view_members', 'Voir la liste des membres', 'Peut voir tous les membres du salon', 'members'),
                ('priority_media', 'Priorité micro/caméra', 'A la priorité pour le micro et la caméra', 'media'),
                ('share_files', 'Partager des fichiers', 'Peut partager des fichiers dans le salon', 'media'),
                ('request_mic', 'Demander le micro', 'Peut demander à activer le micro', 'media'),
                ('request_cam', 'Demander la caméra', 'Peut demander à activer la caméra', 'media'),
                ('send_messages', 'Envoyer des messages', 'Peut envoyer des messages dans le chat', 'base'),
                ('view_chat', 'Voir le chat', 'Peut voir les messages du chat', 'base'),
                ('view_online_members', 'Voir les membres en ligne', 'Peut voir qui est connecté', 'base')
                ON CONFLICT (""PermissionKey"") DO NOTHING;
            ";

            using var cmd = new NpgsqlCommand(insertSql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task InsertDefaultRolesAsync(NpgsqlConnection conn)
        {
            // Insérer les rôles système
            var rolesSql = @"
                INSERT INTO ""RoomRoleDefinitions"" (""RoleLevel"", ""RoleName"", ""DisplayName"", ""Description"", ""Icon"", ""Color"", ""IsSystem"") VALUES
                (1, 'RoomOwner', 'Propriétaire du Salon', 'Contrôle total sur le salon. Peut modifier tous les paramètres, gérer les rôles et supprimer le salon.', 'crown', '#FFD700', TRUE),
                (2, 'RoomSuperAdmin', 'Super Administrateur', 'Pouvoirs étendus de gestion. Peut attribuer les rôles Admin et inférieurs.', 'shield-check', '#E74C3C', TRUE),
                (3, 'RoomAdmin', 'Administrateur', 'Gère la modération et les membres. Peut attribuer les rôles Modérateur et inférieurs.', 'shield', '#9B59B6', TRUE),
                (4, 'PowerUser', 'Utilisateur Avancé', 'Utilisateur de confiance avec des privilèges étendus comme le partage vidéo prioritaire.', 'bolt', '#3498DB', TRUE),
                (5, 'RoomModerator', 'Modérateur', 'Surveille le chat et peut avertir ou muter les utilisateurs problématiques.', 'eye', '#2ECC71', TRUE),
                (6, 'RoomMember', 'Membre', 'Membre standard du salon avec les permissions de base.', 'user', '#95A5A6', TRUE)
                ON CONFLICT (""RoleName"") DO NOTHING;
            ";

            using var rolesCmd = new NpgsqlCommand(rolesSql, conn);
            await rolesCmd.ExecuteNonQueryAsync();

            // Attribuer toutes les permissions au RoomOwner
            var ownerPermsSql = @"
                INSERT INTO ""RoomRolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""RoomRoleDefinitions"" r, ""RoomPermissions"" p
                WHERE r.""RoleName"" = 'RoomOwner'
                ON CONFLICT DO NOTHING;
            ";
            using var ownerCmd = new NpgsqlCommand(ownerPermsSql, conn);
            await ownerCmd.ExecuteNonQueryAsync();

            // SuperAdmin - tout sauf delete_room et assign_all_roles
            var superAdminPermsSql = @"
                INSERT INTO ""RoomRolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""RoomRoleDefinitions"" r, ""RoomPermissions"" p
                WHERE r.""RoleName"" = 'RoomSuperAdmin'
                  AND p.""PermissionKey"" NOT IN ('delete_room', 'assign_all_roles')
                ON CONFLICT DO NOTHING;
            ";
            using var superAdminCmd = new NpgsqlCommand(superAdminPermsSql, conn);
            await superAdminCmd.ExecuteNonQueryAsync();

            // Admin - modération + membres
            var adminPermsSql = @"
                INSERT INTO ""RoomRolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""RoomRoleDefinitions"" r, ""RoomPermissions"" p
                WHERE r.""RoleName"" = 'RoomAdmin'
                  AND p.""PermissionKey"" IN ('assign_mod_roles', 'kick_users', 'ban_users', 'mute_users', 'warn_users', 
                    'delete_messages', 'invite_members', 'view_members', 'view_stats',
                    'send_messages', 'view_chat', 'view_online_members', 'request_mic', 'request_cam')
                ON CONFLICT DO NOTHING;
            ";
            using var adminCmd = new NpgsqlCommand(adminPermsSql, conn);
            await adminCmd.ExecuteNonQueryAsync();

            // PowerUser - privilèges étendus
            var powerUserPermsSql = @"
                INSERT INTO ""RoomRolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""RoomRoleDefinitions"" r, ""RoomPermissions"" p
                WHERE r.""RoleName"" = 'PowerUser'
                  AND p.""PermissionKey"" IN ('priority_media', 'invite_members', 'view_members', 'view_stats', 'share_files',
                    'send_messages', 'view_chat', 'view_online_members', 'request_mic', 'request_cam')
                ON CONFLICT DO NOTHING;
            ";
            using var powerUserCmd = new NpgsqlCommand(powerUserPermsSql, conn);
            await powerUserCmd.ExecuteNonQueryAsync();

            // Moderator - surveillance
            var modPermsSql = @"
                INSERT INTO ""RoomRolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""RoomRoleDefinitions"" r, ""RoomPermissions"" p
                WHERE r.""RoleName"" = 'RoomModerator'
                  AND p.""PermissionKey"" IN ('mute_users', 'warn_users', 'delete_messages', 'report_to_owner', 'view_members',
                    'send_messages', 'view_chat', 'view_online_members', 'request_mic', 'request_cam')
                ON CONFLICT DO NOTHING;
            ";
            using var modCmd = new NpgsqlCommand(modPermsSql, conn);
            await modCmd.ExecuteNonQueryAsync();

            // Member - base
            var memberPermsSql = @"
                INSERT INTO ""RoomRolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""RoomRoleDefinitions"" r, ""RoomPermissions"" p
                WHERE r.""RoleName"" = 'RoomMember'
                  AND p.""PermissionKey"" IN ('send_messages', 'view_chat', 'view_online_members', 'request_mic', 'request_cam')
                ON CONFLICT DO NOTHING;
            ";
            using var memberCmd = new NpgsqlCommand(memberPermsSql, conn);
            await memberCmd.ExecuteNonQueryAsync();

            _logger.LogInformation("Default room roles and permissions created successfully");
        }
    }
}

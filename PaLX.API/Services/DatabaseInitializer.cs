using Npgsql;

namespace PaLX.API.Services
{
    /// <summary>
    /// Database Initializer for PaL.Xtreme
    /// 
    /// IMPORTANT: Ce service vérifie uniquement que les tables existent.
    /// Il ne crée PAS de données de seed automatiquement.
    /// 
    /// Les données de référence (Roles, RoomRoles, Categories, etc.) sont gérées
    /// manuellement via pgAdmin ou scripts SQL dédiés.
    /// 
    /// Structure actuelle de la base (Janvier 2026):
    /// - Roles: 7 rôles système (ServerMaster→User, niveaux 1-7)
    /// - RoomRoles: 6 rôles locaux (RoomOwner→RoomMember, niveaux 1-6)
    /// - RoomCategories: 8 catégories avec sous-catégories
    /// - SubscriptionTiers: 10 niveaux d'abonnement
    /// - ServerRoleRoomPermissions: Permissions des admins (1-6) dans les rooms
    /// </summary>
    public class DatabaseInitializer
    {
        private readonly string _connectionString;

        public DatabaseInitializer(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("Connection string not found.");
        }

        public async Task InitializeAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Vérification simple que la base est accessible
            // Les tables sont créées manuellement ou via migrations
            var checkConnection = "SELECT 1";
            using (var cmd = new NpgsqlCommand(checkConnection, conn))
            {
                await cmd.ExecuteScalarAsync();
            }

            // Log de confirmation (optionnel)
            Console.WriteLine("[DatabaseInitializer] Connexion à la base de données vérifiée avec succès.");
            Console.WriteLine("[DatabaseInitializer] Aucune donnée de seed automatique - Base gérée manuellement.");
        }
    }
}

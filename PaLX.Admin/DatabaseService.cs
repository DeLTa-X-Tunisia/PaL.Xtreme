using System;
using System.Collections.Generic;
using System.Text;
using Npgsql;
using BCrypt.Net;

namespace PaLX.Admin
{
    public class DatabaseService
    {
        private const string Host = "localhost";
        private const string Username = "postgres";
        private const string Password = "2012704";
        private const string DatabaseName = "PaL.X";

        private string GetConnectionString(string? dbName = null)
        {
            return $"Host={Host};Username={Username};Password={Password};Database={dbName ?? "postgres"}";
        }

        public void Initialize()
        {
            try
            {
                // 1. Ensure Database Exists
                using (var conn = new NpgsqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    var cmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{DatabaseName}'", conn);
                    var exists = cmd.ExecuteScalar() != null;

                    if (!exists)
                    {
                        var createDbCmd = new NpgsqlCommand($"CREATE DATABASE \"{DatabaseName}\"", conn);
                        createDbCmd.ExecuteNonQuery();
                    }
                }

                // 2. Update Schema
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();

                    // Create Roles Table
                    var createRolesSql = @"
                        CREATE TABLE IF NOT EXISTS ""Roles"" (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""RoleLevel"" INT NOT NULL,
                            ""RoleName"" TEXT NOT NULL
                        );";
                    using (var cmd = new NpgsqlCommand(createRolesSql, conn)) cmd.ExecuteNonQuery();

                    // Seed Roles
                    var checkRolesCmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"Roles\"", conn);
                    long roleCount = (long)(checkRolesCmd.ExecuteScalar() ?? 0L);
                    if (roleCount == 0)
                    {
                        var seedRolesSql = @"
                            INSERT INTO ""Roles"" (""RoleLevel"", ""RoleName"") VALUES
                            (1, 'ServerMaster'),
                            (2, 'ServerEditor'),
                            (3, 'ServerSuperAdmin'),
                            (4, 'ServerAdmin'),
                            (5, 'ServerModerator'),
                            (6, 'ServerHelp'),
                            (7, 'User');
                        ";
                        using (var cmd = new NpgsqlCommand(seedRolesSql, conn)) cmd.ExecuteNonQuery();
                    }

                    // Create Users Table (Ensure it exists first)
                    var createUsersSql = @"
                        CREATE TABLE IF NOT EXISTS ""Users"" (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""Username"" TEXT UNIQUE NOT NULL,
                            ""PasswordHash"" TEXT NOT NULL,
                            ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW()
                        );";
                    using (var cmd = new NpgsqlCommand(createUsersSql, conn)) cmd.ExecuteNonQuery();

                    // Drop old Role column if exists
                    var dropRoleColSql = @"
                        DO $$ 
                        BEGIN 
                            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'Role') THEN
                                ALTER TABLE ""Users"" DROP COLUMN ""Role"";
                            END IF;
                        END $$;";
                    using (var cmd = new NpgsqlCommand(dropRoleColSql, conn)) cmd.ExecuteNonQuery();

                    // Create UserRoles Table
                    var createUserRolesSql = @"
                        CREATE TABLE IF NOT EXISTS ""UserRoles"" (
                            ""UserId"" INT NOT NULL,
                            ""RoleId"" INT NOT NULL,
                            PRIMARY KEY (""UserId"", ""RoleId""),
                            FOREIGN KEY (""UserId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
                            FOREIGN KEY (""RoleId"") REFERENCES ""Roles""(""Id"") ON DELETE CASCADE
                        );";
                    using (var cmd = new NpgsqlCommand(createUserRolesSql, conn)) cmd.ExecuteNonQuery();

                    // Create UserProfiles Table
                    var createUserProfilesSql = @"
                        CREATE TABLE IF NOT EXISTS ""UserProfiles"" (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""UserId"" INT UNIQUE NOT NULL,
                            ""FirstName"" TEXT,
                            ""LastName"" TEXT,
                            ""Email"" TEXT,
                            ""Gender"" TEXT,
                            ""Country"" TEXT,
                            ""PhoneNumber"" TEXT,
                            ""AvatarPath"" TEXT,
                            ""IsComplete"" BOOLEAN DEFAULT FALSE,
                            FOREIGN KEY (""UserId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE
                        );";
                    using (var cmd = new NpgsqlCommand(createUserProfilesSql, conn)) cmd.ExecuteNonQuery();

                    SeedUsers(conn);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erreur d'initialisation de la base de données : {ex.Message}", "Erreur BDD", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void SeedUsers(NpgsqlConnection conn)
        {
            var usersToSeed = new List<(string Username, int RoleLevel)>
            {
                ("admin1", 1), ("admin2", 2), ("admin3", 3),
                ("admin4", 4), ("admin5", 5), ("admin6", 6),
                ("user1", 7)
            };

            string password = "12345678";
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            foreach (var user in usersToSeed)
            {
                // Check if user exists
                using (var checkCmd = new NpgsqlCommand("SELECT \"Id\" FROM \"Users\" WHERE \"Username\" = @u", conn))
                {
                    checkCmd.Parameters.AddWithValue("u", user.Username);
                    var userIdObj = checkCmd.ExecuteScalar();

                    int userId;
                    if (userIdObj == null)
                    {
                        // Create User
                        using (var insertCmd = new NpgsqlCommand("INSERT INTO \"Users\" (\"Username\", \"PasswordHash\", \"CreatedAt\") VALUES (@u, @p, @c) RETURNING \"Id\"", conn))
                        {
                            insertCmd.Parameters.AddWithValue("u", user.Username);
                            insertCmd.Parameters.AddWithValue("p", passwordHash);
                            insertCmd.Parameters.AddWithValue("c", DateTime.Now);
                            userId = (int)(insertCmd.ExecuteScalar() ?? 0);
                        }
                    }
                    else
                    {
                        userId = (int)userIdObj;
                        // Update password to ensure BCrypt hash is used (migration from SHA256)
                        using (var updateCmd = new NpgsqlCommand("UPDATE \"Users\" SET \"PasswordHash\" = @p WHERE \"Id\" = @id", conn))
                        {
                            updateCmd.Parameters.AddWithValue("p", passwordHash);
                            updateCmd.Parameters.AddWithValue("id", userId);
                            updateCmd.ExecuteNonQuery();
                        }
                    }

                    // Assign Role
                    // Find RoleId for RoleLevel
                    using (var roleCmd = new NpgsqlCommand("SELECT \"Id\" FROM \"Roles\" WHERE \"RoleLevel\" = @l", conn))
                    {
                        roleCmd.Parameters.AddWithValue("l", user.RoleLevel);
                        var roleId = (int)(roleCmd.ExecuteScalar() ?? 0);

                        // Check if assignment exists
                        using (var checkRoleAssign = new NpgsqlCommand("SELECT 1 FROM \"UserRoles\" WHERE \"UserId\" = @u AND \"RoleId\" = @r", conn))
                        {
                            checkRoleAssign.Parameters.AddWithValue("u", userId);
                            checkRoleAssign.Parameters.AddWithValue("r", roleId);
                            if (checkRoleAssign.ExecuteScalar() == null)
                            {
                                using (var assignCmd = new NpgsqlCommand("INSERT INTO \"UserRoles\" (\"UserId\", \"RoleId\") VALUES (@u, @r)", conn))
                                {
                                    assignCmd.Parameters.AddWithValue("u", userId);
                                    assignCmd.Parameters.AddWithValue("r", roleId);
                                    assignCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
        }

        public bool RegisterUser(string username, string password, int roleLevel = 7)
        {
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
                    
                    // 1. Insert User
                    var sqlUser = "INSERT INTO \"Users\" (\"Username\", \"PasswordHash\", \"CreatedAt\") VALUES (@u, @p, @c) RETURNING \"Id\"";
                    int userId;
                    using (var cmd = new NpgsqlCommand(sqlUser, conn))
                    {
                        cmd.Parameters.AddWithValue("u", username);
                        cmd.Parameters.AddWithValue("p", passwordHash);
                        cmd.Parameters.AddWithValue("c", DateTime.Now);
                        userId = (int)(cmd.ExecuteScalar() ?? 0);
                    }

                    // 2. Get Role ID
                    var sqlRole = "SELECT \"Id\" FROM \"Roles\" WHERE \"RoleLevel\" = @l";
                    int roleId;
                    using (var cmd = new NpgsqlCommand(sqlRole, conn))
                    {
                        cmd.Parameters.AddWithValue("l", roleLevel);
                        var res = cmd.ExecuteScalar();
                        if (res == null) throw new Exception("Role not found");
                        roleId = (int)res;
                    }

                    // 3. Assign Role
                    var sqlAssign = "INSERT INTO \"UserRoles\" (\"UserId\", \"RoleId\") VALUES (@u, @r)";
                    using (var cmd = new NpgsqlCommand(sqlAssign, conn))
                    {
                        cmd.Parameters.AddWithValue("u", userId);
                        cmd.Parameters.AddWithValue("r", roleId);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                System.Windows.MessageBox.Show("Ce nom d'utilisateur est déjà pris.", "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erreur lors de l'inscription : {ex.Message}", "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        public (bool isValid, int roleLevel, string? roleName) ValidateUser(string username, string password)
        {
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    var sql = @"
                        SELECT u.""PasswordHash"", r.""RoleLevel"", r.""RoleName""
                        FROM ""Users"" u
                        JOIN ""UserRoles"" ur ON u.""Id"" = ur.""UserId""
                        JOIN ""Roles"" r ON ur.""RoleId"" = r.""Id""
                        WHERE u.""Username"" = @u";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("u", username);
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var storedHash = reader.GetString(0);
                                var roleLevel = reader.GetInt32(1);
                                var roleName = reader.GetString(2);
                                
                                if (BCrypt.Net.BCrypt.Verify(password, storedHash))
                                {
                                    return (true, roleLevel, roleName);
                                }
                            }
                        }
                    }
                }
                return (false, 0, null);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erreur de connexion : {ex.Message}", "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return (false, 0, null);
            }
        }

        public bool IsProfileComplete(string username)
        {
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    var sql = @"
                        SELECT p.""IsComplete""
                        FROM ""UserProfiles"" p
                        JOIN ""Users"" u ON p.""UserId"" = u.""Id""
                        WHERE u.""Username"" = @u";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("u", username);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return (bool)result;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking profile: {ex.Message}");
                return false;
            }
        }

        public void SaveProfile(string username, string firstName, string lastName, string email, string gender, string country, string? phoneNumber, string? avatarPath)
        {
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    
                    // Get UserId
                    int userId;
                    using (var cmd = new NpgsqlCommand("SELECT \"Id\" FROM \"Users\" WHERE \"Username\" = @u", conn))
                    {
                        cmd.Parameters.AddWithValue("u", username);
                        userId = (int)(cmd.ExecuteScalar() ?? throw new Exception("User not found"));
                    }

                    var sql = @"
                        INSERT INTO ""UserProfiles"" (""UserId"", ""FirstName"", ""LastName"", ""Email"", ""Gender"", ""Country"", ""PhoneNumber"", ""AvatarPath"", ""IsComplete"")
                        VALUES (@uid, @fn, @ln, @em, @gn, @co, @ph, @av, TRUE)
                        ON CONFLICT (""UserId"") DO UPDATE SET
                            ""FirstName"" = EXCLUDED.""FirstName"",
                            ""LastName"" = EXCLUDED.""LastName"",
                            ""Email"" = EXCLUDED.""Email"",
                            ""Gender"" = EXCLUDED.""Gender"",
                            ""Country"" = EXCLUDED.""Country"",
                            ""PhoneNumber"" = EXCLUDED.""PhoneNumber"",
                            ""AvatarPath"" = EXCLUDED.""AvatarPath"",
                            ""IsComplete"" = TRUE;";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("uid", userId);
                        cmd.Parameters.AddWithValue("fn", firstName);
                        cmd.Parameters.AddWithValue("ln", lastName);
                        cmd.Parameters.AddWithValue("em", email);
                        cmd.Parameters.AddWithValue("gn", gender);
                        cmd.Parameters.AddWithValue("co", country);
                        cmd.Parameters.AddWithValue("ph", phoneNumber ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("av", avatarPath ?? (object)DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de la sauvegarde du profil : {ex.Message}");
            }
        }
    }
}

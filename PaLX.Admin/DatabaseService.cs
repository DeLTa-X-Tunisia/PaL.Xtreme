using System;
using System.Collections.Generic;
using System.Text;
using Npgsql;
using BCrypt.Net;

namespace PaLX.Admin
{
    public class UserProfileData
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Country { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? AvatarPath { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }

    public class FriendInfo
    {
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? AvatarPath { get; set; }
        public string Gender { get; set; } = "";
        public string Country { get; set; } = "";
        public int Age { get; set; }
        public string Status { get; set; } = "Hors ligne"; // For UI
        public int FriendshipStatus { get; set; } // 0: Pending, 1: Accepted, 2: None (Search result)
        public bool IsIncomingRequest { get; set; }
    }

    public class BlockedUserInfo
    {
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? AvatarPath { get; set; }
        public int Age { get; set; }
        public int BlockType { get; set; } // 0: Permanent, 1: 7Days, 2: DateRange
        public DateTime? EndDate { get; set; }
        public string Reason { get; set; } = "";
        
        public string BlockPeriodDisplay
        {
            get
            {
                if (BlockType == 0) return "Permanent";
                if (BlockType == 1) return "7 Jours";
                if (BlockType == 2 && EndDate.HasValue) return $"Jusqu'au {EndDate.Value:dd/MM/yyyy}";
                return "Inconnu";
            }
        }
    }

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

                    // Create Friendships Table
                    var createFriendshipsSql = @"
                        CREATE TABLE IF NOT EXISTS ""Friendships"" (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""RequesterId"" INT NOT NULL,
                            ""ReceiverId"" INT NOT NULL,
                            ""Status"" INT NOT NULL DEFAULT 0, -- 0: Pending, 1: Accepted, 2: Declined
                            ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                            FOREIGN KEY (""RequesterId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
                            FOREIGN KEY (""ReceiverId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
                            UNIQUE(""RequesterId"", ""ReceiverId"")
                        );";
                    using (var cmd = new NpgsqlCommand(createFriendshipsSql, conn)) cmd.ExecuteNonQuery();

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
                            ""DateOfBirth"" TIMESTAMP,
                            ""IsComplete"" BOOLEAN DEFAULT FALSE,
                            FOREIGN KEY (""UserId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE
                        );";
                    using (var cmd = new NpgsqlCommand(createUserProfilesSql, conn)) cmd.ExecuteNonQuery();

                    // Create BlockedUsers Table
                    var createBlockedUsersSql = @"
                        CREATE TABLE IF NOT EXISTS ""BlockedUsers"" (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""BlockerId"" INT NOT NULL,
                            ""BlockedId"" INT NOT NULL,
                            ""BlockType"" INT NOT NULL, -- 0: Permanent, 1: 7Days, 2: DateRange
                            ""StartDate"" TIMESTAMP NOT NULL DEFAULT NOW(),
                            ""EndDate"" TIMESTAMP,
                            ""Reason"" TEXT,
                            FOREIGN KEY (""BlockerId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
                            FOREIGN KEY (""BlockedId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
                            UNIQUE(""BlockerId"", ""BlockedId"")
                        );";
                    using (var cmd = new NpgsqlCommand(createBlockedUsersSql, conn)) cmd.ExecuteNonQuery();

                    // Add DateOfBirth column if it doesn't exist (migration)
                    var addDobSql = @"
                        DO $$ 
                        BEGIN 
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'UserProfiles' AND column_name = 'DateOfBirth') THEN
                                ALTER TABLE ""UserProfiles"" ADD COLUMN ""DateOfBirth"" TIMESTAMP;
                            END IF;
                        END $$;";
                    using (var cmd = new NpgsqlCommand(addDobSql, conn)) cmd.ExecuteNonQuery();

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

        public void SaveProfile(string username, string firstName, string lastName, string email, string gender, string country, string? phoneNumber, string? avatarPath, DateTime? dateOfBirth)
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
                        INSERT INTO ""UserProfiles"" (""UserId"", ""FirstName"", ""LastName"", ""Email"", ""Gender"", ""Country"", ""PhoneNumber"", ""AvatarPath"", ""DateOfBirth"", ""IsComplete"")
                        VALUES (@uid, @fn, @ln, @em, @gn, @co, @ph, @av, @dob, TRUE)
                        ON CONFLICT (""UserId"") DO UPDATE SET
                            ""FirstName"" = EXCLUDED.""FirstName"",
                            ""LastName"" = EXCLUDED.""LastName"",
                            ""Email"" = EXCLUDED.""Email"",
                            ""Gender"" = EXCLUDED.""Gender"",
                            ""Country"" = EXCLUDED.""Country"",
                            ""PhoneNumber"" = EXCLUDED.""PhoneNumber"",
                            ""AvatarPath"" = EXCLUDED.""AvatarPath"",
                            ""DateOfBirth"" = EXCLUDED.""DateOfBirth"",
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
                        cmd.Parameters.AddWithValue("dob", dateOfBirth ?? (object)DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur lors de la sauvegarde du profil : {ex.Message}");
            }
        }

        public UserProfileData? GetUserProfile(string username)
        {
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    var sql = @"
                        SELECT p.""FirstName"", p.""LastName"", p.""Email"", p.""Gender"", p.""Country"", p.""PhoneNumber"", p.""AvatarPath"", p.""DateOfBirth""
                        FROM ""UserProfiles"" p
                        JOIN ""Users"" u ON p.""UserId"" = u.""Id""
                        WHERE u.""Username"" = @u";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("u", username);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new UserProfileData
                                {
                                    FirstName = reader.GetString(0),
                                    LastName = reader.GetString(1),
                                    Email = reader.GetString(2),
                                    Gender = reader.GetString(3),
                                    Country = reader.GetString(4),
                                    PhoneNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    AvatarPath = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    DateOfBirth = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7)
                                };
                            }
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting profile: {ex.Message}");
                return null;
            }
        }

        public string? GetAvatarPath(string username)
        {
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    var sql = @"
                        SELECT p.""AvatarPath""
                        FROM ""UserProfiles"" p
                        JOIN ""Users"" u ON p.""UserId"" = u.""Id""
                        WHERE u.""Username"" = @u";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("u", username);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString();
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting avatar: {ex.Message}");
                return null;
            }
        }

        public List<FriendInfo> SearchUsers(string query, string currentUsername)
        {
            var results = new List<FriendInfo>();
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    var sql = @"
                        SELECT u.""Username"", p.""FirstName"", p.""LastName"", p.""AvatarPath"",
                               p.""Gender"", p.""Country"", p.""DateOfBirth"",
                               f.""Status"", f.""RequesterId""
                        FROM ""Users"" u
                        LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                        LEFT JOIN ""Friendships"" f ON 
                            (f.""RequesterId"" = u.""Id"" AND f.""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @cu)) OR
                            (f.""ReceiverId"" = u.""Id"" AND f.""RequesterId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @cu))
                        WHERE u.""Username"" != @cu
                        AND (@q = '' OR LOWER(u.""Username"") LIKE LOWER(@q) OR LOWER(p.""Email"") LIKE LOWER(@q) OR LOWER(p.""FirstName"") LIKE LOWER(@q) OR LOWER(p.""LastName"") LIKE LOWER(@q))";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("cu", currentUsername);
                        cmd.Parameters.AddWithValue("q", string.IsNullOrEmpty(query) ? "" : $"%{query}%");
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var username = reader.GetString(0);
                                var firstName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                var lastName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                var avatarPath = reader.IsDBNull(3) ? null : reader.GetString(3);
                                var gender = reader.IsDBNull(4) ? "" : reader.GetString(4);
                                var country = reader.IsDBNull(5) ? "" : reader.GetString(5);
                                var dob = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);
                                
                                int age = 0;
                                if (dob.HasValue)
                                {
                                    var today = DateTime.Today;
                                    age = today.Year - dob.Value.Year;
                                    if (dob.Value.Date > today.AddYears(-age)) age--;
                                }

                                int status = 2; // None
                                if (!reader.IsDBNull(7))
                                {
                                    status = reader.GetInt32(7);
                                }

                                results.Add(new FriendInfo
                                {
                                    Username = username,
                                    DisplayName = string.IsNullOrWhiteSpace(firstName) ? username : $"{lastName} {firstName}",
                                    AvatarPath = avatarPath,
                                    Gender = gender,
                                    Country = country,
                                    Age = age,
                                    FriendshipStatus = status
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            return results;
        }

        public void SendFriendRequest(string fromUsername, string toUsername)
        {
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    var sql = @"
                        INSERT INTO ""Friendships"" (""RequesterId"", ""ReceiverId"", ""Status"")
                        VALUES (
                            (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @fu),
                            (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @tu),
                            0
                        ) ON CONFLICT (""RequesterId"", ""ReceiverId"") DO UPDATE SET ""Status"" = 0";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("fu", fromUsername);
                        cmd.Parameters.AddWithValue("tu", toUsername);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        public List<FriendInfo> GetPendingRequests(string username)
        {
            var results = new List<FriendInfo>();
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    var sql = @"
                        SELECT u.""Username"", p.""FirstName"", p.""LastName"", p.""AvatarPath"", p.""Gender"", p.""DateOfBirth""
                        FROM ""Friendships"" f
                        JOIN ""Users"" u ON f.""RequesterId"" = u.""Id""
                        LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                        WHERE f.""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u)
                        AND f.""Status"" = 0";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("u", username);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var uname = reader.GetString(0);
                                var firstName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                var lastName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                var avatarPath = reader.IsDBNull(3) ? null : reader.GetString(3);
                                var gender = reader.IsDBNull(4) ? "" : reader.GetString(4);
                                var dob = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5);

                                int age = 0;
                                if (dob.HasValue)
                                {
                                    var today = DateTime.Today;
                                    age = today.Year - dob.Value.Year;
                                    if (dob.Value.Date > today.AddYears(-age)) age--;
                                }

                                results.Add(new FriendInfo
                                {
                                    Username = uname,
                                    DisplayName = string.IsNullOrWhiteSpace(firstName) ? uname : $"{lastName} {firstName}",
                                    AvatarPath = avatarPath,
                                    Gender = gender,
                                    Age = age,
                                    FriendshipStatus = 0,
                                    IsIncomingRequest = true
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            return results;
        }

        public void RespondToFriendRequest(string currentUsername, string requesterUsername, int action)
        {
            // action: 1=Accept (One-way), 2=AcceptAndAdd (Two-way), 3=Decline
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    
                    if (action == 3) // Decline
                    {
                        var sql = @"
                            UPDATE ""Friendships""
                            SET ""Status"" = 2
                            WHERE ""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @cu)
                            AND ""RequesterId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @ru)";
                        using (var cmd = new NpgsqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("cu", currentUsername);
                            cmd.Parameters.AddWithValue("ru", requesterUsername);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else // Accept (1) or AcceptAndAdd (2)
                    {
                        // 1. Update the incoming request to Accepted (1)
                        var sqlUpdate = @"
                            UPDATE ""Friendships""
                            SET ""Status"" = 1
                            WHERE ""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @cu)
                            AND ""RequesterId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @ru)";
                        using (var cmd = new NpgsqlCommand(sqlUpdate, conn))
                        {
                            cmd.Parameters.AddWithValue("cu", currentUsername);
                            cmd.Parameters.AddWithValue("ru", requesterUsername);
                            cmd.ExecuteNonQuery();
                        }

                        // 2. If AcceptAndAdd, insert reverse relationship
                        if (action == 2)
                        {
                            var sqlInsert = @"
                                INSERT INTO ""Friendships"" (""RequesterId"", ""ReceiverId"", ""Status"")
                                VALUES (
                                    (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @cu),
                                    (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @ru),
                                    1
                                ) ON CONFLICT (""RequesterId"", ""ReceiverId"") DO UPDATE SET ""Status"" = 1";
                            using (var cmd = new NpgsqlCommand(sqlInsert, conn))
                            {
                                cmd.Parameters.AddWithValue("cu", currentUsername);
                                cmd.Parameters.AddWithValue("ru", requesterUsername);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        public List<FriendInfo> GetFriends(string username)
        {
            var results = new List<FriendInfo>();
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    // Get all friends (both directions)
                    var sql = @"
                        SELECT u.""Username"", p.""FirstName"", p.""LastName"", p.""AvatarPath""
                        FROM ""Friendships"" f
                        JOIN ""Users"" u ON f.""ReceiverId"" = u.""Id""
                        LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                        WHERE f.""RequesterId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u)
                        AND f.""Status"" = 1
                        UNION
                        SELECT u.""Username"", p.""FirstName"", p.""LastName"", p.""AvatarPath""
                        FROM ""Friendships"" f
                        JOIN ""Users"" u ON f.""RequesterId"" = u.""Id""
                        LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                        WHERE f.""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u)
                        AND f.""Status"" = 1";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("u", username);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var uname = reader.GetString(0);
                                var firstName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                var lastName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                var avatarPath = reader.IsDBNull(3) ? null : reader.GetString(3);

                                results.Add(new FriendInfo
                                {
                                    Username = uname,
                                    DisplayName = string.IsNullOrWhiteSpace(firstName) ? uname : $"{lastName} {firstName}",
                                    AvatarPath = avatarPath,
                                    FriendshipStatus = 1,
                                    Status = "En ligne"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            return results;
        }

        public void BlockUser(string blockerUsername, string blockedUsername, int blockType, DateTime? endDate, string reason)
        {
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    var sql = @"
                        INSERT INTO ""BlockedUsers"" (""BlockerId"", ""BlockedId"", ""BlockType"", ""EndDate"", ""Reason"")
                        VALUES (
                            (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @bu),
                            (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @bdu),
                            @bt, @ed, @r
                        ) ON CONFLICT (""BlockerId"", ""BlockedId"") DO UPDATE SET
                            ""BlockType"" = EXCLUDED.""BlockType"",
                            ""EndDate"" = EXCLUDED.""EndDate"",
                            ""Reason"" = EXCLUDED.""Reason""";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("bu", blockerUsername);
                        cmd.Parameters.AddWithValue("bdu", blockedUsername);
                        cmd.Parameters.AddWithValue("bt", blockType);
                        cmd.Parameters.AddWithValue("ed", endDate ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("r", reason ?? "");
                        cmd.ExecuteNonQuery();
                    }

                    // Also remove friendship if exists
                    var sqlDelete = @"
                        DELETE FROM ""Friendships""
                        WHERE (""RequesterId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @bu) AND ""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @bdu))
                        OR (""ReceiverId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @bu) AND ""RequesterId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @bdu))";
                    using (var cmd = new NpgsqlCommand(sqlDelete, conn))
                    {
                        cmd.Parameters.AddWithValue("bu", blockerUsername);
                        cmd.Parameters.AddWithValue("bdu", blockedUsername);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        public void UnblockUser(string blockerUsername, string blockedUsername)
        {
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    var sql = @"
                        DELETE FROM ""BlockedUsers""
                        WHERE ""BlockerId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @bu)
                        AND ""BlockedId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @bdu)";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("bu", blockerUsername);
                        cmd.Parameters.AddWithValue("bdu", blockedUsername);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
        }

        public List<BlockedUserInfo> GetBlockedUsers(string username)
        {
            var results = new List<BlockedUserInfo>();
            try
            {
                using (var conn = new NpgsqlConnection(GetConnectionString(DatabaseName)))
                {
                    conn.Open();
                    var sql = @"
                        SELECT u.""Username"", p.""FirstName"", p.""LastName"", p.""AvatarPath"", p.""DateOfBirth"",
                               b.""BlockType"", b.""EndDate"", b.""Reason""
                        FROM ""BlockedUsers"" b
                        JOIN ""Users"" u ON b.""BlockedId"" = u.""Id""
                        LEFT JOIN ""UserProfiles"" p ON u.""Id"" = p.""UserId""
                        WHERE b.""BlockerId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u)";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("u", username);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var uname = reader.GetString(0);
                                var firstName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                var lastName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                var avatarPath = reader.IsDBNull(3) ? null : reader.GetString(3);
                                var dob = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4);
                                var blockType = reader.GetInt32(5);
                                var endDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);
                                var reason = reader.IsDBNull(7) ? "" : reader.GetString(7);

                                int age = 0;
                                if (dob.HasValue)
                                {
                                    var today = DateTime.Today;
                                    age = today.Year - dob.Value.Year;
                                    if (dob.Value.Date > today.AddYears(-age)) age--;
                                }

                                results.Add(new BlockedUserInfo
                                {
                                    Username = uname,
                                    DisplayName = string.IsNullOrWhiteSpace(firstName) ? uname : $"{lastName} {firstName}",
                                    AvatarPath = avatarPath,
                                    Age = age,
                                    BlockType = blockType,
                                    EndDate = endDate,
                                    Reason = reason
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            return results;
        }
    }
}

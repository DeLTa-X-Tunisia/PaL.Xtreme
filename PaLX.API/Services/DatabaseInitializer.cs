using Npgsql;

namespace PaLX.API.Services
{
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

            // 1. Room Categories
            var sqlCategories = @"
                CREATE TABLE IF NOT EXISTS ""RoomCategories"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Name"" VARCHAR(100) NOT NULL,
                    ""ParentId"" INT DEFAULT 0,
                    ""Order"" INT DEFAULT 0
                );";
            using (var cmd = new NpgsqlCommand(sqlCategories, conn)) await cmd.ExecuteNonQueryAsync();

            // Seed Room Categories if empty
            var checkCategories = "SELECT COUNT(*) FROM \"RoomCategories\"";
            using (var cmd = new NpgsqlCommand(checkCategories, conn))
            {
                var count = (long)(await cmd.ExecuteScalarAsync() ?? 0);
                if (count == 0)
                {
                    var seedCategories = @"
                        INSERT INTO ""RoomCategories"" (""Name"", ""ParentId"", ""Order"") VALUES
                        ('Général', 0, 1),
                        ('Rencontres', 0, 2),
                        ('Musique', 0, 3),
                        ('Jeux Vidéo', 0, 4),
                        ('Cinéma & Séries', 0, 5),
                        ('Technologie', 0, 6),
                        ('Sport', 0, 7),
                        ('Adulte (18+)', 0, 99);
                    ";
                    using (var seedCmd = new NpgsqlCommand(seedCategories, conn)) await seedCmd.ExecuteNonQueryAsync();
                }
            }

            // 2. Rooms
            var sqlRooms = @"
                CREATE TABLE IF NOT EXISTS ""Rooms"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Name"" VARCHAR(100) NOT NULL,
                    ""Description"" TEXT,
                    ""CategoryId"" INT NOT NULL,
                    ""OwnerId"" INT NOT NULL,
                    ""MaxUsers"" INT DEFAULT 50,
                    ""IsPrivate"" BOOLEAN DEFAULT FALSE,
                    ""Password"" VARCHAR(100),
                    ""Is18Plus"" BOOLEAN DEFAULT FALSE,
                    ""SubscriptionLevel"" INT DEFAULT 0,
                    ""CreatedAt"" TIMESTAMP DEFAULT NOW(),
                    ""IsActive"" BOOLEAN DEFAULT TRUE
                );";
            using (var cmd = new NpgsqlCommand(sqlRooms, conn)) await cmd.ExecuteNonQueryAsync();

            // 3. Room Roles
            var sqlRoomRoles = @"
                CREATE TABLE IF NOT EXISTS ""RoomRoles"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Name"" VARCHAR(50) NOT NULL,
                    ""Level"" INT NOT NULL,
                    ""Color"" VARCHAR(20),
                    ""Icon"" VARCHAR(100)
                );";
            using (var cmd = new NpgsqlCommand(sqlRoomRoles, conn)) await cmd.ExecuteNonQueryAsync();

            // Seed Room Roles if empty
            var checkRoles = "SELECT COUNT(*) FROM \"RoomRoles\"";
            using (var cmd = new NpgsqlCommand(checkRoles, conn))
            {
                var count = (long)(await cmd.ExecuteScalarAsync() ?? 0);
                if (count == 0)
                {
                    var seedRoles = @"
                        INSERT INTO ""RoomRoles"" (""Name"", ""Level"", ""Color"", ""Icon"") VALUES
                        ('RoomOwner', 1, '#FF0000', 'crown'),
                        ('RoomSuperAdmin', 2, '#FF4500', 'shield-star'),
                        ('RoomAdmin', 3, '#FFA500', 'shield'),
                        ('PowerUser', 4, '#008000', 'lightning'),
                        ('RoomModerator', 5, '#0000FF', 'gavel'),
                        ('RoomMember', 6, '#000000', 'user');
                    ";
                    using (var seedCmd = new NpgsqlCommand(seedRoles, conn)) await seedCmd.ExecuteNonQueryAsync();
                }
            }

            // 4. Room Members
            var sqlRoomMembers = @"
                CREATE TABLE IF NOT EXISTS ""RoomMembers"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""RoomId"" INT NOT NULL,
                    ""UserId"" INT NOT NULL,
                    ""RoleId"" INT NOT NULL,
                    ""JoinedAt"" TIMESTAMP DEFAULT NOW(),
                    ""IsBanned"" BOOLEAN DEFAULT FALSE,
                    ""IsMuted"" BOOLEAN DEFAULT FALSE,
                    ""HasHandRaised"" BOOLEAN DEFAULT FALSE,
                    ""IsCamOn"" BOOLEAN DEFAULT FALSE,
                    ""IsMicOn"" BOOLEAN DEFAULT FALSE,
                    UNIQUE(""RoomId"", ""UserId"")
                );";
            using (var cmd = new NpgsqlCommand(sqlRoomMembers, conn)) await cmd.ExecuteNonQueryAsync();

            // 5. Room Messages
            var sqlRoomMessages = @"
                CREATE TABLE IF NOT EXISTS ""RoomMessages"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""RoomId"" INT NOT NULL,
                    ""UserId"" INT NOT NULL,
                    ""Content"" TEXT,
                    ""MessageType"" VARCHAR(20) DEFAULT 'Text',
                    ""Timestamp"" TIMESTAMP DEFAULT NOW(),
                    ""AttachmentUrl"" TEXT
                );";
            using (var cmd = new NpgsqlCommand(sqlRoomMessages, conn)) await cmd.ExecuteNonQueryAsync();

            // 6. User Subscriptions
            var sqlUserSubs = @"
                CREATE TABLE IF NOT EXISTS ""UserSubscriptions"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""UserId"" INT NOT NULL,
                    ""SubscriptionType"" INT NOT NULL,
                    ""StartDate"" TIMESTAMP DEFAULT NOW(),
                    ""EndDate"" TIMESTAMP,
                    ""IsActive"" BOOLEAN DEFAULT TRUE
                );";
            using (var cmd = new NpgsqlCommand(sqlUserSubs, conn)) await cmd.ExecuteNonQueryAsync();
        }
    }
}

using PaLX.API.Enums;
using PaLX.API.Models;
using Npgsql;

namespace PaLX.API.Services
{
    public interface IAccessControlService
    {
        Task<UserSubscriptionLevel> GetUserSubscriptionLevelAsync(int userId);
        Task<bool> CanCreateRoomAsync(int userId, RoomSubscriptionLevel roomLevel, bool is18Plus);
        Task<bool> CanJoinRoomAsync(int roomId);
        bool HasPermission(RoomRoleLevel userRole, RoomRoleLevel requiredRole);
        int GetMaxRoomsAllowed(UserSubscriptionLevel level);
        int GetMaxRoomCapacity(RoomSubscriptionLevel level);
    }

    public class AccessControlService : IAccessControlService
    {
        private readonly string _connectionString;

        public AccessControlService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("Connection string not found.");
        }

        public async Task<UserSubscriptionLevel> GetUserSubscriptionLevelAsync(int userId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var sql = "SELECT \"SubscriptionType\" FROM \"UserSubscriptions\" WHERE \"UserId\" = @uid AND \"IsActive\" = TRUE ORDER BY \"EndDate\" DESC LIMIT 1";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("uid", userId);
            var result = await cmd.ExecuteScalarAsync();
            
            if (result == null || result == DBNull.Value) return UserSubscriptionLevel.Member;
            return (UserSubscriptionLevel)Convert.ToInt32(result);
        }

        public int GetMaxRoomsAllowed(UserSubscriptionLevel level)
        {
            return level switch
            {
                UserSubscriptionLevel.Member => 1,
                UserSubscriptionLevel.Deluxe => 3,
                UserSubscriptionLevel.VIP => 5,
                UserSubscriptionLevel.Royal => 10,
                UserSubscriptionLevel.Legend => 9999,
                _ => 1
            };
        }

        public int GetMaxRoomCapacity(RoomSubscriptionLevel level)
        {
            return level switch
            {
                RoomSubscriptionLevel.Basic => 20,
                RoomSubscriptionLevel.Deluxe => 50,
                RoomSubscriptionLevel.VIP => 100,
                RoomSubscriptionLevel.Royal => 200,
                RoomSubscriptionLevel.Legend => 500,
                _ => 20
            };
        }

        public async Task<bool> CanCreateRoomAsync(int userId, RoomSubscriptionLevel roomLevel, bool is18Plus)
        {
            // For testing purposes, allow everyone to create rooms
            return true;

            /*
            var userLevel = await GetUserSubscriptionLevelAsync(userId);

            // 1. Check if user level allows creating this room level
            // Logic: User Level must be >= Room Level
            if ((int)userLevel < (int)roomLevel) return false;

            // 2. Check 18+ restriction (VIP+ required)
            if (is18Plus && userLevel < UserSubscriptionLevel.VIP) return false;

            // 3. Check Max Rooms
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var sql = "SELECT COUNT(*) FROM \"Rooms\" WHERE \"OwnerId\" = @uid AND \"IsActive\" = TRUE";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("uid", userId);
            long count = (long)(await cmd.ExecuteScalarAsync() ?? 0);

            return count < GetMaxRoomsAllowed(userLevel);
            */
        }

        public async Task<bool> CanJoinRoomAsync(int roomId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            
            // Get Room Level and Current Count
            var sql = @"
                SELECT r.""SubscriptionLevel"", 
                       (SELECT COUNT(*) FROM ""RoomMembers"" WHERE ""RoomId"" = r.""Id"") as Count
                FROM ""Rooms"" r WHERE r.""Id"" = @rid";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("rid", roomId);
            
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return false;

            var roomLevel = (RoomSubscriptionLevel)reader.GetInt32(0);
            var count = reader.GetInt64(1);

            return count < GetMaxRoomCapacity(roomLevel);
        }

        public bool HasPermission(RoomRoleLevel userRole, RoomRoleLevel requiredRole)
        {
            return userRole <= requiredRole; // Lower number = Higher rank (1=Owner)
        }
    }
}

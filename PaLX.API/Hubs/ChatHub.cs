using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace PaLX.API.Hubs
{
    public class ChatHub : Hub
    {
        private readonly string _connectionString;

        public ChatHub(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("Connection string not found.");
        }

        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public async Task SendPrivateMessage(string receiver, string message)
        {
            var sender = Context.UserIdentifier;
            if (string.IsNullOrEmpty(sender)) return;

            // Save to DB
            await SaveMessageAsync(sender, receiver, message);

            // Send to Receiver
            await Clients.User(receiver).SendAsync("ReceivePrivateMessage", sender, message);
        }

        public async Task UserTyping(string receiver)
        {
            var sender = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(sender))
            {
                await Clients.User(receiver).SendAsync("UserTyping", sender);
            }
        }

        public async Task SendBuzz(string receiver)
        {
            var sender = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(sender))
            {
                await Clients.User(receiver).SendAsync("ReceiveBuzz", sender);
            }
        }

        public override async Task OnConnectedAsync()
        {
            var username = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(username))
            {
                // Check current status in UserSessions
                int currentStatus = await GetUserSessionStatusAsync(username);
                
                // Only set to Online (0) if currently Offline (6)
                if (currentStatus == 6)
                {
                    await UpdateUserSessionStatusAsync(username, 0);
                    await Clients.All.SendAsync("UserStatusChanged", username, "En ligne");
                }
                else
                {
                    // Broadcast current status to ensure sync
                    string statusStr = GetStatusString(currentStatus);
                    await Clients.All.SendAsync("UserStatusChanged", username, statusStr);
                }
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var username = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(username))
            {
                // Update UserSessions to Offline (6)
                await UpdateUserSessionStatusAsync(username, 6);
                // Broadcast Offline Status
                await Clients.All.SendAsync("UserStatusChanged", username, "Hors ligne");
            }
            await base.OnDisconnectedAsync(exception);
        }

        private async Task<int> GetUserSessionStatusAsync(string username)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                var sql = @"
                    SELECT ""DisplayedStatus"" 
                    FROM ""UserSessions"" s
                    JOIN ""Users"" u ON s.""UserId"" = u.""Id""
                    WHERE u.""Username"" = @u AND s.""DéconnectéLe"" IS NULL
                    ORDER BY s.""ConnectéLe"" DESC 
                    LIMIT 1";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("u", username);
                var result = await cmd.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 6;
            }
            catch { return 6; }
        }

        private string GetStatusString(int status)
        {
            return status switch
            {
                0 => "En ligne",
                1 => "Occupé",
                2 => "Absent",
                3 => "En appel",
                4 => "Ne pas déranger",
                _ => "Hors ligne"
            };
        }

        private async Task UpdateUserSessionStatusAsync(string username, int statusValue)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                var sql = @"
                    UPDATE ""UserSessions"" 
                    SET ""DisplayedStatus"" = @st 
                    WHERE ""UserId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = @u)
                      AND ""DéconnectéLe"" IS NULL";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("st", statusValue);
                cmd.Parameters.AddWithValue("u", username);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* Ignore DB errors during signalr events */ }
        }

        private async Task SaveMessageAsync(string sender, string receiver, string content)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO ""Messages"" (""SenderUsername"", ""ReceiverUsername"", ""Content"", ""Timestamp"", ""IsRead"")
                VALUES (@s, @r, @c, NOW(), FALSE)";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("s", sender);
            cmd.Parameters.AddWithValue("r", receiver);
            cmd.Parameters.AddWithValue("c", content);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
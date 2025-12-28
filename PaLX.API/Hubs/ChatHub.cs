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

        public async Task SendImageRequest(string receiver, string fileUrl, string fileName, long fileSize)
        {
            var sender = Context.UserIdentifier;
            if (string.IsNullOrEmpty(sender)) return;

            int fileId = 0;
            // Save to DB
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                var sql = @"
                    INSERT INTO ""FileTransfers"" (""SenderUsername"", ""ReceiverUsername"", ""FileUrl"", ""FileName"", ""FileSize"", ""Status"", ""Timestamp"")
                    VALUES (@s, @r, @url, @name, @size, 0, NOW())
                    RETURNING ""Id""";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("s", sender);
                cmd.Parameters.AddWithValue("r", receiver);
                cmd.Parameters.AddWithValue("url", fileUrl);
                cmd.Parameters.AddWithValue("name", fileName ?? "image.png");
                cmd.Parameters.AddWithValue("size", fileSize);
                fileId = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            }

            // Notify Receiver
            await Clients.User(receiver).SendAsync("ReceiveImageRequest", fileId, sender, fileName, fileUrl);
            
            // Notify Sender (to show pending state)
            await Clients.Caller.SendAsync("ImageRequestSent", fileId, receiver, fileName, fileUrl);
        }

        public async Task RespondToImageRequest(int fileId, bool isAccepted)
        {
            var responder = Context.UserIdentifier;
            if (string.IsNullOrEmpty(responder)) return;

            string sender = "";
            string receiver = "";
            string fileUrl = "";
            string fileName = "";

            // Update DB and Get Info
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                
                // Verify responder is the receiver
                var checkSql = @"SELECT ""SenderUsername"", ""ReceiverUsername"", ""FileUrl"", ""FileName"" FROM ""FileTransfers"" WHERE ""Id"" = @id";
                using (var checkCmd = new NpgsqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("id", fileId);
                    using var reader = await checkCmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        sender = reader.GetString(0);
                        receiver = reader.GetString(1);
                        fileUrl = reader.GetString(2);
                        fileName = reader.IsDBNull(3) ? "image.png" : reader.GetString(3);
                    }
                    else return; // Not found
                }

                if (receiver != responder) return; // Unauthorized

                var updateSql = @"UPDATE ""FileTransfers"" SET ""Status"" = @st WHERE ""Id"" = @id";
                using (var updateCmd = new NpgsqlCommand(updateSql, conn))
                {
                    updateCmd.Parameters.AddWithValue("st", isAccepted ? 1 : 2);
                    updateCmd.Parameters.AddWithValue("id", fileId);
                    await updateCmd.ExecuteNonQueryAsync();
                }
            }

            // Notify Both
            await Clients.User(sender).SendAsync("ImageTransferUpdated", fileId, isAccepted, fileUrl);
            await Clients.User(receiver).SendAsync("ImageTransferUpdated", fileId, isAccepted, fileUrl);
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

                // PUSH OFFLINE MESSAGES
                await PushOfflineMessagesAsync(username);
            }
            await base.OnConnectedAsync();
        }

        private async Task PushOfflineMessagesAsync(string username)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // Get Unread Senders
                var senders = new List<string>();
                var sql = @"
                    SELECT DISTINCT ""SenderUsername"" FROM ""Messages"" WHERE LOWER(""ReceiverUsername"") = LOWER(@u) AND ""IsRead"" = false
                    UNION
                    SELECT DISTINCT ""SenderUsername"" FROM ""FileTransfers"" WHERE LOWER(""ReceiverUsername"") = LOWER(@u) AND ""IsRead"" = false";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("u", username);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        senders.Add(reader.GetString(0));
                    }
                }

                // Push to Client
                foreach (var sender in senders)
                {
                    await Clients.Caller.SendAsync("ReceivePrivateMessage", sender, "Nouveau message (Offline)");
                }
            }
            catch { }
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

        public async Task SendVideoRequest(string receiver, string fileUrl, string fileName, long fileSize)
        {
            var sender = Context.UserIdentifier;
            if (string.IsNullOrEmpty(sender)) return;

            int fileId = 0;
            // Save to DB (Reuse FileTransfers table)
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                var sql = @"
                    INSERT INTO ""FileTransfers"" (""SenderUsername"", ""ReceiverUsername"", ""FileUrl"", ""FileName"", ""FileSize"", ""Status"", ""Timestamp"")
                    VALUES (@s, @r, @url, @name, @size, 0, NOW())
                    RETURNING ""Id""";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("s", sender);
                cmd.Parameters.AddWithValue("r", receiver);
                cmd.Parameters.AddWithValue("url", fileUrl);
                cmd.Parameters.AddWithValue("name", fileName ?? "video.mp4");
                cmd.Parameters.AddWithValue("size", fileSize);
                fileId = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            }

            // Notify Receiver
            await Clients.User(receiver).SendAsync("ReceiveVideoRequest", fileId, sender, fileName, fileUrl);
            
            // Notify Sender
            await Clients.Caller.SendAsync("VideoRequestSent", fileId, receiver, fileName, fileUrl);
        }

        public async Task RespondToVideoRequest(int fileId, bool isAccepted)
        {
            var responder = Context.UserIdentifier;
            if (string.IsNullOrEmpty(responder)) return;

            string sender = "";
            string receiver = "";
            string fileUrl = "";
            string fileName = "";

            // Update DB and Get Info
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                
                // Verify responder is the receiver
                var checkSql = @"SELECT ""SenderUsername"", ""ReceiverUsername"", ""FileUrl"", ""FileName"" FROM ""FileTransfers"" WHERE ""Id"" = @id";
                using (var checkCmd = new NpgsqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("id", fileId);
                    using (var reader = await checkCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            sender = reader.GetString(0);
                            receiver = reader.GetString(1);
                            fileUrl = reader.GetString(2);
                            fileName = reader.GetString(3);
                        }
                    }
                }

                if (receiver != responder) return; // Unauthorized

                // Update Status
                var updateSql = @"UPDATE ""FileTransfers"" SET ""Status"" = @status WHERE ""Id"" = @id";
                using (var updateCmd = new NpgsqlCommand(updateSql, conn))
                {
                    updateCmd.Parameters.AddWithValue("status", isAccepted ? 1 : 2);
                    updateCmd.Parameters.AddWithValue("id", fileId);
                    await updateCmd.ExecuteNonQueryAsync();
                }
            }

            // Notify Both Parties
            await Clients.User(sender).SendAsync("VideoTransferUpdated", fileId, isAccepted, fileUrl);
            await Clients.User(receiver).SendAsync("VideoTransferUpdated", fileId, isAccepted, fileUrl);
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
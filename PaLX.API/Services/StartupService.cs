using Npgsql;

namespace PaLX.API.Services
{
    public class StartupService : IHostedService
    {
        private readonly string _connectionString;

        public StartupService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                                ?? throw new InvalidOperationException("Connection string not found.");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Reset all active sessions to Offline (6) on startup
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                // Ensure FileTransfers table exists
                var createTableSql = @"
                    CREATE TABLE IF NOT EXISTS ""FileTransfers"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""SenderUsername"" TEXT NOT NULL,
                        ""ReceiverUsername"" TEXT NOT NULL,
                        ""FileName"" TEXT NOT NULL,
                        ""FilePath"" TEXT NOT NULL,
                        ""FileSize"" BIGINT NOT NULL,
                        ""ContentType"" TEXT,
                        ""SentAt"" TIMESTAMP NOT NULL,
                        ""Status"" INT NOT NULL DEFAULT 0
                    );";
                using (var cmdTable = new NpgsqlCommand(createTableSql, conn))
                {
                    await cmdTable.ExecuteNonQueryAsync(cancellationToken);
                }

                var sql = @"
                    UPDATE ""UserSessions"" 
                    SET ""DisplayedStatus"" = 6 
                    WHERE ""DéconnectéLe"" IS NULL";
                using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch
            {
                // Log error or ignore if DB not ready
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
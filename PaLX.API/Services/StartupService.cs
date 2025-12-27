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
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                // Create FileTransfers table if not exists
                var createTableSql = @"
                    CREATE TABLE IF NOT EXISTS ""FileTransfers"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""SenderUsername"" TEXT NOT NULL,
                        ""ReceiverUsername"" TEXT NOT NULL,
                        ""FileName"" TEXT,
                        ""FileUrl"" TEXT NOT NULL,
                        ""FileSize"" BIGINT,
                        ""Status"" INT DEFAULT 0, -- 0: Pending, 1: Accepted, 2: Declined
                        ""Timestamp"" TIMESTAMP DEFAULT NOW()
                    );";
                using var cmdTable = new NpgsqlCommand(createTableSql, conn);
                await cmdTable.ExecuteNonQueryAsync(cancellationToken);

                // Migration: Ensure columns exist (for existing tables)
                try 
                {
                    var alterTableSql = @"
                        ALTER TABLE ""FileTransfers"" ADD COLUMN IF NOT EXISTS ""FileUrl"" TEXT;
                        ALTER TABLE ""FileTransfers"" ADD COLUMN IF NOT EXISTS ""FileSize"" BIGINT;
                        ALTER TABLE ""FileTransfers"" ADD COLUMN IF NOT EXISTS ""FileName"" TEXT;
                        ALTER TABLE ""FileTransfers"" ADD COLUMN IF NOT EXISTS ""Timestamp"" TIMESTAMP DEFAULT NOW();
                        ALTER TABLE ""FileTransfers"" ADD COLUMN IF NOT EXISTS ""IsRead"" BOOLEAN DEFAULT FALSE;
                    ";
                    using var cmdAlter = new NpgsqlCommand(alterTableSql, conn);
                    await cmdAlter.ExecuteNonQueryAsync(cancellationToken);

                    // Fix for legacy schema where FilePath might exist and be NOT NULL
                    var fixLegacySql = @"
                        ALTER TABLE ""FileTransfers"" ALTER COLUMN ""FilePath"" DROP NOT NULL;
                        ALTER TABLE ""FileTransfers"" ALTER COLUMN ""SentAt"" DROP NOT NULL;
                    ";
                    using var cmdFix = new NpgsqlCommand(fixLegacySql, conn);
                    await cmdFix.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Migration Warning: {ex.Message}");
                }

                // Reset all active sessions to Offline (6) on startup
                var sql = @"
                    UPDATE ""UserSessions"" 
                    SET ""DisplayedStatus"" = 6 
                    WHERE ""DéconnectéLe"" IS NULL";
                using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StartupService Error: {ex.Message}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
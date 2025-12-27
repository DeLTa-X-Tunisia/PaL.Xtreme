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
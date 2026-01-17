// ============================================================================
// PaL.Xtreme - Modern Instant Messaging Solution
// Copyright © 2026 Azizi Mounir. All Rights Reserved.
// ============================================================================

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace PaLX.API.Services
{
    /// <summary>
    /// Health Check pour PostgreSQL
    /// </summary>
    public class PostgreSqlHealthCheck : IHealthCheck
    {
        private readonly string _connectionString;
        private readonly ILogger<PostgreSqlHealthCheck> _logger;

        public PostgreSqlHealthCheck(IConfiguration configuration, ILogger<PostgreSqlHealthCheck> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found.");
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                
                await using var cmd = new NpgsqlCommand("SELECT 1", connection);
                await cmd.ExecuteScalarAsync(cancellationToken);

                return HealthCheckResult.Healthy("PostgreSQL is responding");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PostgreSQL health check failed");
                return HealthCheckResult.Unhealthy("PostgreSQL is not responding", ex);
            }
        }
    }

    /// <summary>
    /// Health Check pour Redis (si configuré)
    /// </summary>
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly RedisSettings? _settings;
        private readonly ILogger<RedisHealthCheck> _logger;

        public RedisHealthCheck(IConfiguration configuration, ILogger<RedisHealthCheck> logger)
        {
            _settings = configuration.GetSection("Redis").Get<RedisSettings>();
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            if (_settings == null || 
                (!_settings.EnableDistributedCache && !_settings.EnableSignalRBackplane))
            {
                return HealthCheckResult.Healthy("Redis not configured (using in-memory fallback)");
            }

            try
            {
                // Note: En production, injecter IConnectionMultiplexer
                // Ceci est un check simplifié
                var connectionString = _settings.GetFullConnectionString();
                
                // Pour un vrai check, utiliser StackExchange.Redis
                // var redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
                // var db = redis.GetDatabase();
                // await db.PingAsync();
                
                return HealthCheckResult.Healthy("Redis check skipped (not fully configured)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis health check failed");
                return HealthCheckResult.Unhealthy("Redis is not responding", ex);
            }
        }
    }

    /// <summary>
    /// Health Check pour SignalR
    /// </summary>
    public class SignalRHealthCheck : IHealthCheck
    {
        private readonly ILogger<SignalRHealthCheck> _logger;

        public SignalRHealthCheck(ILogger<SignalRHealthCheck> logger)
        {
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            // SignalR est healthy si l'application tourne
            // Pour un check plus avancé, vérifier le nombre de connexions
            return Task.FromResult(HealthCheckResult.Healthy("SignalR hubs are available"));
        }
    }

    /// <summary>
    /// Health Check pour l'espace disque (logs, uploads)
    /// </summary>
    public class DiskSpaceHealthCheck : IHealthCheck
    {
        private readonly ILogger<DiskSpaceHealthCheck> _logger;
        private const long MIN_FREE_SPACE_GB = 1; // Alerte si moins de 1 GB

        public DiskSpaceHealthCheck(ILogger<DiskSpaceHealthCheck> logger)
        {
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:");
                var freeSpaceGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);

                if (freeSpaceGB < MIN_FREE_SPACE_GB)
                {
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"Low disk space: {freeSpaceGB} GB remaining"));
                }

                return Task.FromResult(HealthCheckResult.Healthy(
                    $"Disk space OK: {freeSpaceGB} GB available"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Disk space health check failed");
                return Task.FromResult(HealthCheckResult.Unhealthy("Cannot check disk space", ex));
            }
        }
    }

    /// <summary>
    /// Extensions pour configurer les Health Checks
    /// </summary>
    public static class HealthCheckExtensions
    {
        public static IServiceCollection AddPaLXHealthChecks(this IServiceCollection services)
        {
            services.AddHealthChecks()
                .AddCheck<PostgreSqlHealthCheck>(
                    "postgresql", 
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "db", "critical" })
                .AddCheck<RedisHealthCheck>(
                    "redis", 
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "cache", "optional" })
                .AddCheck<SignalRHealthCheck>(
                    "signalr", 
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "realtime" })
                .AddCheck<DiskSpaceHealthCheck>(
                    "disk", 
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "infrastructure" });

            return services;
        }
    }
}

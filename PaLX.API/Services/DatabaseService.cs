// ============================================================================
// PaL.Xtreme - Modern Instant Messaging Solution
// Copyright © 2026 Azizi Mounir. All Rights Reserved.
// ============================================================================

using Npgsql;

namespace PaLX.API.Services
{
    /// <summary>
    /// Service centralisé pour la gestion des connexions PostgreSQL
    /// Utilise NpgsqlDataSource pour un pooling optimisé et des performances accrues
    /// 
    /// Avantages par rapport à "new NpgsqlConnection()" partout:
    /// - Connection pooling natif et optimisé
    /// - Multiplexing des connexions (plusieurs requêtes sur une connexion)
    /// - Préparation automatique des statements fréquents
    /// - Meilleure gestion des ressources
    /// - Stats de monitoring intégrées
    /// </summary>
    public interface IDatabaseService
    {
        /// <summary>
        /// Obtient une connexion du pool
        /// </summary>
        Task<NpgsqlConnection> GetConnectionAsync();
        
        /// <summary>
        /// Obtient la source de données pour utilisation directe
        /// </summary>
        NpgsqlDataSource DataSource { get; }
        
        /// <summary>
        /// Statistiques du pool de connexions
        /// </summary>
        DatabasePoolStats GetPoolStats();
    }

    /// <summary>
    /// Statistiques du pool de connexions
    /// </summary>
    public class DatabasePoolStats
    {
        public int TotalConnections { get; set; }
        public int IdleConnections { get; set; }
        public int BusyConnections { get; set; }
        public int WaitingRequests { get; set; }
        public long TotalConnectionsOpened { get; set; }
        public long TotalConnectionsClosed { get; set; }
    }

    /// <summary>
    /// Implémentation du service de base de données avec pooling optimisé
    /// </summary>
    public class DatabaseService : IDatabaseService, IDisposable
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly ILogger<DatabaseService> _logger;
        private readonly DatabaseSettings _settings;

        public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
        {
            _logger = logger;
            
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // Charger les paramètres de configuration
            _settings = configuration.GetSection("Database").Get<DatabaseSettings>() ?? new DatabaseSettings();
            
            // Construire la DataSource avec le pooling optimisé
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            
            // ═══════════════════════════════════════════════════════════════
            // CONFIGURATION DU POOL DE CONNEXIONS
            // ═══════════════════════════════════════════════════════════════
            dataSourceBuilder.ConnectionStringBuilder.MinPoolSize = _settings.MinPoolSize;
            dataSourceBuilder.ConnectionStringBuilder.MaxPoolSize = _settings.MaxPoolSize;
            dataSourceBuilder.ConnectionStringBuilder.ConnectionIdleLifetime = _settings.ConnectionIdleLifetimeSeconds;
            dataSourceBuilder.ConnectionStringBuilder.ConnectionPruningInterval = _settings.ConnectionPruningIntervalSeconds;
            dataSourceBuilder.ConnectionStringBuilder.Timeout = _settings.CommandTimeoutSeconds;
            
            // ═══════════════════════════════════════════════════════════════
            // MULTIPLEXING (optionnel, améliore les performances)
            // Permet d'envoyer plusieurs requêtes sur une même connexion
            // ═══════════════════════════════════════════════════════════════
            if (_settings.EnableMultiplexing)
            {
                dataSourceBuilder.ConnectionStringBuilder.Multiplexing = true;
                _logger.LogInformation("🔄 Database multiplexing enabled");
            }
            
            // ═══════════════════════════════════════════════════════════════
            // LOGGING DES REQUÊTES (dev only)
            // ═══════════════════════════════════════════════════════════════
            if (_settings.EnableQueryLogging)
            {
                dataSourceBuilder.EnableParameterLogging();
            }

            _dataSource = dataSourceBuilder.Build();
            
            _logger.LogInformation(
                "✅ Database pool initialized: MinPool={Min}, MaxPool={Max}, Multiplexing={Multiplex}",
                _settings.MinPoolSize, _settings.MaxPoolSize, _settings.EnableMultiplexing);
        }

        public NpgsqlDataSource DataSource => _dataSource;

        public async Task<NpgsqlConnection> GetConnectionAsync()
        {
            var connection = await _dataSource.OpenConnectionAsync();
            return connection;
        }

        public DatabasePoolStats GetPoolStats()
        {
            // Note: NpgsqlDataSource ne expose pas directement toutes les stats
            // En production, utiliser les métriques Prometheus/OpenTelemetry
            var stats = NpgsqlConnection.GlobalTypeMapper; // Placeholder pour les vraies stats
            
            return new DatabasePoolStats
            {
                TotalConnections = _settings.MaxPoolSize,
                IdleConnections = 0, // Nécessite accès aux internals
                BusyConnections = 0,
                WaitingRequests = 0,
                TotalConnectionsOpened = 0,
                TotalConnectionsClosed = 0
            };
        }

        public void Dispose()
        {
            _dataSource.Dispose();
            _logger.LogInformation("Database pool disposed");
        }
    }

    /// <summary>
    /// Configuration du pool de connexions
    /// </summary>
    public class DatabaseSettings
    {
        /// <summary>Nombre minimum de connexions dans le pool (défaut: 10)</summary>
        public int MinPoolSize { get; set; } = 10;
        
        /// <summary>Nombre maximum de connexions dans le pool (défaut: 100)</summary>
        public int MaxPoolSize { get; set; } = 100;
        
        /// <summary>Durée de vie d'une connexion inactive en secondes (défaut: 300)</summary>
        public int ConnectionIdleLifetimeSeconds { get; set; } = 300;
        
        /// <summary>Intervalle de nettoyage des connexions inactives en secondes (défaut: 60)</summary>
        public int ConnectionPruningIntervalSeconds { get; set; } = 60;
        
        /// <summary>Timeout des commandes en secondes (défaut: 30)</summary>
        public int CommandTimeoutSeconds { get; set; } = 30;
        
        /// <summary>Active le multiplexing (plusieurs requêtes par connexion)</summary>
        public bool EnableMultiplexing { get; set; } = false; // Désactivé par défaut car nécessite Npgsql 7+
        
        /// <summary>Active le logging des requêtes (dev only)</summary>
        public bool EnableQueryLogging { get; set; } = false;
    }

    /// <summary>
    /// Extensions pour faciliter l'utilisation du DatabaseService
    /// </summary>
    public static class DatabaseServiceExtensions
    {
        /// <summary>
        /// Exécute une requête avec une connexion du pool
        /// </summary>
        public static async Task<T> ExecuteAsync<T>(
            this IDatabaseService db, 
            Func<NpgsqlConnection, Task<T>> action)
        {
            await using var conn = await db.GetConnectionAsync();
            return await action(conn);
        }
        
        /// <summary>
        /// Exécute une requête sans retour avec une connexion du pool
        /// </summary>
        public static async Task ExecuteAsync(
            this IDatabaseService db, 
            Func<NpgsqlConnection, Task> action)
        {
            await using var conn = await db.GetConnectionAsync();
            await action(conn);
        }
        
        /// <summary>
        /// Exécute une requête dans une transaction
        /// </summary>
        public static async Task<T> ExecuteInTransactionAsync<T>(
            this IDatabaseService db,
            Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> action)
        {
            await using var conn = await db.GetConnectionAsync();
            await using var transaction = await conn.BeginTransactionAsync();
            
            try
            {
                var result = await action(conn, transaction);
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}

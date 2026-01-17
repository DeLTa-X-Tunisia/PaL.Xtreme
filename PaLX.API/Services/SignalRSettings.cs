// ============================================================================
// PaL.Xtreme - Modern Instant Messaging Solution
// Copyright © 2026 Azizi Mounir. All Rights Reserved.
// ============================================================================

namespace PaLX.API.Services
{
    /// <summary>
    /// Configuration centralisée pour SignalR haute performance
    /// Supporte le scaling horizontal avec Redis backplane
    /// </summary>
    public class SignalRSettings
    {
        /// <summary>Taille maximale d'un message (défaut: 64KB)</summary>
        public int MaximumReceiveMessageSizeKB { get; set; } = 64;
        
        /// <summary>Capacité du buffer de streaming</summary>
        public int StreamBufferCapacity { get; set; } = 20;
        
        /// <summary>Intervalle de keep-alive en secondes</summary>
        public int KeepAliveIntervalSeconds { get; set; } = 15;
        
        /// <summary>Timeout client en secondes</summary>
        public int ClientTimeoutSeconds { get; set; } = 60;
        
        /// <summary>Intervalle de handshake en secondes</summary>
        public int HandshakeTimeoutSeconds { get; set; } = 15;
        
        /// <summary>Active les erreurs détaillées (dev only)</summary>
        public bool EnableDetailedErrors { get; set; } = false;
        
        /// <summary>Nombre max de connexions parallèles par utilisateur</summary>
        public int MaxConnectionsPerUser { get; set; } = 5;
    }

    /// <summary>
    /// Configuration Redis pour le backplane SignalR et le cache distribué
    /// </summary>
    public class RedisSettings
    {
        /// <summary>Chaîne de connexion Redis (ex: "localhost:6379")</summary>
        public string ConnectionString { get; set; } = "localhost:6379";
        
        /// <summary>Mot de passe Redis (optionnel)</summary>
        public string? Password { get; set; }
        
        /// <summary>Nom de l'instance (pour le préfixe des clés)</summary>
        public string InstanceName { get; set; } = "PaLX:";
        
        /// <summary>Database Redis (0-15)</summary>
        public int Database { get; set; } = 0;
        
        /// <summary>Timeout de connexion en ms</summary>
        public int ConnectTimeoutMs { get; set; } = 5000;
        
        /// <summary>Timeout de synchronisation en ms</summary>
        public int SyncTimeoutMs { get; set; } = 5000;
        
        /// <summary>Active le SSL</summary>
        public bool UseSsl { get; set; } = false;
        
        /// <summary>Abandonner si connexion échoue au démarrage</summary>
        public bool AbortOnConnectFail { get; set; } = false;
        
        /// <summary>Active Redis pour le cache distribué</summary>
        public bool EnableDistributedCache { get; set; } = false;
        
        /// <summary>Active Redis comme backplane SignalR</summary>
        public bool EnableSignalRBackplane { get; set; } = false;

        /// <summary>
        /// Construit la chaîne de connexion complète pour StackExchange.Redis
        /// </summary>
        public string GetFullConnectionString()
        {
            var parts = new List<string> { ConnectionString };
            
            if (!string.IsNullOrEmpty(Password))
                parts.Add($"password={Password}");
            
            parts.Add($"connectTimeout={ConnectTimeoutMs}");
            parts.Add($"syncTimeout={SyncTimeoutMs}");
            parts.Add($"abortConnect={AbortOnConnectFail.ToString().ToLower()}");
            parts.Add($"defaultDatabase={Database}");
            
            if (UseSsl)
                parts.Add("ssl=true");
            
            return string.Join(",", parts);
        }
    }
}

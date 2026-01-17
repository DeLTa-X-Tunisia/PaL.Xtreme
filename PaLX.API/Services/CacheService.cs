// ============================================================================
// PaL.Xtreme - Modern Instant Messaging Solution
// Copyright © 2026 Azizi Mounir. All Rights Reserved.
// ============================================================================

using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace PaLX.API.Services
{
    /// <summary>
    /// Service de cache multi-niveau (L1: Memory, L2: Redis/Distributed)
    /// Améliore drastiquement les performances en réduisant les appels DB
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Récupère une valeur du cache, ou exécute la factory si absente
        /// </summary>
        Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, CacheOptions? options = null);
        
        /// <summary>
        /// Récupère une valeur du cache sans factory
        /// </summary>
        Task<T?> GetAsync<T>(string key);
        
        /// <summary>
        /// Définit une valeur dans le cache
        /// </summary>
        Task SetAsync<T>(string key, T value, CacheOptions? options = null);
        
        /// <summary>
        /// Supprime une entrée du cache
        /// </summary>
        Task RemoveAsync(string key);
        
        /// <summary>
        /// Supprime toutes les entrées correspondant à un pattern
        /// </summary>
        Task RemoveByPatternAsync(string pattern);
        
        /// <summary>
        /// Invalide le cache pour une entité spécifique
        /// </summary>
        Task InvalidateEntityAsync(string entityType, string entityId);
    }

    /// <summary>
    /// Options de configuration du cache
    /// </summary>
    public class CacheOptions
    {
        /// <summary>TTL du cache L1 (mémoire locale) - défaut 30 secondes</summary>
        public TimeSpan L1Expiration { get; set; } = TimeSpan.FromSeconds(30);
        
        /// <summary>TTL du cache L2 (Redis) - défaut 5 minutes</summary>
        public TimeSpan L2Expiration { get; set; } = TimeSpan.FromMinutes(5);
        
        /// <summary>Sliding expiration - renouvelle le TTL à chaque accès</summary>
        public bool UseSlidingExpiration { get; set; } = false;

        // Presets pour différents types de données
        public static CacheOptions UserProfile => new() 
        { 
            L1Expiration = TimeSpan.FromSeconds(30), 
            L2Expiration = TimeSpan.FromMinutes(5) 
        };
        
        public static CacheOptions RoomList => new() 
        { 
            L1Expiration = TimeSpan.FromSeconds(10), 
            L2Expiration = TimeSpan.FromSeconds(30) 
        };
        
        public static CacheOptions RoomMembers => new() 
        { 
            L1Expiration = TimeSpan.FromSeconds(5), 
            L2Expiration = TimeSpan.FromSeconds(15) 
        };
        
        public static CacheOptions OnlineStatus => new() 
        { 
            L1Expiration = TimeSpan.FromSeconds(5), 
            L2Expiration = TimeSpan.FromSeconds(10) 
        };
        
        public static CacheOptions Subscription => new() 
        { 
            L1Expiration = TimeSpan.FromMinutes(5), 
            L2Expiration = TimeSpan.FromHours(1) 
        };
        
        public static CacheOptions Messages => new() 
        { 
            L1Expiration = TimeSpan.FromMinutes(1), 
            L2Expiration = TimeSpan.FromMinutes(30),
            UseSlidingExpiration = true
        };
    }

    /// <summary>
    /// Implémentation du service de cache multi-niveau
    /// L1: MemoryCache (ultra rapide, local au process)
    /// L2: IDistributedCache (Redis en prod, Memory en dev)
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _l1Cache;
        private readonly IDistributedCache _l2Cache;
        private readonly ILogger<CacheService> _logger;
        private readonly SemaphoreSlim _lockSlim = new(1, 1);
        
        // Préfixe pour les clés de cache (évite les collisions)
        private const string KEY_PREFIX = "palx:";

        public CacheService(
            IMemoryCache memoryCache, 
            IDistributedCache distributedCache,
            ILogger<CacheService> logger)
        {
            _l1Cache = memoryCache;
            _l2Cache = distributedCache;
            _logger = logger;
        }

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, CacheOptions? options = null)
        {
            options ??= new CacheOptions();
            var fullKey = KEY_PREFIX + key;

            // L1: Try memory cache first (fastest)
            if (_l1Cache.TryGetValue(fullKey, out T? l1Value))
            {
                _logger.LogDebug("Cache L1 HIT: {Key}", key);
                return l1Value;
            }

            // L2: Try distributed cache (Redis)
            try
            {
                var l2Bytes = await _l2Cache.GetAsync(fullKey);
                if (l2Bytes != null)
                {
                    var l2Value = JsonSerializer.Deserialize<T>(l2Bytes);
                    
                    // Populate L1 cache
                    SetL1Cache(fullKey, l2Value, options.L1Expiration);
                    
                    _logger.LogDebug("Cache L2 HIT: {Key}", key);
                    return l2Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache L2 error for key {Key}, falling back to factory", key);
            }

            // L3: Execute factory (database call)
            _logger.LogDebug("Cache MISS: {Key}, calling factory", key);
            
            // Use lock to prevent cache stampede (thundering herd)
            await _lockSlim.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (_l1Cache.TryGetValue(fullKey, out l1Value))
                    return l1Value;

                var value = await factory();
                
                if (value != null)
                {
                    await SetAsync(key, value, options);
                }
                
                return value;
            }
            finally
            {
                _lockSlim.Release();
            }
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var fullKey = KEY_PREFIX + key;

            // L1 first
            if (_l1Cache.TryGetValue(fullKey, out T? value))
                return value;

            // L2
            try
            {
                var bytes = await _l2Cache.GetAsync(fullKey);
                if (bytes != null)
                {
                    return JsonSerializer.Deserialize<T>(bytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache L2 GET error for key {Key}", key);
            }

            return default;
        }

        public async Task SetAsync<T>(string key, T value, CacheOptions? options = null)
        {
            options ??= new CacheOptions();
            var fullKey = KEY_PREFIX + key;

            // Set L1 (memory)
            SetL1Cache(fullKey, value, options.L1Expiration);

            // Set L2 (distributed)
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
                var distributedOptions = new DistributedCacheEntryOptions();
                
                if (options.UseSlidingExpiration)
                    distributedOptions.SlidingExpiration = options.L2Expiration;
                else
                    distributedOptions.AbsoluteExpirationRelativeToNow = options.L2Expiration;

                await _l2Cache.SetAsync(fullKey, bytes, distributedOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache L2 SET error for key {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            var fullKey = KEY_PREFIX + key;
            
            _l1Cache.Remove(fullKey);
            
            try
            {
                await _l2Cache.RemoveAsync(fullKey);
                _logger.LogDebug("Cache removed: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache L2 REMOVE error for key {Key}", key);
            }
        }

        public Task RemoveByPatternAsync(string pattern)
        {
            // Note: Pattern removal nécessite Redis SCAN, simplifié ici
            // En production avec Redis, utiliser StackExchange.Redis directement
            _logger.LogInformation("Cache pattern invalidation requested: {Pattern}", pattern);
            return Task.CompletedTask;
        }

        public async Task InvalidateEntityAsync(string entityType, string entityId)
        {
            // Invalidation intelligente par type d'entité
            var keysToInvalidate = entityType.ToLower() switch
            {
                "user" => new[]
                {
                    $"user:{entityId}",
                    $"user:profile:{entityId}",
                    $"user:subscription:{entityId}",
                    $"user:friends:{entityId}"
                },
                "room" => new[]
                {
                    $"room:{entityId}",
                    $"room:members:{entityId}",
                    $"room:settings:{entityId}",
                    "rooms:list" // Invalidate room list cache
                },
                "subscription" => new[]
                {
                    $"subscription:{entityId}",
                    $"user:subscription:{entityId}"
                },
                _ => Array.Empty<string>()
            };

            foreach (var key in keysToInvalidate)
            {
                await RemoveAsync(key);
            }
            
            _logger.LogInformation("Invalidated cache for {EntityType}:{EntityId}", entityType, entityId);
        }

        private void SetL1Cache<T>(string key, T value, TimeSpan expiration)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration,
                Size = 1 // Pour le tracking de taille du cache
            };
            
            _l1Cache.Set(key, value, cacheOptions);
        }
    }

    /// <summary>
    /// Clés de cache standardisées pour éviter les erreurs
    /// </summary>
    public static class CacheKeys
    {
        public static string User(int userId) => $"user:{userId}";
        public static string User(string username) => $"user:name:{username}";
        public static string UserProfile(int userId) => $"user:profile:{userId}";
        public static string UserSubscription(int userId) => $"user:subscription:{userId}";
        public static string UserFriends(int userId) => $"user:friends:{userId}";
        public static string UserOnline(int userId) => $"user:online:{userId}";
        
        public static string Room(int roomId) => $"room:{roomId}";
        public static string RoomMembers(int roomId) => $"room:members:{roomId}";
        public static string RoomSettings(int roomId) => $"room:settings:{roomId}";
        public static string RoomsList => "rooms:list";
        public static string RoomsCategory(int categoryId) => $"rooms:category:{categoryId}";
        
        public static string Subscription(int userId) => $"subscription:{userId}";
        public static string SubscriptionTiers => "subscription:tiers";
        public static string RoomSubscriptionTiers => "subscription:room:tiers";
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaLX.API.Services;
using System.Security.Claims;

namespace PaLX.API.Controllers
{
    [ApiController]
    [Route("api/admin/subscriptions")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(ISubscriptionService subscriptionService, ILogger<SubscriptionController> logger)
        {
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value 
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }

        private bool IsSystemAdmin()
        {
            var roleLevelClaim = User.FindFirst("RoleLevel")?.Value;
            if (int.TryParse(roleLevelClaim, out var level))
                return level <= 4; // Master(1), Editor(2), SuperAdmin(3), Admin(4)
            return false;
        }

        // ============================================
        // TIERS
        // ============================================

        /// <summary>
        /// Récupère tous les tiers d'abonnement
        /// </summary>
        [HttpGet("tiers")]
        public async Task<IActionResult> GetTiers()
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var tiers = await _subscriptionService.GetTiersAsync();
            return Ok(tiers);
        }

        /// <summary>
        /// Récupère un tier par son ID
        /// </summary>
        [HttpGet("tiers/{id}")]
        public async Task<IActionResult> GetTier(int id)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var tier = await _subscriptionService.GetTierByIdAsync(id);
            if (tier == null)
                return NotFound(new { message = "Tier non trouvé" });

            return Ok(tier);
        }

        /// <summary>
        /// Met à jour un tier (prix, fonctionnalités, etc.)
        /// </summary>
        [HttpPut("tiers/{id}")]
        public async Task<IActionResult> UpdateTier(int id, [FromBody] UpdateTierDto dto)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var result = await _subscriptionService.UpdateTierAsync(id, dto);
            if (!result.Success)
                return NotFound(new { message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        // ============================================
        // DURÉES
        // ============================================

        /// <summary>
        /// Récupère toutes les durées d'abonnement
        /// </summary>
        [HttpGet("durations")]
        public async Task<IActionResult> GetDurations()
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var durations = await _subscriptionService.GetDurationsAsync();
            return Ok(durations);
        }

        /// <summary>
        /// Met à jour une durée (bonus jours, remise, etc.)
        /// </summary>
        [HttpPut("durations/{id}")]
        public async Task<IActionResult> UpdateDuration(int id, [FromBody] UpdateDurationDto dto)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var result = await _subscriptionService.UpdateDurationAsync(id, dto);
            if (!result.Success)
                return NotFound(new { message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        // ============================================
        // PRIX
        // ============================================

        /// <summary>
        /// Récupère tous les prix (calculés et personnalisés)
        /// </summary>
        [HttpGet("prices")]
        public async Task<IActionResult> GetAllPrices()
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var prices = await _subscriptionService.GetAllPricesAsync();
            return Ok(prices);
        }

        /// <summary>
        /// Récupère le prix d'une combinaison tier/durée
        /// </summary>
        [HttpGet("prices/{tierId}/{durationId}")]
        public async Task<IActionResult> GetPrice(int tierId, int durationId)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var price = await _subscriptionService.GetPriceAsync(tierId, durationId);
            if (price == null)
                return NotFound(new { message = "Combinaison non trouvée" });

            return Ok(price);
        }

        /// <summary>
        /// Définit un prix personnalisé
        /// </summary>
        [HttpPut("prices/{tierId}/{durationId}")]
        public async Task<IActionResult> SetCustomPrice(int tierId, int durationId, [FromBody] SetPriceRequest request)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var result = await _subscriptionService.SetCustomPriceAsync(tierId, durationId, request.PriceCents, request.Points);
            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Réinitialise au prix calculé automatiquement
        /// </summary>
        [HttpDelete("prices/{tierId}/{durationId}")]
        public async Task<IActionResult> ResetPrice(int tierId, int durationId)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var result = await _subscriptionService.ResetToCalculatedPriceAsync(tierId, durationId);
            return Ok(new { success = true, message = result.Message });
        }

        // ============================================
        // ABONNEMENTS UTILISATEUR
        // ============================================

        /// <summary>
        /// Liste les abonnements utilisateur
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetUserSubscriptions(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20,
            [FromQuery] int? tierId = null,
            [FromQuery] bool? isActive = null)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var subscriptions = await _subscriptionService.GetUserSubscriptionsAsync(page, pageSize, tierId, isActive);
            return Ok(subscriptions);
        }

        /// <summary>
        /// Récupère l'abonnement actif d'un utilisateur
        /// </summary>
        [HttpGet("users/{userId}/current")]
        public async Task<IActionResult> GetUserCurrentSubscription(int userId)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var subscription = await _subscriptionService.GetUserCurrentSubscriptionAsync(userId);
            return Ok(subscription); // Peut être null si pas d'abonnement actif
        }

        /// <summary>
        /// Attribue un abonnement à un utilisateur
        /// </summary>
        [HttpPost("users/{userId}/grant")]
        public async Task<IActionResult> GrantSubscription(int userId, [FromBody] GrantSubscriptionRequest request)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var result = await _subscriptionService.GrantSubscriptionAsync(
                userId, 
                request.TierId, 
                request.DurationId, 
                GetCurrentUserId(),
                request.PaymentMethod);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Révoque l'abonnement d'un utilisateur
        /// </summary>
        [HttpPost("users/{userId}/revoke")]
        public async Task<IActionResult> RevokeSubscription(int userId, [FromBody] RevokeRequest? request)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var result = await _subscriptionService.RevokeSubscriptionAsync(
                userId, 
                GetCurrentUserId(),
                request?.Reason ?? "Révoqué par admin");

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Prolonge l'abonnement d'un utilisateur
        /// </summary>
        [HttpPost("users/{userId}/extend")]
        public async Task<IActionResult> ExtendSubscription(int userId, [FromBody] ExtendRequest request)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var result = await _subscriptionService.ExtendSubscriptionAsync(userId, request.Days, GetCurrentUserId());

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        // ============================================
        // POINTS
        // ============================================

        /// <summary>
        /// Récupère le solde de points d'un utilisateur
        /// </summary>
        [HttpGet("users/{userId}/points")]
        public async Task<IActionResult> GetUserPoints(int userId)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var points = await _subscriptionService.GetUserPointsAsync(userId);
            return Ok(points);
        }

        /// <summary>
        /// Ajoute des points à un utilisateur
        /// </summary>
        [HttpPost("users/{userId}/points/grant")]
        public async Task<IActionResult> GrantPoints(int userId, [FromBody] GrantPointsRequest request)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var result = await _subscriptionService.GrantPointsAsync(
                userId, 
                request.Amount, 
                request.Description ?? "Points offerts par admin",
                GetCurrentUserId());

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        /// <summary>
        /// Historique des transactions de points
        /// </summary>
        [HttpGet("users/{userId}/points/history")]
        public async Task<IActionResult> GetPointHistory(int userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var history = await _subscriptionService.GetPointHistoryAsync(userId, page, pageSize);
            return Ok(history);
        }

        // ============================================
        // PÉRIODE D'ESSAI
        // ============================================

        /// <summary>
        /// Vérifie si un utilisateur peut utiliser la période d'essai
        /// </summary>
        [HttpGet("users/{userId}/trial/{tierId}/available")]
        public async Task<IActionResult> CanUseTrial(int userId, int tierId)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var canUse = await _subscriptionService.CanUseTrialAsync(userId, tierId);
            return Ok(new { canUseTrial = canUse });
        }

        /// <summary>
        /// Active la période d'essai pour un utilisateur
        /// </summary>
        [HttpPost("users/{userId}/trial/{tierId}/activate")]
        public async Task<IActionResult> ActivateTrial(int userId, int tierId)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var result = await _subscriptionService.ActivateTrialAsync(userId, tierId);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { success = true, message = result.Message });
        }

        // ============================================
        // STATISTIQUES
        // ============================================

        /// <summary>
        /// Récupère les statistiques des abonnements
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs");

            var stats = await _subscriptionService.GetStatsAsync();
            return Ok(stats);
        }
    }

    // DTOs supplémentaires pour les requêtes
    public class RevokeRequest
    {
        public string? Reason { get; set; }
    }

    public class ExtendRequest
    {
        public int Days { get; set; }
    }

    public class GrantSubscriptionRequest
    {
        public int TierId { get; set; }
        public int DurationId { get; set; }
        public string PaymentMethod { get; set; } = "Manual";
    }

    public class GrantPointsRequest
    {
        public int Amount { get; set; }
        public string? Description { get; set; }
    }

    public class SetPriceRequest
    {
        public int PriceCents { get; set; }
        public int Points { get; set; }
    }
}

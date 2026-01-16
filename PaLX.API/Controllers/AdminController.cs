using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PaLX.API.DTOs;
using PaLX.API.Hubs;
using PaLX.API.Services;
using System.Security.Claims;

namespace PaLX.API.Controllers
{
    /// <summary>
    /// Contrôleur d'administration pour le panel PaL.X.Admin
    /// Requiert un RoleLevel <= 6 (System Admin)
    /// </summary>
    [Route("api/admin")]
    [ApiController]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IHubContext<ChatHub> _chatHubContext;
        private readonly IHubContext<RoomHub> _roomHubContext;

        public AdminController(
            IAdminService adminService, 
            IHubContext<ChatHub> chatHubContext,
            IHubContext<RoomHub> roomHubContext)
        {
            _adminService = adminService;
            _chatHubContext = chatHubContext;
            _roomHubContext = roomHubContext;
        }

        private int GetCurrentUserId() => int.Parse(User.FindFirst(Constants.JwtClaims.UserId)?.Value ?? "0");
        private int GetCurrentRoleLevel() => int.Parse(User.FindFirst(Constants.JwtClaims.RoleLevel)?.Value ?? "0");
        private string GetCurrentUsername() => User.FindFirst(Constants.JwtClaims.Username)?.Value ?? "";

        private bool IsSystemAdmin() => Constants.RoleLevels.IsSystemAdmin(GetCurrentRoleLevel());

        // ============================================
        // Dashboard
        // ============================================

        /// <summary>
        /// Récupère les statistiques du dashboard
        /// </summary>
        [HttpGet("dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var stats = await _adminService.GetDashboardStatsAsync();
            return Ok(stats);
        }

        // ============================================
        // Roles Management
        // ============================================

        /// <summary>
        /// Liste tous les rôles avec leurs informations
        /// </summary>
        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var roles = await _adminService.GetRolesAsync();
            return Ok(roles);
        }

        // ============================================
        // Global Broadcast / Annonces
        // ============================================

        /// <summary>
        /// Envoie une annonce globale à tous les salons et utilisateurs connectés
        /// </summary>
        [HttpPost("broadcast")]
        public async Task<IActionResult> BroadcastAnnouncement([FromBody] BroadcastRequest request)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Le message ne peut pas être vide");

            var announcement = new
            {
                Type = request.Type ?? "info",
                Title = request.Title ?? "Annonce",
                Message = request.Message,
                SentBy = GetCurrentUsername(),
                SentByDisplayName = User.FindFirst("DisplayName")?.Value ?? GetCurrentUsername(),
                Timestamp = DateTime.UtcNow
            };

            // Envoyer à tous les clients connectés au ChatHub (messages privés)
            await _chatHubContext.Clients.All.SendAsync("ReceiveGlobalAnnouncement", announcement);
            
            // Envoyer à tous les clients connectés au RoomHub (salons)
            await _roomHubContext.Clients.All.SendAsync("ReceiveGlobalAnnouncement", announcement);

            // Enregistrer l'annonce dans la base de données
            await _adminService.SaveBroadcastAsync(
                GetCurrentUserId(), 
                request.Type ?? "info", 
                request.Title ?? "Annonce", 
                request.Message
            );

            return Ok(new { 
                success = true, 
                message = "Annonce envoyée avec succès",
                announcement 
            });
        }

        /// <summary>
        /// Récupère l'historique des annonces envoyées
        /// </summary>
        [HttpGet("broadcasts")]
        public async Task<IActionResult> GetBroadcastHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var history = await _adminService.GetBroadcastHistoryAsync(page, pageSize);
            return Ok(history);
        }

        // ============================================
        // Categories Management
        // ============================================

        /// <summary>
        /// Liste toutes les catégories
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var categories = await _adminService.GetCategoriesAsync();
            return Ok(categories);
        }

        /// <summary>
        /// Récupère une catégorie par ID
        /// </summary>
        [HttpGet("categories/{id}")]
        public async Task<IActionResult> GetCategoryById(int id)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var category = await _adminService.GetCategoryByIdAsync(id);
            if (category == null)
                return NotFound("Catégorie non trouvée");
            return Ok(category);
        }

        /// <summary>
        /// Crée une nouvelle catégorie
        /// </summary>
        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Le nom est requis");

            var result = await _adminService.CreateCategoryAsync(dto, GetCurrentUserId());
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        /// <summary>
        /// Met à jour une catégorie
        /// </summary>
        [HttpPut("categories/{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryDto dto)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var result = await _adminService.UpdateCategoryAsync(id, dto, GetCurrentUserId());
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        /// <summary>
        /// Supprime une catégorie
        /// </summary>
        [HttpDelete("categories/{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var result = await _adminService.DeleteCategoryAsync(id, GetCurrentUserId());
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        // ============================================
        // SubCategories Management
        // ============================================

        /// <summary>
        /// Liste toutes les sous-catégories (optionnellement filtrées par catégorie)
        /// </summary>
        [HttpGet("subcategories")]
        public async Task<IActionResult> GetSubCategories([FromQuery] int? categoryId = null)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var subCategories = await _adminService.GetSubCategoriesAsync(categoryId);
            return Ok(subCategories);
        }

        /// <summary>
        /// Récupère une sous-catégorie par ID
        /// </summary>
        [HttpGet("subcategories/{id}")]
        public async Task<IActionResult> GetSubCategoryById(int id)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var subCategory = await _adminService.GetSubCategoryByIdAsync(id);
            if (subCategory == null)
                return NotFound("Sous-catégorie non trouvée");
            return Ok(subCategory);
        }

        /// <summary>
        /// Crée une nouvelle sous-catégorie
        /// </summary>
        [HttpPost("subcategories")]
        public async Task<IActionResult> CreateSubCategory([FromBody] CreateSubCategoryDto dto)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Le nom est requis");

            var result = await _adminService.CreateSubCategoryAsync(dto, GetCurrentUserId());
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        /// <summary>
        /// Met à jour une sous-catégorie
        /// </summary>
        [HttpPut("subcategories/{id}")]
        public async Task<IActionResult> UpdateSubCategory(int id, [FromBody] UpdateSubCategoryDto dto)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var result = await _adminService.UpdateSubCategoryAsync(id, dto, GetCurrentUserId());
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        /// <summary>
        /// Supprime une sous-catégorie
        /// </summary>
        [HttpDelete("subcategories/{id}")]
        public async Task<IActionResult> DeleteSubCategory(int id)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var result = await _adminService.DeleteSubCategoryAsync(id, GetCurrentUserId());
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = result.Message });
        }

        // ============================================
        // Users Management
        // ============================================

        /// <summary>
        /// Liste les utilisateurs avec pagination et filtres
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] int? roleLevel = null,
            [FromQuery] bool? isOnline = null,
            [FromQuery] bool? isBanned = null)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var result = await _adminService.GetUsersAsync(page, pageSize, search, roleLevel, isOnline, isBanned);
            return Ok(result);
        }

        /// <summary>
        /// Récupère les détails d'un utilisateur
        /// </summary>
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var user = await _adminService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "Utilisateur non trouvé" });

            return Ok(user);
        }

        /// <summary>
        /// Bannit un utilisateur
        /// </summary>
        [HttpPost("users/{id}/ban")]
        public async Task<IActionResult> BanUser(int id, [FromBody] AdminBanUserDto dto)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var adminId = GetCurrentUserId();
            var adminUsername = GetCurrentUsername();
            var adminRoleLevel = GetCurrentRoleLevel();

            var result = await _adminService.BanUserAsync(id, dto.Reason, dto.DurationDays, adminId, adminRoleLevel);
            
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            // Notifier via SignalR
            await _chatHubContext.Clients.All.SendAsync("UserBanned", id, dto.Reason);
            
            // Déconnecter l'utilisateur
            var targetUsername = await _adminService.GetUsernameByIdAsync(id);
            if (!string.IsNullOrEmpty(targetUsername))
            {
                await _chatHubContext.Clients.User(targetUsername).SendAsync("ForceDisconnect", 
                    $"Vous avez été banni. Raison: {dto.Reason}");
            }

            return Ok(new { message = "Utilisateur banni avec succès" });
        }

        /// <summary>
        /// Débannit un utilisateur
        /// </summary>
        [HttpPost("users/{id}/unban")]
        public async Task<IActionResult> UnbanUser(int id)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var adminId = GetCurrentUserId();
            var result = await _adminService.UnbanUserAsync(id, adminId);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = "Utilisateur débanni avec succès" });
        }

        /// <summary>
        /// Change le rôle d'un utilisateur
        /// </summary>
        [HttpPost("users/{id}/role")]
        public async Task<IActionResult> ChangeUserRole(int id, [FromBody] AdminChangeRoleDto dto)
        {
            var adminRoleLevel = GetCurrentRoleLevel();
            
            // Seuls les Master (1) et Editor (2) peuvent changer les rôles
            if (adminRoleLevel > 2)
                return Forbid("Seuls les Master et Editor peuvent modifier les rôles");

            var result = await _adminService.ChangeUserRoleAsync(id, dto.NewRoleLevel, GetCurrentUserId());

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = "Rôle modifié avec succès" });
        }

        /// <summary>
        /// Envoie un avertissement à un utilisateur
        /// </summary>
        [HttpPost("users/{id}/warn")]
        public async Task<IActionResult> WarnUser(int id, [FromBody] AdminWarnUserDto dto)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var adminId = GetCurrentUserId();
            var result = await _adminService.WarnUserAsync(id, dto.Reason, adminId);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            // Notifier l'utilisateur via SignalR
            var targetUsername = await _adminService.GetUsernameByIdAsync(id);
            if (!string.IsNullOrEmpty(targetUsername))
            {
                await _chatHubContext.Clients.User(targetUsername).SendAsync("AdminWarning", dto.Reason);
            }

            return Ok(new { message = "Avertissement envoyé" });
        }

        // ============================================
        // Rooms Management
        // ============================================

        /// <summary>
        /// Liste les salons avec pagination et filtres
        /// </summary>
        [HttpGet("rooms")]
        public async Task<IActionResult> GetRooms(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] bool? isActive = null)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var result = await _adminService.GetRoomsAsync(page, pageSize, search, isActive);
            return Ok(result);
        }

        /// <summary>
        /// Ferme un salon
        /// </summary>
        [HttpPost("rooms/{id}/close")]
        public async Task<IActionResult> CloseRoom(int id, [FromBody] AdminCloseRoomDto? dto)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var result = await _adminService.CloseRoomAsync(id, dto?.Reason, GetCurrentUserId());

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            // Notifier via SignalR
            await _chatHubContext.Clients.Group($"room_{id}").SendAsync("RoomClosed", dto?.Reason ?? "Fermé par un administrateur");

            return Ok(new { message = "Salon fermé" });
        }

        /// <summary>
        /// Supprime un salon
        /// </summary>
        [HttpDelete("rooms/{id}")]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            var adminRoleLevel = GetCurrentRoleLevel();
            
            // Seuls les Master (1), Editor (2), SuperAdmin (3) peuvent supprimer
            if (adminRoleLevel > 3)
                return Forbid("Permissions insuffisantes pour supprimer un salon");

            var result = await _adminService.DeleteRoomAsync(id, GetCurrentUserId());

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = "Salon supprimé" });
        }

        // ============================================
        // Reports / Moderation
        // ============================================

        /// <summary>
        /// Liste les signalements
        /// </summary>
        [HttpGet("reports")]
        public async Task<IActionResult> GetReports(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var result = await _adminService.GetReportsAsync(page, pageSize, status);
            return Ok(result);
        }

        /// <summary>
        /// Résout un signalement
        /// </summary>
        [HttpPost("reports/{id}/resolve")]
        public async Task<IActionResult> ResolveReport(int id, [FromBody] AdminResolveReportDto dto)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var result = await _adminService.ResolveReportAsync(id, dto.Resolution, dto.Action, GetCurrentUserId());

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = "Signalement résolu" });
        }

        /// <summary>
        /// Rejette un signalement
        /// </summary>
        [HttpPost("reports/{id}/dismiss")]
        public async Task<IActionResult> DismissReport(int id, [FromBody] AdminDismissReportDto? dto)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var result = await _adminService.DismissReportAsync(id, dto?.Reason, GetCurrentUserId());

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(new { message = "Signalement rejeté" });
        }

        // ============================================
        // Logs
        // ============================================

        /// <summary>
        /// Récupère les logs d'audit
        /// </summary>
        [HttpGet("logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (!IsSystemAdmin())
                return Forbid("Accès réservé aux administrateurs système");

            var result = await _adminService.GetAuditLogsAsync(page, pageSize);
            return Ok(result);
        }

        // ============================================
        // System
        // ============================================

        /// <summary>
        /// Envoie un message broadcast à tous les utilisateurs
        /// </summary>
        [HttpPost("system/broadcast")]
        public async Task<IActionResult> SendBroadcast([FromBody] AdminBroadcastDto dto)
        {
            var adminRoleLevel = GetCurrentRoleLevel();
            
            // Seuls les Master (1), Editor (2), SuperAdmin (3), Admin (4) peuvent broadcast
            if (adminRoleLevel > 4)
                return Forbid("Permissions insuffisantes pour envoyer un broadcast");

            await _chatHubContext.Clients.All.SendAsync("BroadcastMessage", dto.Message);
            
            // Logger l'action
            await _adminService.LogActionAsync(GetCurrentUserId(), "Broadcast", "System", null, dto.Message);

            return Ok(new { message = "Broadcast envoyé" });
        }

        /// <summary>
        /// Active/Désactive le mode maintenance
        /// </summary>
        [HttpPost("system/maintenance")]
        public async Task<IActionResult> SetMaintenanceMode([FromBody] AdminMaintenanceDto dto)
        {
            var adminRoleLevel = GetCurrentRoleLevel();
            
            // Seuls les Master (1) et Editor (2) peuvent activer la maintenance
            if (adminRoleLevel > 2)
                return Forbid("Seuls les Master et Editor peuvent gérer la maintenance");

            await _adminService.SetMaintenanceModeAsync(dto.Enabled, dto.Message);

            if (dto.Enabled)
            {
                await _chatHubContext.Clients.All.SendAsync("MaintenanceMode", dto.Message ?? "Maintenance en cours...");
            }

            return Ok(new { message = dto.Enabled ? "Mode maintenance activé" : "Mode maintenance désactivé" });
        }
    }
}

// DTOs pour AdminController
namespace PaLX.API.DTOs
{
    public class AdminBanUserDto
    {
        public string Reason { get; set; } = "";
        public int? DurationDays { get; set; }
    }

    public class AdminChangeRoleDto
    {
        public int NewRoleLevel { get; set; }
    }

    public class AdminWarnUserDto
    {
        public string Reason { get; set; } = "";
    }

    public class AdminCloseRoomDto
    {
        public string? Reason { get; set; }
    }

    public class AdminResolveReportDto
    {
        public string Resolution { get; set; } = "";
        public string? Action { get; set; }
    }

    public class AdminDismissReportDto
    {
        public string? Reason { get; set; }
    }

    public class AdminBroadcastDto
    {
        public string Message { get; set; } = "";
    }

    public class AdminMaintenanceDto
    {
        public bool Enabled { get; set; }
        public string? Message { get; set; }
    }
}

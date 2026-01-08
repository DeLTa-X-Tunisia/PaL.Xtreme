using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaLX.API.DTOs;
using PaLX.API.Services;
using System.Security.Claims;

namespace PaLX.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RoomController : ControllerBase
    {
        private readonly IRoomService _roomService;

        public RoomController(IRoomService roomService)
        {
            _roomService = roomService;
        }

        private int GetUserId()
        {
            var claim = User.FindFirst("UserId");
            if (claim == null || !int.TryParse(claim.Value, out int userId))
            {
                throw new UnauthorizedAccessException("User ID not found in token.");
            }
            return userId;
        }

        [HttpPost]
        public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto dto)
        {
            try
            {
                var userId = GetUserId();
                var room = await _roomService.CreateRoomAsync(userId, dto);
                return Ok(room);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRooms([FromQuery] int? categoryId)
        {
            var userId = GetUserId();
            var rooms = await _roomService.GetRoomsAsync(userId, categoryId);
            return Ok(rooms);
        }

        [HttpPost("{roomId}/join")]
        public async Task<IActionResult> JoinRoom(int roomId, [FromBody] JoinRoomDto dto)
        {
            var userId = GetUserId();
            var success = await _roomService.JoinRoomAsync(userId, roomId, dto.Password);
            if (!success) return BadRequest(new { message = "Cannot join room (Invalid password, full, or not found)." });
            return Ok(new { message = "Joined successfully" });
        }

        [HttpPost("{roomId}/leave")]
        public async Task<IActionResult> LeaveRoom(int roomId)
        {
            var userId = GetUserId();
            await _roomService.LeaveRoomAsync(userId, roomId);
            return Ok(new { message = "Left room" });
        }

        [HttpGet("{roomId}/members")]
        public async Task<IActionResult> GetMembers(int roomId)
        {
            var members = await _roomService.GetRoomMembersAsync(roomId);
            return Ok(members);
        }

        [HttpGet("{roomId}/messages")]
        public async Task<IActionResult> GetMessages(int roomId, [FromQuery] int limit = 50)
        {
            var messages = await _roomService.GetRoomMessagesAsync(roomId, limit);
            return Ok(messages);
        }

        [HttpPost("{roomId}/messages")]
        public async Task<IActionResult> SendMessage(int roomId, [FromBody] SendMessageDto dto)
        {
            var userId = GetUserId();
            var message = await _roomService.SendMessageAsync(userId, roomId, dto.Content, dto.Type, dto.AttachmentUrl);
            return Ok(message);
        }

        [HttpPut("{roomId}/status")]
        public async Task<IActionResult> UpdateStatus(int roomId, [FromBody] UpdateStatusDto dto)
        {
            var userId = GetUserId();
            var success = await _roomService.UpdateMemberStatusAsync(userId, roomId, dto.IsCamOn, dto.IsMicOn, dto.HasHandRaised);
            return Ok(new { success });
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _roomService.GetCategoriesAsync();
            return Ok(categories);
        }

        [HttpGet("categories/{categoryId}/subcategories")]
        public async Task<IActionResult> GetSubCategories(int categoryId)
        {
            var subCategories = await _roomService.GetSubCategoriesAsync(categoryId);
            return Ok(subCategories);
        }

        [HttpGet("subscription-tiers")]
        public async Task<IActionResult> GetSubscriptionTiers()
        {
            var tiers = await _roomService.GetRoomSubscriptionTiersAsync();
            return Ok(tiers);
        }

        [HttpGet("my-rooms")]
        public async Task<IActionResult> GetMyRooms()
        {
            var userId = GetUserId();
            var rooms = await _roomService.GetMyRoomsAsync(userId);
            return Ok(rooms);
        }

        [HttpGet("{roomId}/subscription")]
        public async Task<IActionResult> GetRoomSubscription(int roomId)
        {
            var subscription = await _roomService.GetRoomSubscriptionAsync(roomId);
            if (subscription == null) return NotFound();
            return Ok(subscription);
        }

        [HttpPost("{roomId}/upgrade")]
        public async Task<IActionResult> UpgradeRoom(int roomId, [FromBody] UpgradeRoomSubscriptionDto dto)
        {
            try
            {
                var userId = GetUserId();
                var success = await _roomService.UpgradeRoomSubscriptionAsync(userId, roomId, dto.NewTierLevel, dto.TransactionId, dto.PaymentMethod);
                return Ok(new { success });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Only room owner can upgrade" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{roomId}")]
        public async Task<IActionResult> DeleteRoom(int roomId)
        {
            try
            {
                var userId = GetUserId();
                await _roomService.DeleteRoomAsync(userId, roomId);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{roomId}")]
        public async Task<IActionResult> UpdateRoom(int roomId, [FromBody] CreateRoomDto dto)
        {
            try
            {
                var userId = GetUserId();
                var room = await _roomService.UpdateRoomAsync(userId, roomId, dto);
                return Ok(room);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Not owner" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{roomId}/toggle-visibility")]
        public async Task<IActionResult> ToggleVisibility(int roomId)
        {
            try
            {
                var userId = GetUserId();
                var isActive = await _roomService.ToggleRoomVisibilityAsync(userId, roomId);
                return Ok(new { isActive });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Not owner" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Toggle le statut IsSystemHidden d'un salon (admin système uniquement).
        /// Quand TRUE, même le RoomOwner ne voit plus son salon.
        /// </summary>
        [HttpPost("{roomId}/toggle-system-hidden")]
        public async Task<IActionResult> ToggleSystemHidden(int roomId)
        {
            try
            {
                var userId = GetUserId();
                var isSystemHidden = await _roomService.ToggleSystemHiddenAsync(userId, roomId);
                return Ok(new { isSystemHidden });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ==================== ROOM ADMINS MANAGEMENT (Simplified) ====================

        /// <summary>
        /// Récupère les admins/modérateurs d'un salon
        /// </summary>
        [HttpGet("{roomId}/roles")]
        public async Task<IActionResult> GetRoomRoles(int roomId)
        {
            try
            {
                var userId = GetUserId();
                var roles = await _roomService.GetRoomRolesAsync(userId, roomId);
                return Ok(roles);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Not owner" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Attribue directement un rôle à un utilisateur
        /// </summary>
        [HttpPost("{roomId}/roles/assign")]
        public async Task<IActionResult> AssignRole(int roomId, [FromBody] RoleRequestDto dto)
        {
            try
            {
                var userId = GetUserId();
                await _roomService.AssignRoleAsync(userId, roomId, dto.UserId, dto.Role);
                return Ok(new { message = "Role assigned successfully" });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Not owner" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retire le rôle d'un utilisateur dans un salon
        /// </summary>
        [HttpDelete("{roomId}/roles/{targetUserId}")]
        public async Task<IActionResult> RemoveRole(int roomId, int targetUserId)
        {
            try
            {
                var userId = GetUserId();
                await _roomService.RemoveRoomRoleAsync(userId, roomId, targetUserId);
                return Ok(new { message = "Role removed" });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Not owner" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class JoinRoomDto
    {
        public string? Password { get; set; }
    }

    public class SendMessageDto
    {
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = "Text";
        public string? AttachmentUrl { get; set; }
    }

    public class UpdateStatusDto
    {
        public bool? IsCamOn { get; set; }
        public bool? IsMicOn { get; set; }
        public bool? HasHandRaised { get; set; }
    }

    public class RoleRequestDto
    {
        public int UserId { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}

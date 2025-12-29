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
}

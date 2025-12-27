using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaLX.API.Models;
using PaLX.API.Services;
using PaLX.API.DTOs;
using System.Security.Claims;

using Microsoft.AspNetCore.SignalR;
using PaLX.API.Hubs;

namespace PaLX.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IHubContext<ChatHub> _hubContext;

        public UserController(IUserService userService, IHubContext<ChatHub> hubContext)
        {
            _userService = userService;
            _hubContext = hubContext;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (model.Password != model.ConfirmPassword)
                return BadRequest(new { message = "Passwords do not match" });

            var success = await _userService.RegisterUserAsync(model.Username, model.Password);
            if (!success)
                return BadRequest(new { message = "Username already taken or error occurred" });

            return Ok(new { message = "Registration successful" });
        }

        [Authorize]
        [HttpGet("profile/{username}")]
        public async Task<IActionResult> GetProfile(string username)
        {
            var profile = await _userService.GetUserProfileAsync(username);
            if (profile == null) return NotFound();
            return Ok(profile);
        }

        [Authorize]
        [HttpPost("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UserProfileDto profile)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var success = await _userService.UpdateUserProfileAsync(username, profile);
            if (!success) return BadRequest();
            return Ok();
        }

        [Authorize]
        [HttpGet("friends")]
        public async Task<IActionResult> GetFriends()
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var friends = await _userService.GetFriendsAsync(username);
            return Ok(friends);
        }

        [Authorize]
        [HttpPost("status")]
        public async Task<IActionResult> UpdateStatus([FromBody] int status)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            await _userService.UpdateStatusAsync(username, status);

            // Broadcast via SignalR
            string statusStr = status switch
            {
                0 => "En ligne",
                1 => "Occupé",
                2 => "Absent",
                3 => "En appel",
                4 => "Ne pas déranger",
                _ => "Hors ligne"
            };
            await _hubContext.Clients.All.SendAsync("UserStatusChanged", username, statusStr);

            return Ok();
        }

        [Authorize]
        [HttpPost("block")]
        public async Task<IActionResult> BlockUser([FromBody] BlockRequestModel model)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            try
            {
                var success = await _userService.BlockUserAsync(username, model);
                return success ? Ok() : BadRequest(new { message = "Erreur lors du blocage (Utilisateur introuvable ou erreur DB)" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erreur interne : {ex.Message}" });
            }
        }

        [Authorize]
        [HttpGet("blocked")]
        public async Task<IActionResult> GetBlockedUsers()
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var blockedUsers = await _userService.GetBlockedUsersAsync(username);
            return Ok(blockedUsers);
        }

        [Authorize]
        [HttpGet("chat/history")]
        public async Task<IActionResult> GetChatHistory([FromQuery] string partner)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var history = await _userService.GetChatHistoryAsync(username, partner);
            return Ok(history);
        }

        [Authorize]
        [HttpPost("chat/read")]
        public async Task<IActionResult> MarkMessagesAsRead([FromBody] string partner)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            await _userService.MarkMessagesAsReadAsync(partner, username);
            return Ok();
        }

        [Authorize]
        [HttpPost("unblock")]
        public async Task<IActionResult> UnblockUser([FromBody] string blockedUser)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var success = await _userService.UnblockUserAsync(username, blockedUser);
            return success ? Ok() : BadRequest();
        }

        [Authorize]
        [HttpPost("removefriend")]
        public async Task<IActionResult> RemoveFriend([FromBody] string friendUsername)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var success = await _userService.RemoveFriendAsync(username, friendUsername);
            
            if (success)
            {
                await _hubContext.Clients.User(friendUsername).SendAsync("FriendRemoved", username);
            }

            return success ? Ok() : BadRequest();
        }

        [Authorize]
        [HttpGet("isblocked")]
        public async Task<IActionResult> IsBlocked([FromQuery] string user1, [FromQuery] string user2)
        {
            var isBlocked = await _userService.IsUserBlockedAsync(user1, user2);
            return Ok(isBlocked);
        }

        [Authorize]
        [HttpGet("requests")]
        public async Task<IActionResult> GetPendingRequests()
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var requests = await _userService.GetPendingRequestsAsync(username);
            return Ok(requests);
        }

        [Authorize]
        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string? query)
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(username)) return Unauthorized();

                // Allow empty query to return all users (or limit to top 20)
                var users = await _userService.SearchUsersAsync(query ?? "", username);
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message, stack = ex.StackTrace });
            }
        }

        [Authorize]
        [HttpPost("request")]
        public async Task<IActionResult> SendFriendRequest([FromBody] string toUser)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var success = await _userService.SendFriendRequestAsync(username, toUser);
            
            if (success)
            {
                await _hubContext.Clients.User(toUser).SendAsync("FriendRequestReceived", username);
            }

            return success ? Ok() : BadRequest();
        }

        [Authorize]
        [HttpPost("respond")]
        public async Task<IActionResult> RespondToRequest([FromBody] FriendResponseModel model)
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var success = await _userService.RespondToFriendRequestAsync(username, model.Requester, model.Response);

            if (success)
            {
                if (model.Response == 1 || model.Response == 2) // Accepted
                {
                    await _hubContext.Clients.User(model.Requester).SendAsync("FriendRequestAccepted", username);
                }
                // We could also handle "Declined" if we wanted to notify the user
            }

            return success ? Ok() : BadRequest();
        }
    }
}
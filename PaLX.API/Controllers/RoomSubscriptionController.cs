using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaLX.API.Services;

namespace PaLX.API.Controllers;

[ApiController]
[Route("api/admin/room-subscriptions")]
[Authorize]
public class RoomSubscriptionController : ControllerBase
{
    private readonly RoomSubscriptionService _roomSubscriptionService;
    private readonly ILogger<RoomSubscriptionController> _logger;

    public RoomSubscriptionController(RoomSubscriptionService roomSubscriptionService, ILogger<RoomSubscriptionController> logger)
    {
        _roomSubscriptionService = roomSubscriptionService;
        _logger = logger;
    }

    // ========================= TIERS =========================

    /// <summary>
    /// Get all room subscription tiers
    /// </summary>
    [HttpGet("tiers")]
    public async Task<IActionResult> GetAllTiers()
    {
        try
        {
            var tiers = await _roomSubscriptionService.GetAllTiersAsync();
            return Ok(tiers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching room subscription tiers");
            return StatusCode(500, new { error = "Failed to fetch room subscription tiers", details = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific room subscription tier by ID
    /// </summary>
    [HttpGet("tiers/{id}")]
    public async Task<IActionResult> GetTierById(int id)
    {
        try
        {
            var tier = await _roomSubscriptionService.GetTierByIdAsync(id);
            if (tier == null)
                return NotFound(new { error = "Room subscription tier not found" });

            return Ok(tier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching room subscription tier {Id}", id);
            return StatusCode(500, new { error = "Failed to fetch room subscription tier", details = ex.Message });
        }
    }

    /// <summary>
    /// Update a room subscription tier
    /// </summary>
    [HttpPut("tiers/{id}")]
    public async Task<IActionResult> UpdateTier(int id, [FromBody] UpdateRoomTierDto dto)
    {
        try
        {
            var success = await _roomSubscriptionService.UpdateTierAsync(id, dto);
            if (!success)
                return NotFound(new { error = "Room subscription tier not found" });

            return Ok(new { message = "Room subscription tier updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating room subscription tier {Id}", id);
            return StatusCode(500, new { error = "Failed to update room subscription tier", details = ex.Message });
        }
    }

    // ========================= ROOM SUBSCRIPTIONS =========================

    /// <summary>
    /// Get all room subscriptions
    /// </summary>
    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetAllSubscriptions()
    {
        try
        {
            var subscriptions = await _roomSubscriptionService.GetAllRoomSubscriptionsAsync();
            return Ok(subscriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching room subscriptions");
            return StatusCode(500, new { error = "Failed to fetch room subscriptions", details = ex.Message });
        }
    }

    /// <summary>
    /// Get subscription for a specific room
    /// </summary>
    [HttpGet("subscriptions/room/{roomId}")]
    public async Task<IActionResult> GetRoomSubscription(int roomId)
    {
        try
        {
            var subscription = await _roomSubscriptionService.GetRoomSubscriptionAsync(roomId);
            if (subscription == null)
                return NotFound(new { error = "No subscription found for this room" });

            return Ok(subscription);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching subscription for room {RoomId}", roomId);
            return StatusCode(500, new { error = "Failed to fetch room subscription", details = ex.Message });
        }
    }

    /// <summary>
    /// Grant a subscription to a room
    /// </summary>
    [HttpPost("subscriptions/grant")]
    public async Task<IActionResult> GrantSubscription([FromBody] GrantRoomSubscriptionDto dto)
    {
        try
        {
            var success = await _roomSubscriptionService.GrantRoomSubscriptionAsync(dto);
            if (!success)
                return BadRequest(new { error = "Failed to grant subscription" });

            return Ok(new { message = "Subscription granted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting subscription to room {RoomId}", dto.RoomId);
            return StatusCode(500, new { error = "Failed to grant subscription", details = ex.Message });
        }
    }

    /// <summary>
    /// Revoke a room's subscription
    /// </summary>
    [HttpPost("subscriptions/revoke/{roomId}")]
    public async Task<IActionResult> RevokeSubscription(int roomId)
    {
        try
        {
            var success = await _roomSubscriptionService.RevokeRoomSubscriptionAsync(roomId);
            if (!success)
                return NotFound(new { error = "No active subscription found for this room" });

            return Ok(new { message = "Subscription revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking subscription for room {RoomId}", roomId);
            return StatusCode(500, new { error = "Failed to revoke subscription", details = ex.Message });
        }
    }

    /// <summary>
    /// Extend a room's subscription
    /// </summary>
    [HttpPost("subscriptions/extend/{roomId}")]
    public async Task<IActionResult> ExtendSubscription(int roomId, [FromQuery] int days)
    {
        try
        {
            if (days <= 0)
                return BadRequest(new { error = "Days must be greater than 0" });

            var success = await _roomSubscriptionService.ExtendRoomSubscriptionAsync(roomId, days);
            if (!success)
                return NotFound(new { error = "No active subscription found for this room" });

            return Ok(new { message = $"Subscription extended by {days} days" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending subscription for room {RoomId}", roomId);
            return StatusCode(500, new { error = "Failed to extend subscription", details = ex.Message });
        }
    }

    // ========================= ROOMS SEARCH =========================

    /// <summary>
    /// Search rooms by name or ID
    /// </summary>
    [HttpGet("rooms/search")]
    public async Task<IActionResult> SearchRooms([FromQuery] string? query, [FromQuery] int limit = 50)
    {
        try
        {
            var rooms = await _roomSubscriptionService.SearchRoomsAsync(query, limit);
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching rooms with query {Query}", query);
            return StatusCode(500, new { error = "Failed to search rooms", details = ex.Message });
        }
    }

    // ========================= STATISTICS =========================

    /// <summary>
    /// Get room subscription statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var stats = await _roomSubscriptionService.GetStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching room subscription statistics");
            return StatusCode(500, new { error = "Failed to fetch statistics", details = ex.Message });
        }
    }
}

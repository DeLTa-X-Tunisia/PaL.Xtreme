using PaLX.API.DTOs;
using PaLX.API.Models;

namespace PaLX.API.Services
{
    public interface IRoomService
    {
        Task<RoomDto> CreateRoomAsync(int userId, CreateRoomDto dto);
        Task<bool> JoinRoomAsync(int userId, int roomId, string? password, bool isInvisible = false);
        Task LeaveRoomAsync(int userId, int roomId);
        Task<List<RoomDto>> GetRoomsAsync(int userId, int? categoryId = null);
        Task<List<RoomMemberDto>> GetRoomMembersAsync(int roomId, int? requesterId = null);
        Task<RoomMessageDto> SendMessageAsync(int userId, int roomId, string content, string type = "Text", string? attachmentUrl = null);
        Task<List<RoomMessageDto>> GetRoomMessagesAsync(int roomId, int limit = 50);
        Task<bool> UpdateMemberStatusAsync(int userId, int roomId, bool? isCamOn, bool? isMicOn, bool? hasHandRaised);
        Task<List<RoomCategoryDto>> GetCategoriesAsync();
        Task<List<RoomSubCategoryDto>> GetSubCategoriesAsync(int categoryId);
        Task<List<RoomSubscriptionTierDto>> GetRoomSubscriptionTiersAsync();
        Task<List<MyRoomDto>> GetMyRoomsAsync(int userId);
        Task<RoomSubscriptionInfoDto?> GetRoomSubscriptionAsync(int roomId);
        Task<bool> UpgradeRoomSubscriptionAsync(int userId, int roomId, int newTierLevel, string? transactionId = null, string? paymentMethod = null);
        Task DeleteRoomAsync(int userId, int roomId);
        Task<RoomDto> UpdateRoomAsync(int userId, int roomId, CreateRoomDto dto);
        Task<bool> ToggleRoomVisibilityAsync(int userId, int roomId);
        
        /// <summary>
        /// Toggle le statut IsSystemHidden d'un salon (admin système uniquement).
        /// Quand TRUE, même le RoomOwner ne voit plus son salon.
        /// </summary>
        Task<bool> ToggleSystemHiddenAsync(int userId, int roomId);
        
        // Room Admins Management (Simplified)
        Task<List<RoomRoleInfoDto>> GetRoomRolesAsync(int requesterId, int roomId);
        Task AssignRoleAsync(int ownerId, int roomId, int targetUserId, string role);
        Task RemoveRoomRoleAsync(int ownerId, int roomId, int targetUserId);
        Task<string?> GetUserRoleInRoomAsync(int userId, int roomId);
    }
}

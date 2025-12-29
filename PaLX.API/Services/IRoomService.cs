using PaLX.API.DTOs;
using PaLX.API.Models;

namespace PaLX.API.Services
{
    public interface IRoomService
    {
        Task<RoomDto> CreateRoomAsync(int userId, CreateRoomDto dto);
        Task<bool> JoinRoomAsync(int userId, int roomId, string? password);
        Task LeaveRoomAsync(int userId, int roomId);
        Task<List<RoomDto>> GetRoomsAsync(int userId, int? categoryId = null);
        Task<List<RoomMemberDto>> GetRoomMembersAsync(int roomId);
        Task<RoomMessageDto> SendMessageAsync(int userId, int roomId, string content, string type = "Text", string? attachmentUrl = null);
        Task<List<RoomMessageDto>> GetRoomMessagesAsync(int roomId, int limit = 50);
        Task<bool> UpdateMemberStatusAsync(int userId, int roomId, bool? isCamOn, bool? isMicOn, bool? hasHandRaised);
        Task<List<RoomCategoryDto>> GetCategoriesAsync();
        Task DeleteRoomAsync(int userId, int roomId);
        Task<RoomDto> UpdateRoomAsync(int userId, int roomId, CreateRoomDto dto);
        Task<bool> ToggleRoomVisibilityAsync(int userId, int roomId);
    }
}

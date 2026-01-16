using PaLX.API.Models;
using PaLX.API.DTOs;

namespace PaLX.API.Services
{
    public interface IUserService
    {
        // ===== User Retrieval =====
        Task<User?> GetByIdAsync(int userId);
        Task<User?> GetByUsernameAsync(string username);
        
        // ===== Registration =====
        Task<bool> RegisterUserAsync(string username, string password);
        
        // ===== Profile Management =====
        Task<UserProfileDto?> GetUserProfileAsync(string username);
        Task<PublicProfileDto?> GetPublicProfileAsync(int viewerId, int viewedUserId, string context);
        Task<List<ProfileViewerDto>> GetProfileViewersAsync(int userId, int limit = 50);
        Task<bool> DeleteProfileViewAsync(int userId, int viewerId);
        Task<bool> UpdateUserProfileAsync(string username, UserProfileDto profile);
        
        // ===== Friends =====
        Task<List<FriendDto>> GetFriendsAsync(string username);
        Task<List<FriendDto>> GetPendingRequestsAsync(string username);
        Task<List<FriendDto>> SearchUsersAsync(string query, string currentUsername);
        Task<bool> SendFriendRequestAsync(string fromUser, string toUser);
        Task<bool> RespondToFriendRequestAsync(string responder, string requester, int response);
        Task<bool> RemoveFriendAsync(string username, string friendUsername);
        
        // ===== Blocking =====
        Task<bool> BlockUserAsync(string blocker, BlockRequestModel model);
        Task<bool> UnblockUserAsync(string blocker, string blocked);
        Task<bool> IsUserBlockedAsync(string user1, string user2);
        Task<List<BlockedUserDto>> GetBlockedUsersAsync(string username);
        
        // ===== Status & Chat =====
        Task UpdateStatusAsync(string username, int status);
        Task<List<ChatMessageDto>> GetChatHistoryAsync(string user1, string user2);
        Task MarkMessagesAsReadAsync(string sender, string receiver);
        Task<List<string>> GetUnreadSendersAsync(string username);
    }
}
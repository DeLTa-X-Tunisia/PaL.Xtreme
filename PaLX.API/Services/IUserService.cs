using PaLX.API.Models;
using PaLX.API.DTOs;

namespace PaLX.API.Services
{
    public interface IUserService
    {
        Task<bool> RegisterUserAsync(string username, string password);
        Task<UserProfileDto?> GetUserProfileAsync(string username);
        Task<bool> UpdateUserProfileAsync(string username, UserProfileDto profile);
        Task<List<FriendDto>> GetFriendsAsync(string username);
        Task<bool> BlockUserAsync(string blocker, BlockRequestModel model);
        Task<bool> UnblockUserAsync(string blocker, string blocked);
        Task<bool> IsUserBlockedAsync(string user1, string user2);
        Task UpdateStatusAsync(string username, int status);
        Task<List<BlockedUserDto>> GetBlockedUsersAsync(string username);
        Task<List<ChatMessageDto>> GetChatHistoryAsync(string user1, string user2);
        Task MarkMessagesAsReadAsync(string sender, string receiver);
        Task<List<FriendDto>> GetPendingRequestsAsync(string username);
        Task<List<FriendDto>> SearchUsersAsync(string query, string currentUsername);
        Task<bool> SendFriendRequestAsync(string fromUser, string toUser);
        Task<bool> RespondToFriendRequestAsync(string responder, string requester, int response);
        Task<bool> RemoveFriendAsync(string username, string friendUsername);
        Task<List<string>> GetUnreadSendersAsync(string username);
    }
}
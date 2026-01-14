namespace PaLX.Client.Services
{
    /// <summary>
    /// Interface pour les services API côté client.
    /// Définit le contrat commun pour tous les services spécialisés.
    /// </summary>
    public interface IApiServiceBase
    {
        /// <summary>
        /// URL de base de l'API
        /// </summary>
        string BaseUrl { get; }
        
        /// <summary>
        /// Token JWT d'authentification
        /// </summary>
        string? AuthToken { get; }
        
        /// <summary>
        /// Vérifie si l'utilisateur est authentifié
        /// </summary>
        bool IsAuthenticated { get; }
    }

    /// <summary>
    /// Service dédié aux opérations d'authentification.
    /// À implémenter lors du refactoring complet.
    /// </summary>
    public interface IAuthService : IApiServiceBase
    {
        Task<bool> LoginAsync(string username, string password);
        Task<bool> RegisterAsync(string username, string password, string confirmPassword);
        Task LogoutAsync();
        Task<bool> RefreshTokenAsync();
    }

    /// <summary>
    /// Service dédié aux opérations sur les amis.
    /// À implémenter lors du refactoring complet.
    /// </summary>
    public interface IFriendService : IApiServiceBase
    {
        Task<List<FriendDto>> GetFriendsAsync();
        Task<bool> AddFriendAsync(string username);
        Task<bool> RemoveFriendAsync(int friendId);
        Task<bool> AcceptFriendRequestAsync(int requestId);
        Task<bool> BlockUserAsync(int userId);
        Task<bool> UnblockUserAsync(int userId);
    }

    /// <summary>
    /// Service dédié aux opérations sur les salons.
    /// À implémenter lors du refactoring complet.
    /// </summary>
    public interface IRoomClientService : IApiServiceBase
    {
        Task<List<RoomDto>> GetRoomsAsync();
        Task<RoomDto?> GetRoomAsync(int roomId);
        Task<bool> JoinRoomAsync(int roomId, string? password = null);
        Task<bool> LeaveRoomAsync(int roomId);
        Task<bool> CreateRoomAsync(CreateRoomDto dto);
        Task<bool> DeleteRoomAsync(int roomId);
    }

    /// <summary>
    /// Service dédié aux opérations sur le profil utilisateur.
    /// À implémenter lors du refactoring complet.
    /// </summary>
    public interface IProfileService : IApiServiceBase
    {
        Task<UserProfileDto?> GetMyProfileAsync();
        Task<PublicProfileDto?> GetPublicProfileAsync(int userId);
        Task<bool> UpdateAvatarAsync(string avatarPath);
        Task<List<ProfileViewerDto>> GetProfileViewersAsync();
        Task<bool> DeleteProfileViewerAsync(int viewerId);
    }

    /// <summary>
    /// Service dédié aux opérations sur le Bot IA.
    /// À implémenter lors du refactoring complet.
    /// </summary>
    public interface IBotClientService : IApiServiceBase
    {
        Task<BotConfigDto?> GetBotConfigAsync(int roomId);
        Task<bool> UpdateBotConfigAsync(int roomId, UpdateBotConfigDto dto);
        Task<bool> ToggleBotAsync(int roomId, bool enabled);
        Task<List<BannedWordDto>> GetBannedWordsAsync(int roomId);
        Task<bool> AddBannedWordAsync(int roomId, AddBannedWordDto dto);
        Task<bool> RemoveBannedWordAsync(int roomId, int wordId);
        Task<bool> StartQuizAsync(int roomId);
        Task<bool> SuggestTopicAsync(int roomId);
    }

    // Note: Ces interfaces sont préparées pour un futur refactoring.
    // Actuellement, toutes les fonctionnalités sont dans ApiService.cs.
    // Le refactoring progressif permettra de découper ApiService en services spécialisés
    // tout en maintenant la compatibilité avec le code existant.
    // 
    // Les interfaces IMessageService et IFileTransferService seront ajoutées
    // quand les DTOs correspondants seront créés.
}

using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using System.Windows;
using System.IO;

namespace PaLX.Client.Services
{
    public class ApiService
    {
        private static ApiService? _instance;
        public static ApiService Instance => _instance ??= new ApiService();

        private readonly HttpClient _httpClient;
        private HubConnection? _hubConnection;
        private HubConnection? _roomHubConnection;
        private string _authToken = string.Empty;
        private int _currentRoomId = 0; // Pour la reconnexion au groupe SignalR
        public const string BaseUrl = "http://localhost:5145"; // Adjust if needed
        
        public string CurrentUsername { get; private set; } = string.Empty;
        public int CurrentUserId { get; private set; }
        public int CurrentUserRoleLevel { get; private set; } = 7; // Default to User
        
        /// <summary>
        /// Vérifie si l'utilisateur connecté est un admin système (RoleLevel 1-5)
        /// ServerMaster(1), ServerEditor(2), ServerSuperAdmin(3), ServerAdmin(4), ServerModerator(5)
        /// Ces rôles ont un accès total à tous les salons.
        /// </summary>
        public bool IsSystemAdmin => CurrentUserRoleLevel >= 1 && CurrentUserRoleLevel <= 5;

        public event Action<string, string>? OnMessageReceived;
        public event Action<string, string, int>? OnPrivateMessageReceived;
        public event Action<int>? OnAudioListened;
        public event Action<string>? OnUserTyping;
        public event Action<string>? OnBuzzReceived;
        public event Action<string, string>? OnUserStatusChanged;

        // Room Events
        public event Action<RoomMessageDto>? OnRoomMessageReceived;
        public event Action<RoomMemberDto>? OnRoomUserJoined;
        public event Action<int>? OnRoomUserLeft;
        public event Action<int, bool?, bool?, bool?>? OnRoomMemberStatusUpdated;
        public event Action<int, string, string, string>? OnMemberRoleUpdated; // userId, displayName, color, icon
        
        // Room Video Events (centralisés ici pour éviter les effets de bord)
        public event Action<int, int, string>? OnRoomCameraStarted; // roomId, userId, username
        public event Action<int, int>? OnRoomCameraStopped; // roomId, userId
        public event Action<int, int, byte[]>? OnRoomVideoFrame; // roomId, userId, frameData
        
        // ═══════════════════════════════════════════════════════════════════════════════════
        // KICK & BAN EVENTS - v1.8.4
        // ═══════════════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Événement déclenché quand l'utilisateur est kické d'un salon
        /// Params: roomId, roomName, reason
        /// </summary>
        public event Action<int, string, string>? OnUserKicked;
        
        /// <summary>
        /// Événement déclenché quand l'utilisateur est banni d'un salon
        /// Params: roomId, roomName, reason, banType, expiresAt
        /// </summary>
        public event Action<int, string, string, string, DateTime?>? OnUserBanned;
        
        // Image Transfer Events
        public event Action<int, string, string, string>? OnImageRequestReceived; // id, sender, filename, url
        public event Action<int, string, string, string>? OnImageRequestSent; // id, receiver, filename, url
        public event Action<int, bool, string>? OnImageTransferUpdated; // id, isAccepted, url

        // Video Transfer Events
        public event Action<int, string, string, string>? OnVideoRequestReceived; // id, sender, filename, url
        public event Action<int, string, string, string>? OnVideoRequestSent; // id, receiver, filename, url
        public event Action<int, bool, string>? OnVideoTransferUpdated; // id, isAccepted, url

        // Audio Transfer Events
        public event Action<int, string, string, string>? OnAudioRequestReceived; // id, sender, filename, url
        public event Action<int, string, string, string>? OnAudioRequestSent; // id, receiver, filename, url
        public event Action<int, bool, string>? OnAudioTransferUpdated; // id, isAccepted, url

        // File Transfer Events
        public event Action<int, string, string, string>? OnFileRequestReceived; // id, sender, filename, url
        public event Action<int, string, string, string>? OnFileRequestSent; // id, receiver, filename, url
        public event Action<int, bool, string>? OnFileTransferUpdated; // id, isAccepted, url

        // Friend Events
        public event Action<string>? OnFriendRequestReceived;
        public event Action<string>? OnFriendRequestAccepted;
        public event Action<string>? OnFriendRemoved;

        // Block Events
        public event Action<string>? OnUserBlocked;
        public event Action<string>? OnUserBlockedBy;
        public event Action<string>? OnUserUnblocked;
        public event Action<string>? OnUserUnblockedBy;

        // Room Role Request Events
        public event Action<RoleRequestReceivedDto>? OnRoleRequestReceived;
        
        // Room Role Removed Event (when owner removes your role)
        public event Action<int, string>? OnRoleRemoved; // roomId, roomName
        
        // Room Role Assigned Event (when owner assigns you a role)
        public event Action<int, string, string>? OnRoleAssigned; // roomId, roomName, role

        // Room Visibility Changed Event (real-time update)
        public event Action<int, bool, bool>? OnRoomVisibilityChanged; // roomId, isActive, isSystemHidden

        // Global Announcement Event (admin broadcast)
        public event Action<GlobalAnnouncementDto>? OnGlobalAnnouncementReceived;

        // Room Invitation Event (v2.4.0) - friend inviting to room
        // Params: inviterUsername, inviterDisplayName, inviterAvatarPath, roomId, roomName, roomCategory
        public event Action<string, string, string?, int, string, string>? OnRoomInvitationReceived;
        public event Action<string, int>? OnRoomInvitationSent; // targetUsername, roomId (confirmation)

        // System Events
        public event Action? OnConnectionClosed;
        public event Action<string>? OnForceDisconnect; // Session kicked by another login

        public event Action<string>? OnChatCleared;
        public event Action<string>? OnPartnerLeft;

        public HubConnection? GetHubConnection() => _hubConnection;
        public HubConnection? HubConnection => _roomHubConnection ?? _hubConnection;
        /// <summary>
        /// Connexion spécifique au RoomHub - utiliser pour toutes les opérations de chatroom
        /// </summary>
        public HubConnection? RoomHubConnection => _roomHubConnection;
        public VoiceCallService? VoiceService { get; private set; }
        public VideoCallService? VideoService { get; private set; }
        
        /// <summary>
        /// Indique si l'utilisateur a un abonnement premium actif
        /// </summary>
        public bool HasPremiumSubscription { get; private set; } = false;
        
        public string GetBaseUrl() => BaseUrl;

        private ApiService()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        }

        public async Task<(AuthResponse? Response, bool IsConnectionError)> LoginAsync(string username, string password, string ip, string deviceName, string deviceNumber, bool forceConnect = false)
        {
            try
            {
                var model = new { 
                    Username = username, 
                    Password = password, 
                    IpAddress = ip, 
                    DeviceName = deviceName, 
                    DeviceNumber = deviceNumber,
                    ForceConnect = forceConnect
                };
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", model);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                    if (result != null)
                    {
                        // Check if already connected (session control)
                        if (result.IsAlreadyConnected)
                        {
                            return (result, false);
                        }
                        
                        if (!string.IsNullOrEmpty(result.Token))
                        {
                            _authToken = result.Token;
                            CurrentUsername = username;
                            CurrentUserId = result.UserId;
                            CurrentUserRoleLevel = result.RoleLevel;
                            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
                            return (result, false);
                        }
                    }
                }
                return (null, false);
            }
            catch (Exception)
            {
                Application.Current.Dispatcher.Invoke(() => 
                {
                    new ServiceUnavailableWindow().ShowDialog();
                });
                return (null, true);
            }
        }

        public async Task<bool> RegisterAsync(string username, string password, string confirmPassword)
        {
            try
            {
                var model = new { Username = username, Password = password, ConfirmPassword = confirmPassword };
                var response = await _httpClient.PostAsJsonAsync("api/user/register", model);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<FriendDto>> GetFriendsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<FriendDto>>("api/user/friends") ?? new List<FriendDto>();
            }
            catch
            {
                return new List<FriendDto>();
            }
        }

        public async Task<UserProfileDto?> GetUserProfileAsync(string username)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<UserProfileDto>($"api/user/profile/{username}");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Récupère le profil public d'un utilisateur par son ID (enregistre la visite côté serveur)
        /// </summary>
        public async Task<PublicProfileDto?> GetPublicProfileByIdAsync(int userId, string context = "room")
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<PublicProfileDto>($"api/user/public-profile/{userId}?context={context}");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Récupère la liste des utilisateurs qui ont consulté mon profil
        /// </summary>
        public async Task<List<ProfileViewerDto>> GetProfileViewersAsync(int limit = 50)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<ProfileViewerDto>>($"api/user/profile-viewers?limit={limit}") 
                       ?? new List<ProfileViewerDto>();
            }
            catch
            {
                return new List<ProfileViewerDto>();
            }
        }

        /// <summary>
        /// Supprime une visite de profil (un visiteur de la liste)
        /// </summary>
        public async Task<bool> DeleteProfileViewerAsync(int viewerId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/user/profile-viewers/{viewerId}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════════
        // BOT IA API - v1.8.8
        // ═══════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Récupère la configuration du bot pour un salon
        /// </summary>
        public async Task<BotConfigDto?> GetBotConfigAsync(int roomId)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<BotConfigDto>($"api/bot/config/{roomId}");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Met à jour la configuration du bot pour un salon
        /// </summary>
        public async Task<(BotConfigDto? Config, string? Error)> UpdateBotConfigAsync(int roomId, UpdateBotConfigDto dto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/bot/config/{roomId}", dto);
                if (response.IsSuccessStatusCode)
                {
                    var config = await response.Content.ReadFromJsonAsync<BotConfigDto>();
                    return (config, null);
                }
                
                // Lire le message d'erreur du serveur
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[ApiService] UpdateBotConfig error: {response.StatusCode} - {errorContent}");
                return (null, $"Erreur {(int)response.StatusCode}: {errorContent}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] UpdateBotConfig exception: {ex.Message}");
                return (null, ex.Message);
            }
        }

        /// <summary>
        /// Active ou désactive le bot pour un salon
        /// </summary>
        public async Task<bool> ToggleBotAsync(int roomId, bool enabled)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/bot/config/{roomId}/toggle?enabled={enabled}", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Récupère la liste des mots interdits d'un salon
        /// </summary>
        public async Task<List<BannedWordDto>> GetBannedWordsAsync(int roomId)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<BannedWordDto>>($"api/bot/words/{roomId}") 
                       ?? new List<BannedWordDto>();
            }
            catch
            {
                return new List<BannedWordDto>();
            }
        }

        /// <summary>
        /// Ajoute un mot interdit dans un salon
        /// </summary>
        public async Task<BannedWordDto?> AddBannedWordAsync(int roomId, AddBannedWordDto dto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/bot/words/{roomId}", dto);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<BannedWordDto>();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Supprime un mot interdit d'un salon
        /// </summary>
        public async Task<bool> RemoveBannedWordAsync(int roomId, int wordId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/bot/words/{roomId}/{wordId}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lance un quiz dans le salon
        /// </summary>
        public async Task<bool> StartQuizAsync(int roomId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/bot/quiz/{roomId}/start", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Suggère un sujet de discussion dans le salon
        /// </summary>
        public async Task<bool> SuggestTopicAsync(int roomId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/bot/topics/{roomId}/suggest", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> UploadImageAsync(string filePath)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/" + Path.GetExtension(filePath).TrimStart('.'));
                content.Add(streamContent, "file", Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync("api/upload/image", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (result.TryGetProperty("url", out var urlProperty))
                    {
                        return urlProperty.GetString();
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        public async Task<bool> UpdateUserProfileAsync(UserProfileDto profile)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/user/profile", profile);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task UpdateStatusAsync(int status)
        {
            try
            {
                await _httpClient.PostAsJsonAsync("api/user/status", status);
            }
            catch { }
        }

        public async Task<(bool Success, string Message)> BlockUserAsync(string username, int blockType = 0, DateTime? endDate = null, string? reason = null)
        {
            try
            {
                var model = new BlockRequestModel 
                { 
                    BlockedUsername = username, 
                    BlockType = blockType, 
                    EndDate = endDate, 
                    Reason = reason ?? string.Empty
                };
                var response = await _httpClient.PostAsJsonAsync("api/user/block", model);
                
                if (response.IsSuccessStatusCode) return (true, "Success");

                try 
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(errorJson);
                    if (doc.RootElement.TryGetProperty("message", out var msg))
                    {
                        return (false, msg.GetString() ?? "Erreur inconnue");
                    }
                }
                catch {}

                return (false, "Erreur lors du blocage.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<List<BlockedUserDto>> GetBlockedUsersAsync()
        {
            var response = await _httpClient.GetAsync("api/user/blocked");
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception(content);
            }
            return await response.Content.ReadFromJsonAsync<List<BlockedUserDto>>() ?? new List<BlockedUserDto>();
        }

        public async Task<List<ChatMessageDto>> GetChatHistoryAsync(string partner)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<ChatMessageDto>>($"api/user/chat/history?partner={partner}") ?? new List<ChatMessageDto>();
            }
            catch { return new List<ChatMessageDto>(); }
        }

        public async Task MarkMessagesAsReadAsync(string partner)
        {
            try
            {
                var model = new { Partner = partner };
                await _httpClient.PostAsJsonAsync("api/user/chat/read", model);
            }
            catch { }
        }

        public async Task<(bool Success, string Message)> UnblockUserAsync(string username)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/user/unblock", username);
                if (response.IsSuccessStatusCode) return (true, "Success");
                return (false, "Erreur lors du déblocage.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<bool> RemoveFriendAsync(string username)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/user/removefriend", username);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<List<FriendDto>> GetPendingRequestsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<FriendDto>>("api/user/requests") ?? new List<FriendDto>();
            }
            catch { return new List<FriendDto>(); }
        }

        public async Task<List<FriendDto>> SearchUsersAsync(string query)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<FriendDto>>($"api/user/search?query={query}") ?? new List<FriendDto>();
            }
            catch { return new List<FriendDto>(); }
        }

        public async Task<List<string>> GetUnreadConversationsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<string>>("api/user/unread-conversations") ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        public async Task<bool> SendFriendRequestAsync(string toUser)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/user/request", toUser);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> RespondToFriendRequestAsync(string requester, int responseValue)
        {
            try
            {
                var model = new { Requester = requester, Response = responseValue };
                var response = await _httpClient.PostAsJsonAsync("api/user/respond", model);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> IsUserBlockedAsync(string user1, string user2)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/user/isblocked?user1={user1}&user2={user2}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<bool>();
                }
                return false;
            }
            catch { return false; }
        }

        private bool _isIntentionalDisconnect = false;

        public async Task ConnectSignalRAsync()
        {
            if (string.IsNullOrEmpty(_authToken)) return;
            
            _isIntentionalDisconnect = false;

            // Chat Hub
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{BaseUrl}/chatHub", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult((string?)_authToken);
                })
                .Build();

            // Room Hub
            _roomHubConnection = new HubConnectionBuilder()
                .WithUrl($"{BaseUrl}/roomHub", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult((string?)_authToken);
                })
                .Build();

            VoiceService = new VoiceCallService(_hubConnection);
            VideoService = new VideoCallService(_hubConnection);

            // ... (Existing ChatHub Handlers) ...
            _hubConnection.On<string, string>("ReceiveMessage", (user, message) => OnMessageReceived?.Invoke(user, message));
            _hubConnection.On<string, string, int>("ReceivePrivateMessage", (user, message, id) => OnPrivateMessageReceived?.Invoke(user, message, id));
            _hubConnection.On<int>("AudioListened", (id) => OnAudioListened?.Invoke(id));
            _hubConnection.On<string>("UserTyping", (user) => OnUserTyping?.Invoke(user));
            _hubConnection.On<string>("ReceiveBuzz", (user) => OnBuzzReceived?.Invoke(user));
            _hubConnection.On<string, string>("UserStatusChanged", (user, status) => OnUserStatusChanged?.Invoke(user, status));

            _hubConnection.On<string>("ChatCleared", (partnerUser) => OnChatCleared?.Invoke(partnerUser));
            _hubConnection.On<string>("PartnerLeft", (partnerUser) => OnPartnerLeft?.Invoke(partnerUser));
            
            // Force Disconnect Handler - when another session takes over
            _hubConnection.On<string>("ForceDisconnect", (reason) => OnForceDisconnect?.Invoke(reason));
            
            _hubConnection.On<string>("FriendRequestReceived", (username) => OnFriendRequestReceived?.Invoke(username));
            _hubConnection.On<string>("FriendRequestAccepted", (username) => OnFriendRequestAccepted?.Invoke(username));
            _hubConnection.On<string>("FriendRemoved", (username) => OnFriendRemoved?.Invoke(username));
            _hubConnection.On<string>("UserBlocked", (blockedUser) => OnUserBlocked?.Invoke(blockedUser));
            _hubConnection.On<string>("UserBlockedBy", (blocker) => OnUserBlockedBy?.Invoke(blocker));
            _hubConnection.On<string>("UserUnblocked", (unblockedUser) => OnUserUnblocked?.Invoke(unblockedUser));
            _hubConnection.On<string>("UserUnblockedBy", (blocker) => OnUserUnblockedBy?.Invoke(blocker));

            // Room Role Request Handler - Debug avec log
            _hubConnection.On<int, int, string, string, int>("RoleRequestReceived", (requestId, roomId, roomName, role, requesterId) => 
            {
                Console.WriteLine($"[SignalR CLIENT] *** RoleRequestReceived EVENT FIRED ***");
                Console.WriteLine($"[SignalR CLIENT] requestId={requestId}, roomId={roomId}, roomName={roomName}, role={role}, requesterId={requesterId}");
                try
                {
                    var dto = new RoleRequestReceivedDto 
                    { 
                        RequestId = requestId, 
                        RoomId = roomId, 
                        RoomName = roomName, 
                        Role = role, 
                        RequesterId = requesterId 
                    };
                    Console.WriteLine($"[SignalR CLIENT] Invoking OnRoleRequestReceived event...");
                    OnRoleRequestReceived?.Invoke(dto);
                    Console.WriteLine($"[SignalR CLIENT] OnRoleRequestReceived event invoked successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SignalR CLIENT] ERROR in RoleRequestReceived handler: {ex.Message}\n{ex.StackTrace}");
                }
            });

            // Handler pour la suppression de rôle (RoleRemoved)
            _hubConnection.On<int, string>("RoleRemoved", (roomId, roomName) =>
            {
                Console.WriteLine($"[SignalR CLIENT] *** RoleRemoved EVENT FIRED ***");
                Console.WriteLine($"[SignalR CLIENT] roomId={roomId}, roomName={roomName}");
                try
                {
                    OnRoleRemoved?.Invoke(roomId, roomName);
                    Console.WriteLine($"[SignalR CLIENT] OnRoleRemoved event invoked successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SignalR CLIENT] ERROR in RoleRemoved handler: {ex.Message}\n{ex.StackTrace}");
                }
            });

            // Handler pour l'attribution de rôle (RoleAssigned)
            _hubConnection.On<int, string, string>("RoleAssigned", (roomId, roomName, role) =>
            {
                Console.WriteLine($"[SignalR CLIENT] *** RoleAssigned EVENT FIRED ***");
                Console.WriteLine($"[SignalR CLIENT] roomId={roomId}, roomName={roomName}, role={role}");
                try
                {
                    OnRoleAssigned?.Invoke(roomId, roomName, role);
                    Console.WriteLine($"[SignalR CLIENT] OnRoleAssigned event invoked successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SignalR CLIENT] ERROR in RoleAssigned handler: {ex.Message}\n{ex.StackTrace}");
                }
            });

            // Handler pour le changement de visibilité d'un salon (temps réel)
            _hubConnection.On<int, bool, bool>("RoomVisibilityChanged", (roomId, isActive, isSystemHidden) =>
            {
                Console.WriteLine($"[SignalR CLIENT] *** RoomVisibilityChanged EVENT FIRED ***");
                Console.WriteLine($"[SignalR CLIENT] roomId={roomId}, isActive={isActive}, isSystemHidden={isSystemHidden}");
                try
                {
                    OnRoomVisibilityChanged?.Invoke(roomId, isActive, isSystemHidden);
                    Console.WriteLine($"[SignalR CLIENT] OnRoomVisibilityChanged event invoked successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SignalR CLIENT] ERROR in RoomVisibilityChanged handler: {ex.Message}\n{ex.StackTrace}");
                }
            });

            _hubConnection.On<int, string, string, string>("ReceiveImageRequest", (id, sender, filename, url) => OnImageRequestReceived?.Invoke(id, sender, filename, url));
            _hubConnection.On<int, string, string, string>("ImageRequestSent", (id, receiver, filename, url) => OnImageRequestSent?.Invoke(id, receiver, filename, url));
            _hubConnection.On<int, bool, string>("ImageTransferUpdated", (id, isAccepted, url) => OnImageTransferUpdated?.Invoke(id, isAccepted, url));

            _hubConnection.On<int, string, string, string>("ReceiveVideoRequest", (id, sender, filename, url) => OnVideoRequestReceived?.Invoke(id, sender, filename, url));
            _hubConnection.On<int, string, string, string>("VideoRequestSent", (id, receiver, filename, url) => OnVideoRequestSent?.Invoke(id, receiver, filename, url));
            _hubConnection.On<int, bool, string>("VideoTransferUpdated", (id, isAccepted, url) => OnVideoTransferUpdated?.Invoke(id, isAccepted, url));

            _hubConnection.On<int, string, string, string>("ReceiveAudioRequest", (id, sender, filename, url) => OnAudioRequestReceived?.Invoke(id, sender, filename, url));
            _hubConnection.On<int, string, string, string>("AudioRequestSent", (id, receiver, filename, url) => OnAudioRequestSent?.Invoke(id, receiver, filename, url));
            _hubConnection.On<int, bool, string>("AudioTransferUpdated", (id, isAccepted, url) => OnAudioTransferUpdated?.Invoke(id, isAccepted, url));

            _hubConnection.On<int, string, string, string>("ReceiveFileRequest", (id, sender, filename, url) => OnFileRequestReceived?.Invoke(id, sender, filename, url));
            _hubConnection.On<int, string, string, string>("FileRequestSent", (id, receiver, filename, url) => OnFileRequestSent?.Invoke(id, receiver, filename, url));
            _hubConnection.On<int, bool, string>("FileTransferUpdated", (id, isAccepted, url) => OnFileTransferUpdated?.Invoke(id, isAccepted, url));

            // Global Announcement Handler (admin broadcast to all users)
            _hubConnection.On<GlobalAnnouncementDto>("ReceiveGlobalAnnouncement", (announcement) =>
            {
                Console.WriteLine($"[SignalR CLIENT] *** ReceiveGlobalAnnouncement EVENT FIRED ***");
                Console.WriteLine($"[SignalR CLIENT] Type={announcement.Type}, Title={announcement.Title}, Message={announcement.Message}");
                try
                {
                    OnGlobalAnnouncementReceived?.Invoke(announcement);
                    Console.WriteLine($"[SignalR CLIENT] OnGlobalAnnouncementReceived event invoked successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SignalR CLIENT] ERROR in ReceiveGlobalAnnouncement handler: {ex.Message}\n{ex.StackTrace}");
                }
            });

            // Room Invitation Handler (v2.4.0) - Receive invitation to join a room
            _hubConnection.On<string, string, string, int, string, string>("ReceiveRoomInvitation", 
                (inviterUsername, inviterDisplayName, inviterAvatarPath, roomId, roomName, roomCategory) =>
            {
                Console.WriteLine($"[SignalR CLIENT] *** ReceiveRoomInvitation EVENT FIRED ***");
                Console.WriteLine($"[SignalR CLIENT] Inviter={inviterDisplayName}, Avatar={inviterAvatarPath ?? "null"}, Room={roomName} (ID={roomId})");
                try
                {
                    // Convert empty string to null for avatar
                    string? avatar = string.IsNullOrEmpty(inviterAvatarPath) ? null : inviterAvatarPath;
                    OnRoomInvitationReceived?.Invoke(inviterUsername, inviterDisplayName, avatar, roomId, roomName, roomCategory);
                    Console.WriteLine($"[SignalR CLIENT] OnRoomInvitationReceived event invoked successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SignalR CLIENT] ERROR in ReceiveRoomInvitation handler: {ex.Message}\n{ex.StackTrace}");
                }
            });

            // Room Invitation Sent confirmation (v2.4.0)
            _hubConnection.On<string, int>("RoomInvitationSent", (targetUsername, roomId) =>
            {
                Console.WriteLine($"[SignalR CLIENT] RoomInvitationSent: target={targetUsername}, roomId={roomId}");
                OnRoomInvitationSent?.Invoke(targetUsername, roomId);
            });

            // Room Hub Handlers - Messages et membres
            _roomHubConnection.On<RoomMessageDto>("ReceiveMessage", (dto) => 
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] ReceiveMessage received: RoomId={dto.RoomId}, UserId={dto.UserId}, Content={dto.Content?.Substring(0, Math.Min(20, dto.Content?.Length ?? 0))}...");
                OnRoomMessageReceived?.Invoke(dto);
            });
            _roomHubConnection.On<RoomMemberDto>("UserJoined", (member) => OnRoomUserJoined?.Invoke(member));
            _roomHubConnection.On<int>("UserLeft", (uid) => OnRoomUserLeft?.Invoke(uid));
            _roomHubConnection.On<int, bool?, bool?, bool?>("MemberStatusUpdated", (uid, cam, mic, hand) => OnRoomMemberStatusUpdated?.Invoke(uid, cam, mic, hand));
            _roomHubConnection.On<int, string, string, string>("MemberRoleUpdated", (uid, roleName, color, icon) => OnMemberRoleUpdated?.Invoke(uid, roleName, color, icon));
            
            // Room Hub Handlers - Vidéo (centralisés ici, une seule inscription)
            _roomHubConnection.On<int, int, string>("RoomCameraStarted", (roomId, userId, username) => 
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] RoomCameraStarted received: roomId={roomId}, userId={userId}, username={username}");
                OnRoomCameraStarted?.Invoke(roomId, userId, username);
            });
            _roomHubConnection.On<int, int>("RoomCameraStopped", (roomId, userId) => 
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] RoomCameraStopped received: roomId={roomId}, userId={userId}");
                OnRoomCameraStopped?.Invoke(roomId, userId);
            });
            _roomHubConnection.On<int, int, byte[]>("RoomVideoFrame", (roomId, userId, frameData) => 
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] RoomVideoFrame received: roomId={roomId}, userId={userId}, size={frameData?.Length ?? 0}");
                OnRoomVideoFrame?.Invoke(roomId, userId, frameData);
            });
            
            // Whisper handlers
            _roomHubConnection.On<int, int, string, string>("WhisperReceived", (roomId, fromUserId, fromDisplayName, message) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] WhisperReceived: roomId={roomId}, from={fromDisplayName}, message={message}");
                OnWhisperReceived?.Invoke(roomId, fromUserId, fromDisplayName, message);
            });
            _roomHubConnection.On<int, int, string>("WhisperSent", (roomId, toUserId, message) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] WhisperSent: roomId={roomId}, to={toUserId}, message={message}");
                OnWhisperSent?.Invoke(roomId, toUserId, message);
            });
            // Whisper pour les modérateurs (ils voient tous les chuchotements)
            _roomHubConnection.On<int, int, string, int, string>("WhisperModView", (roomId, fromUserId, fromDisplayName, toUserId, message) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] WhisperModView: roomId={roomId}, from={fromDisplayName}, to={toUserId}, message={message}");
                OnWhisperModView?.Invoke(roomId, fromUserId, fromDisplayName, toUserId, message);
            });

            // ═══════════════════════════════════════════════════════════════════════════════════
            // KICK & BAN HANDLERS - v1.8.4
            // ═══════════════════════════════════════════════════════════════════════════════════
            
            // Handler pour quand l'utilisateur est kické d'un salon
            _hubConnection.On<int, string, string>("UserKicked", (roomId, roomName, reason) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] UserKicked: roomId={roomId}, roomName={roomName}, reason={reason}");
                OnUserKicked?.Invoke(roomId, roomName, reason);
            });
            
            // Handler pour quand l'utilisateur est banni d'un salon
            _hubConnection.On<int, string, string, string, DateTime?>("UserBanned", (roomId, roomName, reason, banType, expiresAt) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] UserBanned: roomId={roomId}, roomName={roomName}, banType={banType}, expiresAt={expiresAt}");
                OnUserBanned?.Invoke(roomId, roomName, reason, banType, expiresAt);
            });

            // ... (Existing Transfer Handlers) ...
            
            try
            {
                await _hubConnection.StartAsync();
                await _roomHubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR Connection Error: {ex.Message}");
            }

            _hubConnection.Closed += async (error) =>
            {
                if (_isIntentionalDisconnect) return;
                OnConnectionClosed?.Invoke();
                await Task.Delay(new Random().Next(0, 5) * 1000);
                try { await _hubConnection.StartAsync(); } catch { }
            };
            
            // Handler de reconnexion pour RoomHub
            _roomHubConnection.Closed += async (error) =>
            {
                if (_isIntentionalDisconnect) return;
                System.Diagnostics.Debug.WriteLine($"[RoomHub] Connection closed: {error?.Message}");
                
                // Attendre un peu avant de reconnecter
                await Task.Delay(new Random().Next(1000, 3000));
                
                try 
                { 
                    await _roomHubConnection.StartAsync();
                    System.Diagnostics.Debug.WriteLine("[RoomHub] Reconnected successfully");
                    
                    // Re-joindre le groupe si on était dans une room
                    if (_currentRoomId > 0)
                    {
                        await _roomHubConnection.InvokeAsync("JoinRoomGroup", _currentRoomId);
                        System.Diagnostics.Debug.WriteLine($"[RoomHub] Rejoined room group {_currentRoomId}");
                    }
                } 
                catch (Exception ex)
                { 
                    System.Diagnostics.Debug.WriteLine($"[RoomHub] Reconnection failed: {ex.Message}");
                }
            };

            // Handlers are already registered above in the new block
            // Keeping existing handlers for compatibility if they were outside ConnectSignalRAsync in original file
            // But based on read_file, they seem to be inside ConnectSignalRAsync or just after initialization.
            // I will remove the duplicate handlers I added in the previous step if they conflict, 
            // but since I replaced the whole block, I should be careful.
            
            // Wait, I see I pasted handlers inside ConnectSignalRAsync in my previous edit.
            // The original file had handlers attached to _hubConnection right after Build().
            // I need to make sure I didn't break the existing handlers structure.
            // The read_file output shows handlers being attached.
            
            // Let's just add the Room methods at the end of the class or appropriate place.
        }

        public async Task DisconnectSignalRAsync()
        {
            _isIntentionalDisconnect = true;
            if (_hubConnection != null) await _hubConnection.StopAsync();
            if (_roomHubConnection != null) await _roomHubConnection.StopAsync();
        }

        // Room Methods
        public async Task JoinRoomGroupAsync(int roomId)
        {
            if (_roomHubConnection != null && _roomHubConnection.State == HubConnectionState.Connected)
            {
                await _roomHubConnection.InvokeAsync("JoinRoomGroup", roomId);
                _currentRoomId = roomId; // Mémoriser pour la reconnexion
            }
        }

        public async Task LeaveRoomGroupAsync(int roomId)
        {
            if (_roomHubConnection != null && _roomHubConnection.State == HubConnectionState.Connected)
            {
                await _roomHubConnection.InvokeAsync("LeaveRoomGroup", roomId);
            }
            _currentRoomId = 0; // Réinitialiser
        }

        public async Task<List<RoomMemberDto>> GetRoomMembersAsync(int roomId)
        {
            return await _httpClient.GetFromJsonAsync<List<RoomMemberDto>>($"api/room/{roomId}/members") ?? new List<RoomMemberDto>();
        }

        public async Task<List<RoomMessageDto>> GetRoomMessagesAsync(int roomId, int limit = 50)
        {
            return await _httpClient.GetFromJsonAsync<List<RoomMessageDto>>($"api/room/{roomId}/messages?limit={limit}") ?? new List<RoomMessageDto>();
        }

        public async Task SendRoomMessageAsync(int roomId, string content, string type = "Text", string? attachmentUrl = null)
        {
            var dto = new SendMessageDto { Content = content, Type = type, AttachmentUrl = attachmentUrl };
            await _httpClient.PostAsJsonAsync($"api/room/{roomId}/messages", dto);
        }

        /// <summary>
        /// Envoyer un chuchotement privé à un utilisateur dans le room
        /// </summary>
        public async Task SendWhisperAsync(int roomId, int targetUserId, string message, string senderDisplayName)
        {
            if (_roomHubConnection != null && _roomHubConnection.State == HubConnectionState.Connected)
            {
                await _roomHubConnection.InvokeAsync("SendWhisper", roomId, targetUserId, message, senderDisplayName);
            }
        }

        // Events for Whisper
        public event Action<int, int, string, string>? OnWhisperReceived; // roomId, fromUserId, fromDisplayName, message
        public event Action<int, int, string>? OnWhisperSent; // roomId, toUserId, message
        public event Action<int, int, string, int, string>? OnWhisperModView; // roomId, fromUserId, fromDisplayName, toUserId, message (pour modérateurs)

        public async Task UpdateRoomStatusAsync(int roomId, bool? isCamOn, bool? isMicOn, bool? hasHandRaised)
        {
            var dto = new UpdateStatusDto { IsCamOn = isCamOn, IsMicOn = isMicOn, HasHandRaised = hasHandRaised };
            await _httpClient.PutAsJsonAsync($"api/room/{roomId}/status", dto);
        }

        public async Task LeaveRoomAsync(int roomId)
        {
            await _httpClient.PostAsync($"api/room/{roomId}/leave", null);
            await LeaveRoomGroupAsync(roomId);
        }









        public async Task SendMessageAsync(string user, string message)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("SendMessage", user, message);
            }
        }

        public async Task SendPrivateMessageAsync(string toUser, string message)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("SendPrivateMessage", toUser, message);
            }
        }

        public async Task ClearChatHistoryAsync(string partnerUser)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("ClearConversation", partnerUser);
            }
        }

        public async Task LeaveChatAsync(string partnerUser)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("LeaveChat", partnerUser);
            }
        }

        public async Task SendTypingIndicatorAsync(string sender, string receiver)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("UserTyping", receiver);
            }
        }

        public async Task SendBuzzAsync(string receiver)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("SendBuzz", receiver);
            }
        }

        /// <summary>
        /// Send a room invitation to a user via SignalR (v2.4.0)
        /// </summary>
        public async Task SendRoomInvitationAsync(string targetUsername, int roomId, string roomName, string roomCategory)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("SendRoomInvitation", targetUsername, roomId, roomName, roomCategory);
            }
        }

        public async Task MarkAudioListenedAsync(int messageId)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("MarkAudioListened", messageId);
            }
        }

        public async Task SendImageRequestAsync(string receiver, string fileUrl, string fileName, long fileSize)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("SendImageRequest", receiver, fileUrl, fileName, fileSize);
            }
        }

        public async Task RespondToImageRequestAsync(int fileId, bool isAccepted)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("RespondToImageRequest", fileId, isAccepted);
            }
        }

        public async Task<string?> UploadVideoAsync(string filePath)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var fileContent = new StreamContent(File.OpenRead(filePath));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                content.Add(fileContent, "file", Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync("api/upload/video", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (result.TryGetProperty("url", out var urlProperty))
                    {
                        return urlProperty.GetString();
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task SendVideoRequestAsync(string receiver, string fileUrl, string fileName, long fileSize)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("SendVideoRequest", receiver, fileUrl, fileName, fileSize);
            }
        }

        public async Task RespondToVideoRequestAsync(int fileId, bool isAccepted)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("RespondToVideoRequest", fileId, isAccepted);
            }
        }

        public async Task<string?> UploadAudioAsync(string filePath)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var fileContent = new StreamContent(File.OpenRead(filePath));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
                content.Add(fileContent, "file", Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync("api/upload/audio", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (result.TryGetProperty("url", out var urlProperty))
                    {
                        return urlProperty.GetString();
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task SendAudioRequestAsync(string receiver, string fileUrl, string fileName, long fileSize)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("SendAudioRequest", receiver, fileUrl, fileName, fileSize);
            }
        }

        public async Task RespondToAudioRequestAsync(int fileId, bool isAccepted)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("RespondToAudioRequest", fileId, isAccepted);
            }
        }

        public async Task<string?> UploadFileAsync(string filePath)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var fileContent = new StreamContent(File.OpenRead(filePath));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "file", Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync("api/upload/file", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (result.TryGetProperty("url", out var urlProperty))
                    {
                        return urlProperty.GetString();
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task SendFileRequestAsync(string receiver, string fileUrl, string fileName, long fileSize)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("SendFileRequest", receiver, fileUrl, fileName, fileSize);
            }
        }

        public async Task RespondToFileRequestAsync(int fileId, bool isAccepted)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("RespondToFileRequest", fileId, isAccepted);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════════════
        // KICK & BAN MANAGEMENT - v1.8.4
        // ═══════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Kick un utilisateur d'un salon (éjection temporaire sans ban)
        /// </summary>
        public async Task<KickBanResult?> KickUserAsync(int roomId, int userId, string? reason = null)
        {
            var dto = new { Reason = reason };
            var response = await _httpClient.PostAsJsonAsync($"api/room/{roomId}/kick/{userId}", dto);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<KickBanResult>();
            }
            return null;
        }

        /// <summary>
        /// Ban un utilisateur d'un salon (temporaire ou permanent)
        /// </summary>
        public async Task<KickBanResult?> BanUserAsync(int roomId, int userId, string? reason, string banType = "Permanent", int? durationMinutes = null)
        {
            var dto = new 
            { 
                Reason = reason,
                BanType = banType,
                DurationMinutes = durationMinutes
            };
            var response = await _httpClient.PostAsJsonAsync($"api/room/{roomId}/ban/{userId}", dto);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<KickBanResult>();
            }
            return null;
        }

        /// <summary>
        /// Déban un utilisateur d'un salon
        /// </summary>
        public async Task<bool> UnbanUserAsync(int roomId, int userId)
        {
            var response = await _httpClient.DeleteAsync($"api/room/{roomId}/ban/{userId}");
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// Met à jour la durée d'un bannissement
        /// </summary>
        public async Task<bool> UpdateBanAsync(int roomId, int userId, string banType, int? durationMinutes)
        {
            var dto = new { BanType = banType, DurationMinutes = durationMinutes };
            var response = await _httpClient.PutAsJsonAsync($"api/room/{roomId}/ban/{userId}", dto);
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// Récupère la liste des utilisateurs bannis d'un salon
        /// </summary>
        public async Task<List<RoomBan>> GetRoomBansAsync(int roomId)
        {
            var response = await _httpClient.GetAsync($"api/room/{roomId}/bans");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<RoomBan>>() ?? new List<RoomBan>();
            }
            return new List<RoomBan>();
        }

        /// <summary>
        /// Vérifie si un utilisateur est banni d'un salon
        /// </summary>
        public async Task<bool> IsUserBannedAsync(int roomId, int userId)
        {
            var response = await _httpClient.GetAsync($"api/room/{roomId}/ban-check/{userId}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("isBanned").GetBoolean();
            }
            return false;
        }

        public async Task MuteUserAsync(int roomId, int userId, int durationMinutes)
        {
             await _httpClient.PostAsync($"api/room/{roomId}/mute/{userId}?duration={durationMinutes}", null);
        }
        
        public async Task DisconnectAsync()
        {
            if (VoiceService != null)
            {
                try
                {
                    VoiceService.EndCall();
                }
                catch { }
            }

            if (_hubConnection != null)
            {
                _isIntentionalDisconnect = true;
                try
                {
                    await _hubConnection.StopAsync();
                }
                catch { }

                try
                {
                    await _hubConnection.DisposeAsync();
                }
                catch { }

                _hubConnection = null;
            }
        }
        public async Task<List<RoomDto>> GetRoomsAsync(int? categoryId = null)
        {
            var url = "api/room";
            if (categoryId.HasValue) url += $"?categoryId={categoryId}";
            return await _httpClient.GetFromJsonAsync<List<RoomDto>>(url) ?? new List<RoomDto>();
        }

        public async Task<List<RoomCategoryDto>> GetRoomCategoriesAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<RoomCategoryDto>>("api/room/categories") ?? new List<RoomCategoryDto>();
        }

        public async Task<List<RoomSubCategoryDto>> GetRoomSubCategoriesAsync(int categoryId)
        {
            return await _httpClient.GetFromJsonAsync<List<RoomSubCategoryDto>>($"api/room/categories/{categoryId}/subcategories") ?? new List<RoomSubCategoryDto>();
        }

        public async Task<List<RoomSubscriptionTierDto>> GetSubscriptionTiersAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<RoomSubscriptionTierDto>>("api/room/subscription-tiers") ?? new List<RoomSubscriptionTierDto>();
        }

        public async Task<List<MyRoomDto>> GetMyRoomsAsync()
        {
            return await _httpClient.GetFromJsonAsync<List<MyRoomDto>>("api/room/my-rooms") ?? new List<MyRoomDto>();
        }

        public async Task<RoomDto?> CreateRoomAsync(CreateRoomDto dto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/room", dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<RoomDto>();
            }
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }

        public async Task<bool> JoinRoomAsync(int roomId, string? password, bool isInvisible = false)
        {
            var dto = new JoinRoomDto { Password = password, IsInvisible = isInvisible };
            var response = await _httpClient.PostAsJsonAsync($"api/room/{roomId}/join", dto);
            if (response.IsSuccessStatusCode) return true;
            
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }

        public async Task DeleteRoomAsync(int roomId)
        {
            var response = await _httpClient.DeleteAsync($"api/room/{roomId}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task<RoomDto?> UpdateRoomAsync(int roomId, CreateRoomDto dto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"api/room/{roomId}", dto);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<RoomDto>();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> ToggleRoomVisibilityAsync(int roomId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/room/{roomId}/toggle-visibility", null);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (result.TryGetProperty("isActive", out var isActive))
                    {
                        return isActive.GetBoolean();
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Toggle le statut IsSystemHidden d'un salon (admin système uniquement).
        /// Quand TRUE, même le RoomOwner ne voit plus son salon.
        /// </summary>
        public async Task<bool> ToggleSystemHiddenAsync(int roomId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/room/{roomId}/toggle-system-hidden", null);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (result.TryGetProperty("isSystemHidden", out var isSystemHidden))
                    {
                        return isSystemHidden.GetBoolean();
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ApiService] ToggleSystemHiddenAsync error: {ex.Message}");
                return false;
            }
        }

        // ==================== Room Role Management ====================

        /// <summary>
        /// Récupère les rôles actuels des membres d'un salon
        /// </summary>
        public async Task<List<RoomRoleDto>> GetRoomRolesAsync(int roomId)
        {
            try
            {
                Console.WriteLine($"[ApiService] GetRoomRolesAsync called for roomId={roomId}");
                var url = $"api/room/{roomId}/roles";
                Console.WriteLine($"[ApiService] Calling API: {url}");
                
                var result = await _httpClient.GetFromJsonAsync<List<RoomRoleDto>>(url) ?? new List<RoomRoleDto>();
                
                Console.WriteLine($"[ApiService] GetRoomRolesAsync returned {result.Count} roles:");
                foreach (var r in result)
                {
                    Console.WriteLine($"[ApiService]   -> UserId={r.UserId}, Username={r.Username}, Role={r.Role}");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ApiService] GetRoomRolesAsync ERROR: {ex.Message}");
                return new List<RoomRoleDto>();
            }
        }

        /// <summary>
        /// Attribue directement un rôle à un utilisateur (SuperAdmin, Admin, Moderator)
        /// </summary>
        public async Task<ApiResult> AssignRoleAsync(int roomId, int userId, string role)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"api/room/{roomId}/roles/assign", new { UserId = userId, Role = role });
                if (response.IsSuccessStatusCode)
                {
                    return new ApiResult { Success = true, Message = "Rôle attribué avec succès" };
                }
                var error = await response.Content.ReadAsStringAsync();
                return new ApiResult { Success = false, Message = error };
            }
            catch (Exception ex)
            {
                return new ApiResult { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// Retire le rôle d'un utilisateur dans un salon
        /// </summary>
        public async Task<ApiResult> RemoveRoomRoleAsync(int roomId, int userId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/room/{roomId}/roles/{userId}");
                if (response.IsSuccessStatusCode)
                {
                    return new ApiResult { Success = true, Message = "Rôle retiré" };
                }
                var error = await response.Content.ReadAsStringAsync();
                return new ApiResult { Success = false, Message = error };
            }
            catch (Exception ex)
            {
                return new ApiResult { Success = false, Message = ex.Message };
            }
        }
    }

    public class AuthResponse
    {
        public int UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public bool IsProfileComplete { get; set; }
        public string Role { get; set; } = string.Empty;
        public int RoleLevel { get; set; }
        
        // Session control properties
        public bool IsAlreadyConnected { get; set; } = false;
        public string? ActiveSessionDevice { get; set; }
        public string? ActiveSessionIP { get; set; }
        public DateTime? ActiveSessionSince { get; set; }
    }

    public class RoomDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public int MaxUsers { get; set; }
        public bool IsPrivate { get; set; }
        public bool Is18Plus { get; set; }
        public int SubscriptionLevel { get; set; }
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Quand TRUE, le salon est caché même au RoomOwner.
        /// Seuls les admins système peuvent le voir.
        /// </summary>
        public bool IsSystemHidden { get; set; }
        
        public int UserCount { get; set; }
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Rôle de l'utilisateur connecté dans ce salon (SuperAdmin, Admin, Moderator ou null)
        /// </summary>
        public string? UserRole { get; set; }
        
        // Conditions d'entrée par défaut
        public bool DefaultTextEnabled { get; set; } = true;
        public bool DefaultMicEnabled { get; set; } = false;
        public bool DefaultCamEnabled { get; set; } = false;
    }

    public class RoomCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public int SubCategoryCount { get; set; }
    }

    public class RoomSubCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
    }

    public class RoomSubscriptionTierDto
    {
        public int Tier { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public int MaxUsers { get; set; }
        public int MaxMic { get; set; }
        public int MaxCam { get; set; }
        public bool AlwaysOnline { get; set; }
        public int MonthlyPriceCents { get; set; }
    }

    public class MyRoomDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string? TierName { get; set; }
        public string? TierColor { get; set; }
        public int UserCount { get; set; }
        public int MaxUsers { get; set; }
    }

    public class CreateRoomDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int MaxUsers { get; set; }
        public int MaxMics { get; set; } = 1;
        public int MaxCams { get; set; } = 2;
        public bool IsPrivate { get; set; }
        public string? Password { get; set; }
        public bool Is18Plus { get; set; }
        public int SubscriptionLevel { get; set; }
        
        // Conditions d'entrée par défaut
        public bool DefaultTextEnabled { get; set; } = true;
        public bool DefaultMicEnabled { get; set; } = false;
        public bool DefaultCamEnabled { get; set; } = false;
    }

    public class JoinRoomDto
    {
        public string? Password { get; set; }
        public bool IsInvisible { get; set; } = false;
    }

    public class FriendDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = "Hors ligne";
        public int StatusValue { get; set; }
        public string AvatarPath { get; set; } = string.Empty;
        public int UnreadCount { get; set; }
        public string LastMessage { get; set; } = string.Empty;
        public DateTime LastMessageTime { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool IsBlocked { get; set; }
        public int RoleLevel { get; set; }
    }

    public class UserProfileDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? AvatarPath { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }

    public class BlockRequestModel
    {
        public string BlockedUsername { get; set; } = string.Empty;
        public int BlockType { get; set; }
        public DateTime? EndDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class BlockedUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarPath { get; set; } = string.Empty;
        public int BlockType { get; set; }
        public DateTime? EndDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        public string BlockPeriodDisplay
        {
            get
            {
                if (BlockType == 0) return "Permanent";
                if (EndDate.HasValue) return $"Jusqu'au {EndDate.Value:dd/MM/yyyy}";
                return "Inconnu";
            }
        }
    }

    public class ChatMessageDto
    {
        public int Id { get; set; }
        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
    }

    /// <summary>
    /// Résultat d'une opération API
    /// </summary>
    public class ApiResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// DTO pour les rôles d'un utilisateur dans un salon
    /// </summary>
    public class RoomRoleDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = string.Empty; // RoomSuperAdmin, RoomAdmin, RoomModerator
    }

    /// <summary>
    /// DTO pour une demande de rôle en attente
    /// </summary>
    public class RoleRequestDto
    {
        public int RequestId { get; set; }
        public int RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public int RequesterId { get; set; }
        public string RequesterName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO pour les demandes de rôle en attente (côté owner)
    /// </summary>
    public class RoomRoleRequestDto
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int TargetUserId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO pour la réception d'une demande de rôle via SignalR
    /// </summary>
    public class RoleRequestReceivedDto
    {
        public int RequestId { get; set; }
        public int RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int RequesterId { get; set; }
    }

    /// <summary>
    /// Profil public d'un utilisateur (utilisé pour "Voir le profil")
    /// </summary>
    public class PublicProfileDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public DateTime? MemberSince { get; set; }
    }

    /// <summary>
    /// DTO pour les utilisateurs qui ont consulté mon profil
    /// </summary>
    public class ProfileViewerDto
    {
        public int ViewerId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public DateTime ViewedAt { get; set; }
        public string Context { get; set; } = "room";
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // BOT IA DTOs - v1.8.8
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Configuration du Bot IA pour un salon
    /// </summary>
    public class BotConfigDto
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public string BotName { get; set; } = "PaLX Bot";
        public string BotAvatarUrl { get; set; } = "/images/bot-avatar.png";
        
        public bool IsEnabled { get; set; } = true;
        public bool WelcomeMessageEnabled { get; set; } = true;
        public bool ModerationEnabled { get; set; } = true;
        public bool QuizEnabled { get; set; } = false;
        public bool MentionResponseEnabled { get; set; } = true;
        public bool TopicSuggestionEnabled { get; set; } = false;
        
        public string WelcomeMessageTemplate { get; set; } = "Bienvenue {username} dans le salon ! 👋";
        public string WarningMessageTemplate { get; set; } = "⚠️ {username}, merci de respecter les règles du salon.";
        public string KickMessageTemplate { get; set; } = "❌ {username} a été expulsé pour comportement inapproprié.";
        
        public int WarningsBeforeKick { get; set; } = 3;
        public int WarningResetMinutes { get; set; } = 60;
        
        public int QuizIntervalMinutes { get; set; } = 30;
        public int QuizTimeoutSeconds { get; set; } = 60;
    }

    /// <summary>
    /// DTO pour créer/modifier la config du bot
    /// </summary>
    public class UpdateBotConfigDto
    {
        public string? BotName { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? WelcomeMessageEnabled { get; set; }
        public bool? ModerationEnabled { get; set; }
        public bool? QuizEnabled { get; set; }
        public bool? MentionResponseEnabled { get; set; }
        public bool? TopicSuggestionEnabled { get; set; }
        
        public string? WelcomeMessageTemplate { get; set; }
        public string? WarningMessageTemplate { get; set; }
        public string? KickMessageTemplate { get; set; }
        
        public int? WarningsBeforeKick { get; set; }
        public int? WarningResetMinutes { get; set; }
        
        public int? QuizIntervalMinutes { get; set; }
        public int? QuizTimeoutSeconds { get; set; }
    }

    /// <summary>
    /// DTO pour un mot interdit
    /// </summary>
    public class BannedWordDto
    {
        public int Id { get; set; }
        public string Word { get; set; } = string.Empty;
        public string Severity { get; set; } = "Warning";
        public string AddedByUsername { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO pour ajouter un mot interdit
    /// </summary>
    public class AddBannedWordDto
    {
        public string Word { get; set; } = string.Empty;
        public string Severity { get; set; } = "Warning";
    }
}
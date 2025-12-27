using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using System.Windows;

namespace PaLX.Admin.Services
{
    public class ApiService
    {
        private static ApiService? _instance;
        public static ApiService Instance => _instance ??= new ApiService();

        private readonly HttpClient _httpClient;
        private HubConnection? _hubConnection;
        private string _authToken = string.Empty;
        private const string BaseUrl = "http://localhost:5145"; // Adjust if needed

        public event Action<string, string>? OnMessageReceived;
        public event Action<string, string>? OnPrivateMessageReceived;
        public event Action<string>? OnUserTyping;
        public event Action<string>? OnBuzzReceived;
        public event Action<string, string>? OnUserStatusChanged;

        // Friend Events
        public event Action<string>? OnFriendRequestReceived;
        public event Action<string>? OnFriendRequestAccepted;
        public event Action<string>? OnFriendRemoved;

        // Block Events
        public event Action<string>? OnUserBlocked;
        public event Action<string>? OnUserBlockedBy;
        public event Action<string>? OnUserUnblocked;
        public event Action<string>? OnUserUnblockedBy;

        // System Events
        public event Action? OnConnectionClosed;

        private ApiService()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        }

        public async Task<(AuthResponse? Response, bool IsConnectionError)> LoginAsync(string username, string password, string ip, string deviceName, string deviceNumber)
        {
            try
            {
                var model = new { Username = username, Password = password, IpAddress = ip, DeviceName = deviceName, DeviceNumber = deviceNumber, IsAdminLogin = true };
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", model);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                    if (result != null && !string.IsNullOrEmpty(result.Token))
                    {
                        _authToken = result.Token;
                        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
                        return (result, false);
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
            try
            {
                return await _httpClient.GetFromJsonAsync<List<BlockedUserDto>>("api/user/blocked") ?? new List<BlockedUserDto>();
            }
            catch { return new List<BlockedUserDto>(); }
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
                await _httpClient.PostAsJsonAsync("api/user/chat/read", partner);
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
                var response = await _httpClient.GetAsync($"api/user/search?query={query}");
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Search error ({response.StatusCode}): {error}");
                    return new List<FriendDto>();
                }
                return await response.Content.ReadFromJsonAsync<List<FriendDto>>() ?? new List<FriendDto>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search error: {ex.Message}");
                return new List<FriendDto>();
            }
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

        public async Task ConnectSignalRAsync()
        {
            if (string.IsNullOrEmpty(_authToken)) return;

            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{BaseUrl}/chatHub", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult((string?)_authToken);
                })
                .Build();

            _hubConnection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                OnMessageReceived?.Invoke(user, message);
            });

            _hubConnection.On<string, string>("ReceivePrivateMessage", (user, message) =>
            {
                OnPrivateMessageReceived?.Invoke(user, message);
            });

            _hubConnection.On<string>("UserTyping", (user) =>
            {
                OnUserTyping?.Invoke(user);
            });

            _hubConnection.On<string, string>("UserStatusChanged", (username, status) =>
            {
                OnUserStatusChanged?.Invoke(username, status);
            });

            _hubConnection.On<string>("FriendRequestReceived", (username) =>
            {
                OnFriendRequestReceived?.Invoke(username);
            });

            _hubConnection.On<string>("FriendRequestAccepted", (username) =>
            {
                OnFriendRequestAccepted?.Invoke(username);
            });

            _hubConnection.On<string>("FriendRemoved", (username) =>
            {
                OnFriendRemoved?.Invoke(username);
            });

            _hubConnection.On<string>("UserBlocked", (blockedUser) =>
            {
                OnUserBlocked?.Invoke(blockedUser);
            });

            _hubConnection.On<string>("UserBlockedBy", (blocker) =>
            {
                OnUserBlockedBy?.Invoke(blocker);
            });

            _hubConnection.On<string>("UserUnblocked", (unblockedUser) =>
            {
                OnUserUnblocked?.Invoke(unblockedUser);
            });

            _hubConnection.On<string>("UserUnblockedBy", (blocker) =>
            {
                OnUserUnblockedBy?.Invoke(blocker);
            });

            _hubConnection.On<string>("ReceiveBuzz", (sender) =>
            {
                OnBuzzReceived?.Invoke(sender);
            });

            _hubConnection.Closed += async (error) =>
            {
                OnConnectionClosed?.Invoke();
                await Task.CompletedTask;
            };

            try
            {
                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SignalR Connection error: {ex.Message}");
            }
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

        public async Task SendTypingIndicatorAsync(string toUser)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("UserTyping", toUser);
            }
        }

        public async Task SendBuzzAsync(string receiver)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("SendBuzz", receiver);
            }
        }
        
        public async Task DisconnectAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }
        }
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public bool IsProfileComplete { get; set; }
        public string Role { get; set; } = string.Empty;
        public int RoleLevel { get; set; }
    }

    public class FriendDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = "Hors ligne";
        public string AvatarPath { get; set; } = string.Empty;
        public int UnreadCount { get; set; }
        public string LastMessage { get; set; } = string.Empty;
        public DateTime LastMessageTime { get; set; }
        
        public int StatusValue { get; set; }
        public int FriendshipStatus => StatusValue;
        public string Gender { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool IsBlocked { get; set; }
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
        public string? AvatarPath { get; set; }
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
}

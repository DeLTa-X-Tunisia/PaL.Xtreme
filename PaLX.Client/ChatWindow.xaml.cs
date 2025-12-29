using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Text;
using System.Net.Http;
using Microsoft.Web.WebView2.Core;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class ChatWindow : Window
    {
        private string _currentUser;
        private string _currentUserDisplayName;
        private string _partnerUser;
        private DateTime _lastTypingSent = DateTime.MinValue;
        private System.Media.SoundPlayer? _messageSound;
        private MediaPlayer _buzzPlayer = new MediaPlayer();
        private bool _isBlocked = false;
        private bool _isBlockedByPartner = false;
        private int _lastMessageId = 0;
        private bool _isPartnerOnline = false;
        private int _partnerStatus = 6; // Default Offline
        private bool _dndOverride = false;
        private int _partnerRoleLevel = 7; // Default to User
        
        // Audio Recording
        private AudioRecorder _audioRecorder = new AudioRecorder();
        private bool _isRecording = false;
        private DispatcherTimer _recordingTimer;
        private int _recordingSeconds = 0;

        // Voice Call
        private VoiceCallService _voiceService;

        public ChatWindow(string currentUser, string partnerUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            _currentUserDisplayName = currentUser; // Default
            _partnerUser = partnerUser;

            // Init Timer
            _recordingTimer = new DispatcherTimer();
            _recordingTimer.Interval = TimeSpan.FromSeconds(1);
            _recordingTimer.Tick += RecordingTimer_Tick;

            // Init Voice Service
            _voiceService = ApiService.Instance.VoiceService!;
            // Incoming call handled by MainView

            Activated += async (s, e) => await ApiService.Instance.MarkMessagesAsReadAsync(_partnerUser);

            // Subscribe to SignalR
            ApiService.Instance.OnPrivateMessageReceived += OnPrivateMessageReceived;
            ApiService.Instance.OnAudioListened += OnAudioListened;
            ApiService.Instance.OnBuzzReceived += OnBuzzReceived;
            ApiService.Instance.OnUserStatusChanged += OnUserStatusChanged;
            
            // Image Events
            ApiService.Instance.OnImageRequestReceived += OnImageRequestReceived;
            ApiService.Instance.OnImageRequestSent += OnImageRequestSent;
            ApiService.Instance.OnImageTransferUpdated += OnImageTransferUpdated;

            // Video Events
            ApiService.Instance.OnVideoRequestReceived += OnVideoRequestReceived;
            ApiService.Instance.OnVideoRequestSent += OnVideoRequestSent;
            ApiService.Instance.OnVideoTransferUpdated += OnVideoTransferUpdated;

            // Audio Events
            ApiService.Instance.OnAudioRequestReceived += OnAudioRequestReceived;
            ApiService.Instance.OnAudioRequestSent += OnAudioRequestSent;
            ApiService.Instance.OnAudioTransferUpdated += OnAudioTransferUpdated;

            // File Events
            ApiService.Instance.OnFileRequestReceived += OnFileRequestReceived;
            ApiService.Instance.OnFileRequestSent += OnFileRequestSent;
            ApiService.Instance.OnFileTransferUpdated += OnFileTransferUpdated;

            // Load Sound
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _messageSound = new System.Media.SoundPlayer(System.IO.Path.Combine(baseDir, "Assets", "Sounds", "message.wav"));
                _messageSound.LoadAsync();
            }
            catch { }

            InitializeWebView();
            
            // Subscribe to Typing
            ApiService.Instance.OnUserTyping += OnUserTyping;

            // Subscribe to Block Events
            ApiService.Instance.OnUserBlocked += OnUserBlocked;
            ApiService.Instance.OnUserBlockedBy += OnUserBlockedBy;
            ApiService.Instance.OnUserUnblocked += OnUserUnblocked;
            ApiService.Instance.OnUserUnblockedBy += OnUserUnblockedBy;

            // Chat Cleared
            ApiService.Instance.OnChatCleared += OnChatCleared;
            ApiService.Instance.OnPartnerLeft += OnPartnerLeft;

            // Initial Load
            Loaded += async (s, e) => 
            {
                await ApiService.Instance.MarkMessagesAsReadAsync(_partnerUser);
                await LoadPartnerInfo();
                await CheckBlockStatusAsync();
                CheckBuzzAvailability();
                UpdateDndState();
            };

            Closing += async (s, e) =>
            {
                try { _audioRecorder.CancelRecording(); } catch { }
                await ApiService.Instance.LeaveChatAsync(_partnerUser);
                ApiService.Instance.OnPrivateMessageReceived -= OnPrivateMessageReceived;
                ApiService.Instance.OnAudioListened -= OnAudioListened;
                ApiService.Instance.OnBuzzReceived -= OnBuzzReceived;
                ApiService.Instance.OnUserStatusChanged -= OnUserStatusChanged;
                ApiService.Instance.OnUserTyping -= OnUserTyping;
                ApiService.Instance.OnUserBlocked -= OnUserBlocked;
                ApiService.Instance.OnUserBlockedBy -= OnUserBlockedBy;
                ApiService.Instance.OnUserUnblocked -= OnUserUnblocked;
                ApiService.Instance.OnUserUnblockedBy -= OnUserUnblockedBy;
                ApiService.Instance.OnChatCleared -= OnChatCleared;
                ApiService.Instance.OnPartnerLeft -= OnPartnerLeft;
            };
        }

        private void OnIncomingCall(string sender)
        {
            if (sender == _partnerUser)
            {
                Dispatcher.Invoke(() => 
                {
                    var win = new VoiceCallWindow(_voiceService, sender, true);
                    win.Show();
                });
            }
        }

        private void AudioCall_Click(object sender, RoutedEventArgs e)
        {
            if (_isBlocked || _isBlockedByPartner)
            {
                MessageBox.Show("Impossible d'appeler cet utilisateur.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            var win = new VoiceCallWindow(_voiceService, _partnerUser, false);
            win.Show();
            _voiceService.RequestCall(_partnerUser);
        }

        private void OnImageRequestReceived(int id, string sender, string filename, string url)
        {
            if (sender == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    // Play Sound
                    try { _messageSound?.Play(); } catch { }
                    
                    string safeUrl = url.Replace("\\", "\\\\").Replace("'", "\\'");
                    string safeFilename = filename.Replace("\\", "\\\\").Replace("'", "\\'");
                    string script = $"addFileRequest({id}, '{sender}', '{safeFilename}', '{safeUrl}', false);";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnImageRequestSent(int id, string receiver, string filename, string url)
        {
            if (receiver == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    string safeUrl = url.Replace("\\", "\\\\").Replace("'", "\\'");
                    string safeFilename = filename.Replace("\\", "\\\\").Replace("'", "\\'");
                    string script = $"addFileRequest({id}, '{_currentUser}', '{safeFilename}', '{safeUrl}', true);";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnImageTransferUpdated(int id, bool isAccepted, string url)
        {
            Dispatcher.Invoke(() =>
            {
                // Determine if it's mine or theirs based on context? 
                // Actually the JS function handles updating the UI regardless of who owns it if we pass the right flag
                // But here we don't know easily if it's mine or theirs without tracking.
                // However, the UI update logic in JS is split by ID.
                // Let's try to update both sides just in case, or rely on the fact that the ID is unique.
                // Wait, the JS `updateFileStatus` needs `isMine`.
                // We can infer `isMine` if we track pending requests, but for simplicity:
                // We will try to update assuming it might be either.
                // Actually, the simplest way is to check if the element exists in DOM as mine or theirs.
                // But we can't check DOM easily.
                // Let's just pass a flag to JS to try updating both.
                
                // Better approach: The JS function `updateFileStatus` takes `isMine`.
                // We can just call it twice or modify JS to find the element.
                // Let's modify JS to find element by ID `file-{id}` and check class.
                
                // For now, let's assume we can just call a smart JS function.
                // I'll update the JS to be smarter in the InitializeWebView method.
                // But since I already wrote the JS, let's see.
                // The JS `updateFileStatus` takes `isMine`.
                // I will call it with true AND false, one will work.
                
                string script1 = $"updateFileStatus({id}, {isAccepted.ToString().ToLower()}, true);";
                string script2 = $"updateFileStatus({id}, {isAccepted.ToString().ToLower()}, false);";
                ChatWebView.ExecuteScriptAsync(script1);
                ChatWebView.ExecuteScriptAsync(script2);
            });
        }

        private void OnVideoRequestReceived(int id, string sender, string filename, string url)
        {
            if (sender == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    try { _messageSound?.Play(); } catch { }
                    string safeUrl = url.Replace("\\", "\\\\").Replace("'", "\\'");
                    string safeFilename = filename.Replace("\\", "\\\\").Replace("'", "\\'");
                    string script = $"addVideoRequest({id}, '{sender}', '{safeFilename}', '{safeUrl}', false);";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnVideoRequestSent(int id, string receiver, string filename, string url)
        {
            if (receiver == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    string safeUrl = url.Replace("\\", "\\\\").Replace("'", "\\'");
                    string safeFilename = filename.Replace("\\", "\\\\").Replace("'", "\\'");
                    string script = $"addVideoRequest({id}, '{_currentUser}', '{safeFilename}', '{safeUrl}', true);";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnVideoTransferUpdated(int id, bool isAccepted, string url)
        {
            Dispatcher.Invoke(() =>
            {
                string script1 = $"updateVideoStatus({id}, {isAccepted.ToString().ToLower()}, true);";
                string script2 = $"updateVideoStatus({id}, {isAccepted.ToString().ToLower()}, false);";
                ChatWebView.ExecuteScriptAsync(script1);
                ChatWebView.ExecuteScriptAsync(script2);
            });
        }

        private void OnAudioRequestReceived(int id, string sender, string filename, string url)
        {
            if (sender == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    try { _messageSound?.Play(); } catch { }
                    string safeUrl = url.Replace("\\", "\\\\").Replace("'", "\\'");
                    string safeFilename = filename.Replace("\\", "\\\\").Replace("'", "\\'");
                    string script = $"addAudioRequest({id}, '{sender}', '{safeFilename}', '{safeUrl}', false);";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnAudioRequestSent(int id, string receiver, string filename, string url)
        {
            if (receiver == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    string safeUrl = url.Replace("\\", "\\\\").Replace("'", "\\'");
                    string safeFilename = filename.Replace("\\", "\\\\").Replace("'", "\\'");
                    string script = $"addAudioRequest({id}, '{_currentUser}', '{safeFilename}', '{safeUrl}', true);";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnAudioTransferUpdated(int id, bool isAccepted, string url)
        {
            Dispatcher.Invoke(() =>
            {
                string script1 = $"updateAudioStatus({id}, {isAccepted.ToString().ToLower()}, true);";
                string script2 = $"updateAudioStatus({id}, {isAccepted.ToString().ToLower()}, false);";
                ChatWebView.ExecuteScriptAsync(script1);
                ChatWebView.ExecuteScriptAsync(script2);
            });
        }

        private void OnFileRequestReceived(int id, string sender, string filename, string url)
        {
            if (sender == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    try { _messageSound?.Play(); } catch { }
                    string safeUrl = url.Replace("\\", "\\\\").Replace("'", "\\'");
                    string safeFilename = filename.Replace("\\", "\\\\").Replace("'", "\\'");
                    string script = $"addGenericFileRequest({id}, '{sender}', '{safeFilename}', '{safeUrl}', false);";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnFileRequestSent(int id, string receiver, string filename, string url)
        {
            if (receiver == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    string safeUrl = url.Replace("\\", "\\\\").Replace("'", "\\'");
                    string safeFilename = filename.Replace("\\", "\\\\").Replace("'", "\\'");
                    string script = $"addGenericFileRequest({id}, '{_currentUser}', '{safeFilename}', '{safeUrl}', true);";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnFileTransferUpdated(int id, bool isAccepted, string url)
        {
            Dispatcher.Invoke(() =>
            {
                string script1 = $"updateGenericFileStatus({id}, {isAccepted.ToString().ToLower()}, true);";
                string script2 = $"updateGenericFileStatus({id}, {isAccepted.ToString().ToLower()}, false);";
                ChatWebView.ExecuteScriptAsync(script1);
                ChatWebView.ExecuteScriptAsync(script2);
            });
        }

        private void OnUserStatusChanged(string username, string status)
        {
            if (username == _partnerUser)
            {
                Dispatcher.Invoke(() => 
                {
                    PartnerStatus.Text = status;
                    // Update online status tracking
                    _isPartnerOnline = status == "En ligne";
                    CheckBuzzAvailability();

                    // Update partner status int for DND logic
                    _partnerStatus = status switch
                    {
                        "En ligne" => 0,
                        "Occupé" => 1,
                        "Absent" => 2,
                        "En appel" => 3,
                        "Ne pas déranger" => 4,
                        _ => 6
                    };
                    UpdateDndState();

                    // Add status message to chat
                    string cssClass = "status-offline";
                    string displayName = PartnerName.Text; // Use Display Name
                    string message = $"Ton ami {displayName} est passé en {status}";

                    switch (status)
                    {
                        case "En ligne": 
                            cssClass = "status-online"; 
                            message = $"Ton ami {displayName} est de retour En ligne";
                            break;
                        case "Occupé": 
                        case "En appel":
                        case "Ne pas déranger":
                            cssClass = "status-busy"; 
                            break;
                        case "Absent": 
                            cssClass = "status-away"; 
                            break;
                        default: // Hors ligne
                            cssClass = "status-offline"; 
                            break;
                    }

                    string script = $"addStatusMessage('{message}', '{cssClass}');";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void CheckBuzzAvailability()
        {
            // Buzz is available if partner is online (and we are online, implied by being connected)
            // Assuming "En ligne" is the status string for online
            bool isOnline = PartnerStatus.Text == "En ligne";
            BtnBuzz.Visibility = isOnline ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void Buzz_Click(object sender, RoutedEventArgs e)
        {
            if (_isBlocked || _isBlockedByPartner) return;

            // Play sound locally
            PlayBuzzSound();

            // Show message locally (Blue)
            string msg = "======== Vous avez envoyé un BUZZ à " + PartnerName.Text + " ========";
            string script = $"addStatusMessage('{msg}', 'status-buzz-sent');"; // Need CSS for this
            await ChatWebView.ExecuteScriptAsync(script);

            // Send to partner
            await ApiService.Instance.SendBuzzAsync(_partnerUser);
        }

        private void OnBuzzReceived(string sender)
        {
            if (sender == _partnerUser)
            {
                TriggerBuzz();
            }
        }

        public void TriggerBuzz()
        {
            Dispatcher.Invoke(() => 
            {
                // Shake Window
                ShakeWindow();

                // Play Sound
                PlayBuzzSound();

                // Show message (Orange)
                string name = string.IsNullOrEmpty(PartnerName.Text) ? _partnerUser : PartnerName.Text;
                string msg = $"======== {name} vous a envoyé un BUZZ ========";
                string script = $"addStatusMessage('{msg}', 'status-buzz-received');";
                ChatWebView.ExecuteScriptAsync(script);
            });
        }

        private void PlayBuzzSound()
        {
            try
            {
                string soundPath = @"C:\Users\azizi\OneDrive\Desktop\PaL.Xtreme\start_sound\doorbell_1.mp3";
                if (System.IO.File.Exists(soundPath))
                {
                    _buzzPlayer.Open(new Uri(soundPath));
                    _buzzPlayer.Play();
                }
            }
            catch { }
        }

        private async void ShakeWindow()
        {
            var originalLeft = this.Left;
            var originalTop = this.Top;
            var shakeTimer = new DispatcherTimer();
            shakeTimer.Interval = TimeSpan.FromMilliseconds(50);
            int shakes = 0;
            var rnd = new Random();

            shakeTimer.Tick += (s, e) => 
            {
                if (shakes < 40) // 2 seconds approx (40 * 50ms = 2000ms)
                {
                    this.Left = originalLeft + rnd.Next(-10, 11);
                    this.Top = originalTop + rnd.Next(-10, 11);
                    shakes++;
                }
                else
                {
                    shakeTimer.Stop();
                    this.Left = originalLeft;
                    this.Top = originalTop;
                }
            };
            shakeTimer.Start();
        }

        private void OnUserBlocked(string blockedUser)
        {
            if (blockedUser == _partnerUser)
            {
                Dispatcher.Invoke(() => 
                {
                    _isBlocked = true;
                    UpdateBlockUi();

                    string html = $@"
                        <div style='font-size: 20px;'>🔒</div>
                        <div>
                            <div style='font-size: 14px;'>Vous avez bloqué {PartnerName.Text}</div>
                            <div style='font-size: 11px; opacity: 0.7; font-weight: normal; margin-top: 2px;'>Blocage permanent • {DateTime.Now:HH:mm}</div>
                        </div>";
                    string script = $"addStatusMessage(\"{html.Replace("\r\n", "").Replace("\"", "'")}\", 'status-blocked');";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnUserBlockedBy(string blocker)
        {
            if (blocker == _partnerUser)
            {
                Dispatcher.Invoke(() => 
                {
                    _isBlockedByPartner = true;
                    UpdateBlockUi();

                    string html = $@"
                        <div style='font-size: 20px;'>🔒</div>
                        <div>
                            <div style='font-size: 14px;'>{PartnerName.Text} vous a bloqué</div>
                            <div style='font-size: 11px; opacity: 0.7; font-weight: normal; margin-top: 2px;'>Blocage permanent • {DateTime.Now:HH:mm}</div>
                        </div>";
                    string script = $"addStatusMessage(\"{html.Replace("\r\n", "").Replace("\"", "'")}\", 'status-blocked');";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnUserUnblocked(string unblockedUser)
        {
            if (unblockedUser == _partnerUser)
            {
                Dispatcher.Invoke(() => 
                {
                    _isBlocked = false;
                    UpdateBlockUi();

                    string html = $@"
                        <div style='font-size: 20px;'>🔓</div>
                        <div>
                            <div style='font-size: 14px;'>Vous avez débloqué {PartnerName.Text}</div>
                            <div style='font-size: 11px; opacity: 0.7; font-weight: normal; margin-top: 2px;'>Accès rétabli • {DateTime.Now:HH:mm}</div>
                        </div>";
                    string script = $"addStatusMessage(\"{html.Replace("\r\n", "").Replace("\"", "'")}\", 'status-unblocked');";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void OnUserUnblockedBy(string blocker)
        {
            if (blocker == _partnerUser)
            {
                Dispatcher.Invoke(() => 
                {
                    _isBlockedByPartner = false;
                    UpdateBlockUi();

                    string html = $@"
                        <div style='font-size: 20px;'>🔓</div>
                        <div>
                            <div style='font-size: 14px;'>{PartnerName.Text} vous a débloqué</div>
                            <div style='font-size: 11px; opacity: 0.7; font-weight: normal; margin-top: 2px;'>Accès rétabli • {DateTime.Now:HH:mm}</div>
                        </div>";
                    string script = $"addStatusMessage(\"{html.Replace("\r\n", "").Replace("\"", "'")}\", 'status-unblocked');";
                    ChatWebView.ExecuteScriptAsync(script);
                });
            }
        }

        private void UpdateBlockUi()
        {
            if (_isBlocked || _isBlockedByPartner)
            {
                MessageInput.IsEnabled = false;
                SendButton.IsEnabled = false;
                AttachmentButton.IsEnabled = false;
                AudioButton.IsEnabled = false;
                EmojiButton.IsEnabled = false;
                
                if (_isBlocked)
                {
                    BlockButton.ToolTip = "Débloquer cet utilisateur";
                    BlockIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                    TypingIndicator.Text = $"Vous avez bloqué {PartnerName.Text}.";
                }
                else
                {
                    BlockButton.Visibility = Visibility.Collapsed;
                    TypingIndicator.Text = $"{PartnerName.Text} vous a bloqué.";
                }
                
                TypingIndicator.Visibility = Visibility.Visible;
                TypingIndicator.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
            }
            else
            {
                MessageInput.IsEnabled = true;
                SendButton.IsEnabled = true;
                AttachmentButton.IsEnabled = true;
                AudioButton.IsEnabled = true;
                EmojiButton.IsEnabled = true;

                BlockButton.Visibility = Visibility.Visible;
                BlockButton.ToolTip = "Bloquer cet utilisateur";
                BlockIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                
                TypingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void OnPartnerLeft(string partnerUser)
        {
            if (partnerUser == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    _dndOverride = false;
                    UpdateDndState();
                });
            }
        }

        private bool CanBypassDND()
        {
            int myRole = ApiService.Instance.CurrentUserRoleLevel;
            int partnerRole = _partnerRoleLevel;

            // User (7) can never bypass DND
            if (myRole == 7) return false;

            // Higher or equal role (lower number) can bypass
            return myRole <= partnerRole;
        }

        private void UpdateDndState()
        {
            // Status 4 = Ne pas déranger
            // Block if DND AND No Override AND Cannot Bypass
            if (_partnerStatus == 4 && !_dndOverride && !CanBypassDND())
            {
                MessageInput.IsEnabled = false;
                SendButton.IsEnabled = false;
                AttachmentButton.IsEnabled = false;
                AudioButton.IsEnabled = false;
                EmojiButton.IsEnabled = false;
                
                // Show DND Message
                TypingIndicator.Visibility = Visibility.Visible;
                TypingIndicator.Text = $"{PartnerName.Text} est en mode == NE PAS DÉRANGER == veuillez respecter ça et réessayer plus tard.";
                TypingIndicator.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                TypingIndicator.FontWeight = FontWeights.Bold;
            }
            else
            {
                if (!_isBlocked && !_isBlockedByPartner)
                {
                    MessageInput.IsEnabled = true;
                    SendButton.IsEnabled = true;
                    AttachmentButton.IsEnabled = true;
                    AudioButton.IsEnabled = true;
                    EmojiButton.IsEnabled = true;
                    
                    TypingIndicator.Visibility = Visibility.Collapsed;
                    TypingIndicator.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"));
                    TypingIndicator.FontWeight = FontWeights.Normal;
                }
            }
        }

        private void OnUserTyping(string user)
        {
            if (user == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    TypingIndicator.Visibility = Visibility.Visible;
                    string name = PartnerName.Text.Split(' ')[0];
                    TypingIndicator.Text = $"{name} est en train d'écrire...";
                    
                    // Hide after 3 seconds
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    timer.Tick += (s, args) => 
                    {
                        TypingIndicator.Visibility = Visibility.Collapsed;
                        timer.Stop();
                    };
                    timer.Start();
                });
            }
        }

        private void OnChatCleared(string partnerUser)
        {
            if (partnerUser == _partnerUser || partnerUser == _currentUser)
            {
                Dispatcher.Invoke(async () =>
                {
                    await ChatWebView.ExecuteScriptAsync("clearChat();");
                });
            }
        }

        private async void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ClearHistoryWindow();
            dialog.Owner = this;
            dialog.ShowDialog();

            if (dialog.IsConfirmed)
            {
                await ApiService.Instance.ClearChatHistoryAsync(_partnerUser);
            }
        }

        private async Task LoadPartnerInfo()
        {
            // Load my profile for display name
            var myProfile = await ApiService.Instance.GetUserProfileAsync(_currentUser);
            if (myProfile != null) _currentUserDisplayName = $"{myProfile.LastName} {myProfile.FirstName}";

            // Load partner details (avatar, status)
            var profile = await ApiService.Instance.GetUserProfileAsync(_partnerUser);
            if (profile != null)
            {
                string fullName = $"{profile.LastName} {profile.FirstName}".Trim();
                PartnerName.Text = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(fullName.ToLower());
                
                // Set Avatar
                if (!string.IsNullOrEmpty(profile.AvatarPath) && System.IO.File.Exists(profile.AvatarPath))
                {
                    try
                    {
                        AvatarBrush.ImageSource = new BitmapImage(new Uri(profile.AvatarPath, UriKind.Absolute));
                    }
                    catch { /* Keep default */ }
                }
            }
            else
            {
                PartnerName.Text = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(_partnerUser.ToLower());
            }

            // Get Status from Friends list (as Profile doesn't have status)
            var friends = await ApiService.Instance.GetFriendsAsync();
            var friend = friends.FirstOrDefault(f => f.Username == _partnerUser);
            if (friend != null)
            {
                PartnerStatus.Text = friend.Status;
                var statusColor = GetStatusColor(friend.Status);
                PartnerStatus.Foreground = statusColor;
                StatusIndicator.Fill = statusColor;
                
                _partnerRoleLevel = friend.RoleLevel;

                // Map string status to int for DND check
                _partnerStatus = friend.Status switch
                {
                    "En ligne" => 0,
                    "Occupé" => 1,
                    "Absent" => 2,
                    "En appel" => 3,
                    "Ne pas déranger" => 4,
                    _ => 6
                };
            }
        }

        private void OnPrivateMessageReceived(string sender, string message, int id)
        {
            if (sender == _partnerUser)
            {
                Dispatcher.Invoke(async () => 
                {
                    // If partner writes to us, DND override is activated
                    _dndOverride = true;
                    UpdateDndState();

                    var msg = new ChatMessage 
                    { 
                        Id = id,
                        Content = message, 
                        IsMine = false, 
                        Timestamp = DateTime.Now 
                    };
                    AppendMessageToUi(msg);
                    
                    if (!this.IsActive)
                    {
                        _messageSound?.Play();
                    }
                    else
                    {
                        // Mark as read immediately if window is active
                        await ApiService.Instance.MarkMessagesAsReadAsync(_partnerUser);
                    }
                });
            }
        }

        private void OnAudioListened(int messageId)
        {
            Dispatcher.Invoke(() =>
            {
                ChatWebView.CoreWebView2.PostWebMessageAsJson($"{{\"type\": \"audioListened\", \"id\": {messageId}}}");
            });
        }

        private void RecordingTimer_Tick(object sender, EventArgs e)
        {
            _recordingSeconds++;
            if (_recordingSeconds >= 180) // 3 minutes
            {
                StopRecordingAndSend();
            }
            else
            {
                AudioButton.ToolTip = $"Enregistrement... {TimeSpan.FromSeconds(_recordingSeconds):mm\\:ss}";
            }
        }

        private void AudioButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecording)
            {
                // Start Recording
                _isRecording = true;
                _recordingSeconds = 0;
                _audioRecorder.StartRecording();
                _recordingTimer.Start();
                
                // Visual Feedback
                AudioButton.Background = new SolidColorBrush(Color.FromRgb(255, 82, 82)); // Red
                AudioButton.ToolTip = "Enregistrement en cours... (Cliquer pour arrêter)";
            }
            else
            {
                StopRecordingAndSend();
            }
        }

        private async void StopRecordingAndSend()
        {
            if (!_isRecording) return;

            _isRecording = false;
            _recordingTimer.Stop();
            string filePath = _audioRecorder.StopRecording();
            
            // Reset UI
            AudioButton.Background = Brushes.Transparent;
            AudioButton.ToolTip = "Message Audio";

            if (System.IO.File.Exists(filePath))
            {
                // Upload
                string url = await ApiService.Instance.UploadAudioAsync(filePath);
                if (!string.IsNullOrEmpty(url))
                {
                    // Ensure Absolute URL
                    if (!url.StartsWith("http"))
                    {
                        url = ApiService.BaseUrl + (url.StartsWith("/") ? "" : "/") + url;
                    }

                    // Send Message
                    string content = $"[AUDIO_MSG]{url}|{_recordingSeconds}";
                    await ApiService.Instance.SendPrivateMessageAsync(_partnerUser, content);
                    
                    // Add to UI immediately (Sender side)
                    // Note: We don't have the ID yet, so we can't track read receipt for this specific instance until refresh
                    // But that's fine for the sender view initially.
                    var msg = new ChatMessage 
                    { 
                        Content = content, 
                        IsMine = true, 
                        Timestamp = DateTime.Now 
                    };
                    AppendMessageToUi(msg);
                }
                
                // Cleanup
                try { System.IO.File.Delete(filePath); } catch { }
            }
        }

        private SolidColorBrush GetStatusColor(string status)
        {
            return status?.ToLower() switch
            {
                "en ligne" => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                "occupé" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),   // Red
                "absent" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),   // Orange
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))         // Gray
            };
        }

        private async void AttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Tous les fichiers supportés|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp;*.mp4;*.avi;*.mov;*.wmv;*.mkv;*.webm;*.mp3;*.wav;*.ogg;*.m4a;*.aac;*.wma;*.flac;*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.zip;*.rar",
                Title = "Sélectionner un fichier"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                var fileInfo = new System.IO.FileInfo(filePath);
                string ext = fileInfo.Extension.ToLower();
                
                bool isVideo = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".webm" }.Contains(ext);
                bool isImage = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" }.Contains(ext);
                bool isAudio = new[] { ".mp3", ".wav", ".ogg", ".m4a", ".aac", ".wma", ".flac" }.Contains(ext);

                if (isVideo)
                {
                    string? url = await ApiService.Instance.UploadVideoAsync(filePath);
                    if (!string.IsNullOrEmpty(url))
                    {
                        string fullUrl = $"{ApiService.BaseUrl}{url}";
                        try { await ApiService.Instance.SendVideoRequestAsync(_partnerUser, fullUrl, fileInfo.Name, fileInfo.Length); }
                        catch (Exception ex) { MessageBox.Show($"Erreur: {ex.Message}"); }
                    }
                    else
                    {
                        MessageBox.Show("Échec du téléchargement de la vidéo.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (isAudio)
                {
                    string? url = await ApiService.Instance.UploadAudioAsync(filePath);
                    if (!string.IsNullOrEmpty(url))
                    {
                        string fullUrl = $"{ApiService.BaseUrl}{url}";
                        try { await ApiService.Instance.SendAudioRequestAsync(_partnerUser, fullUrl, fileInfo.Name, fileInfo.Length); }
                        catch (Exception ex) { MessageBox.Show($"Erreur: {ex.Message}"); }
                    }
                    else
                    {
                        MessageBox.Show("Échec du téléchargement de l'audio.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (isImage)
                {
                    string? url = await ApiService.Instance.UploadImageAsync(filePath);
                    if (!string.IsNullOrEmpty(url))
                    {
                        string fullUrl = $"{ApiService.BaseUrl}{url}";
                        try { await ApiService.Instance.SendImageRequestAsync(_partnerUser, fullUrl, fileInfo.Name, fileInfo.Length); }
                        catch (Exception ex) { MessageBox.Show($"Erreur: {ex.Message}"); }
                    }
                    else
                    {
                        MessageBox.Show("Échec du téléchargement de l'image.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Generic File
                    string? url = await ApiService.Instance.UploadFileAsync(filePath);
                    if (!string.IsNullOrEmpty(url))
                    {
                        string fullUrl = $"{ApiService.BaseUrl}{url}";
                        try { await ApiService.Instance.SendFileRequestAsync(_partnerUser, fullUrl, fileInfo.Name, fileInfo.Length); }
                        catch (Exception ex) { MessageBox.Show($"Erreur: {ex.Message}"); }
                    }
                    else
                    {
                        MessageBox.Show("Échec du téléchargement du fichier.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void InitializeWebView()
        {
            try
            {
                // Ensure the environment is ready
                await ChatWebView.EnsureCoreWebView2Async();
                
                // Disable Default Context Menus and DevTools
                ChatWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                ChatWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                ChatWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                // Handle Downloads Silently (Hide Default Dialog)
                ChatWebView.CoreWebView2.DownloadStarting += (s, args) =>
                {
                    args.Handled = true; // Hide the default download dialog
                };
                
                // Map Assets folder for Smileys
                string assetsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
                ChatWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "assets", assetsPath, CoreWebView2HostResourceAccessKind.Allow);
                
                // Handle Web Messages (Accept/Decline/OpenImage/SaveImage)
                ChatWebView.CoreWebView2.WebMessageReceived += async (s, args) =>
                {
                    try
                    {
                        string json = args.TryGetWebMessageAsString();
                        var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        
                        if (data != null && data.ContainsKey("type"))
                        {
                            string type = data["type"];
                            if (type == "openImage" && data.ContainsKey("url"))
                            {
                                try 
                                {
                                    // Download to Temp and Open
                                    string url = data["url"];
                                    string ext = System.IO.Path.GetExtension(url);
                                    if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                                    string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PaLX_View_{Guid.NewGuid()}{ext}");
                                    
                                    using (var client = new HttpClient())
                                    {
                                        var bytes = await client.GetByteArrayAsync(url);
                                        await System.IO.File.WriteAllBytesAsync(tempFile, bytes);
                                    }

                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = tempFile,
                                        UseShellExecute = true
                                    });
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Impossible d'ouvrir l'image : " + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            else if (type == "saveImage" && data.ContainsKey("url"))
                            {
                                try
                                {
                                    string url = data["url"];
                                    string fileName = data.ContainsKey("filename") ? data["filename"] : "image.jpg";
                                    string downloadsPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                                    string filePath = System.IO.Path.Combine(downloadsPath, fileName);

                                    // Ensure unique filename
                                    int count = 1;
                                    string fileNameOnly = System.IO.Path.GetFileNameWithoutExtension(filePath);
                                    string extension = System.IO.Path.GetExtension(filePath);
                                    while (System.IO.File.Exists(filePath))
                                    {
                                        filePath = System.IO.Path.Combine(downloadsPath, $"{fileNameOnly} ({count++}){extension}");
                                    }

                                    using (var client = new HttpClient())
                                    {
                                        var bytes = await client.GetByteArrayAsync(url);
                                        await System.IO.File.WriteAllBytesAsync(filePath, bytes);
                                    }

                                    var dialog = new DownloadCompleteWindow();
                                    if (dialog.ShowDialog() == true && dialog.ShouldOpen)
                                    {
                                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                        {
                                            FileName = filePath,
                                            UseShellExecute = true
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Erreur lors du téléchargement : " + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            else if (type == "respondImage" && data.ContainsKey("id") && data.ContainsKey("accepted"))
                            {
                                int id = int.Parse(data["id"]);
                                bool accepted = bool.Parse(data["accepted"]);
                                await ApiService.Instance.RespondToImageRequestAsync(id, accepted);
                            }
                            else if (type == "openVideo" && data.ContainsKey("url"))
                            {
                                try 
                                {
                                    string url = data["url"];
                                    string ext = System.IO.Path.GetExtension(url);
                                    if (string.IsNullOrEmpty(ext)) ext = ".mp4";
                                    string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PaLX_Video_{Guid.NewGuid()}{ext}");
                                    
                                    using (var client = new HttpClient())
                                    {
                                        var bytes = await client.GetByteArrayAsync(url);
                                        await System.IO.File.WriteAllBytesAsync(tempFile, bytes);
                                    }

                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = tempFile,
                                        UseShellExecute = true
                                    });
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Impossible d'ouvrir la vidéo : " + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            else if (type == "saveVideo" && data.ContainsKey("url"))
                            {
                                try
                                {
                                    string url = data["url"];
                                    string fileName = data.ContainsKey("filename") ? data["filename"] : "video.mp4";
                                    string downloadsPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                                    string filePath = System.IO.Path.Combine(downloadsPath, fileName);

                                    int count = 1;
                                    string fileNameOnly = System.IO.Path.GetFileNameWithoutExtension(filePath);
                                    string extension = System.IO.Path.GetExtension(filePath);
                                    while (System.IO.File.Exists(filePath))
                                    {
                                        filePath = System.IO.Path.Combine(downloadsPath, $"{fileNameOnly} ({count++}){extension}");
                                    }

                                    using (var client = new HttpClient())
                                    {
                                        var bytes = await client.GetByteArrayAsync(url);
                                        await System.IO.File.WriteAllBytesAsync(filePath, bytes);
                                    }

                                    var dialog = new DownloadCompleteWindow();
                                    if (dialog.ShowDialog() == true && dialog.ShouldOpen)
                                    {
                                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                        {
                                            FileName = filePath,
                                            UseShellExecute = true
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Erreur lors du téléchargement : " + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            else if (type == "respondVideo" && data.ContainsKey("id") && data.ContainsKey("accepted"))
                            {
                                int id = int.Parse(data["id"]);
                                bool accepted = bool.Parse(data["accepted"]);
                                await ApiService.Instance.RespondToVideoRequestAsync(id, accepted);
                            }
                            else if (type == "respondAudio" && data.ContainsKey("id") && data.ContainsKey("accepted"))
                            {
                                int id = int.Parse(data["id"]);
                                bool accepted = bool.Parse(data["accepted"]);
                                await ApiService.Instance.RespondToAudioRequestAsync(id, accepted);
                            }
                            else if (type == "audioPlayed" && data.ContainsKey("id"))
                            {
                                int id = int.Parse(data["id"]);
                                await ApiService.Instance.MarkAudioListenedAsync(id);
                            }
                            else if (type == "saveAudio" && data.ContainsKey("url"))
                            {
                                try
                                {
                                    string url = data["url"];
                                    string fileName = data.ContainsKey("filename") ? data["filename"] : "audio.mp3";
                                    string downloadsPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                                    string filePath = System.IO.Path.Combine(downloadsPath, fileName);

                                    int count = 1;
                                    string fileNameOnly = System.IO.Path.GetFileNameWithoutExtension(filePath);
                                    string extension = System.IO.Path.GetExtension(filePath);
                                    while (System.IO.File.Exists(filePath))
                                    {
                                        filePath = System.IO.Path.Combine(downloadsPath, $"{fileNameOnly} ({count++}){extension}");
                                    }

                                    using (var client = new HttpClient())
                                    {
                                        var bytes = await client.GetByteArrayAsync(url);
                                        await System.IO.File.WriteAllBytesAsync(filePath, bytes);
                                    }

                                    var dialog = new DownloadCompleteWindow();
                                    if (dialog.ShowDialog() == true && dialog.ShouldOpen)
                                    {
                                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                        {
                                            FileName = filePath,
                                            UseShellExecute = true
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Erreur lors du téléchargement : " + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            else if (type == "respondFile" && data.ContainsKey("id") && data.ContainsKey("accepted"))
                            {
                                int id = int.Parse(data["id"]);
                                bool accepted = bool.Parse(data["accepted"]);
                                await ApiService.Instance.RespondToFileRequestAsync(id, accepted);
                            }
                        }
                    }
                    catch { }
                };

                string html = @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: 'Segoe UI', sans-serif; background-color: #f5f5f5; margin: 0; padding: 10px; overflow-x: hidden; }
        .message { display: flex; margin-bottom: 10px; animation: fadeIn 0.3s ease; }
        .message.mine { justify-content: flex-end; }
        .bubble { max-width: 75%; padding: 10px 15px; border-radius: 18px; position: relative; word-wrap: break-word; box-shadow: 0 1px 2px rgba(0,0,0,0.1); }
        .mine .bubble { background-color: #E3F2FD; color: black; border-bottom-right-radius: 4px; }
        .theirs .bubble { background-color: white; color: black; border: 1px solid #e0e0e0; border-bottom-left-radius: 4px; }
        .timestamp { font-size: 10px; margin-top: 4px; opacity: 0.7; text-align: right; }
        .status-message { text-align: center; margin: 15px 0; font-size: 12px; font-weight: 600; opacity: 0.9; animation: fadeIn 0.5s ease; }
        .status-online { color: #4CAF50; }
        .status-busy { color: #F44336; }
        .status-away { color: #FF9800; }
        .status-offline { color: #9E9E9E; }
        .status-blocked { 
            color: #D32F2F; 
            background: linear-gradient(to right, rgba(255, 235, 238, 0.95), rgba(255, 255, 255, 0.8));
            border-left: 4px solid #D32F2F;
            padding: 12px 20px;
            margin: 20px 0;
            border-radius: 4px;
            font-family: 'Segoe UI Semibold', sans-serif;
            display: flex;
            align-items: center;
            gap: 12px;
            box-shadow: 0 2px 8px rgba(211, 47, 47, 0.1);
            animation: slideIn 0.4s ease-out;
            text-align: left;
        }
        .status-unblocked { 
            color: #2E7D32; 
            background: linear-gradient(to right, rgba(232, 245, 233, 0.95), rgba(255, 255, 255, 0.8));
            border-left: 4px solid #2E7D32;
            padding: 12px 20px;
            margin: 20px 0;
            border-radius: 4px;
            font-family: 'Segoe UI Semibold', sans-serif;
            display: flex;
            align-items: center;
            gap: 12px;
            box-shadow: 0 2px 8px rgba(46, 125, 50, 0.1);
            animation: slideIn 0.4s ease-out;
            text-align: left;
        }
        @keyframes slideIn { from { opacity: 0; transform: translateX(-10px); } to { opacity: 1; transform: translateX(0); } }
        .smiley { width: 40px; height: 40px; vertical-align: middle; margin: 0 2px; }
        
        /* File Transfer Styles */
        .file-request { background-color: #FFF3E0; border: 1px solid #FFB74D; padding: 10px; border-radius: 8px; text-align: center; }
        .file-actions { margin-top: 10px; display: flex; justify-content: center; gap: 10px; }
        .btn-accept { background-color: #4CAF50; color: white; border: none; padding: 5px 10px; border-radius: 4px; cursor: pointer; }
        .btn-decline { background-color: #F44336; color: white; border: none; padding: 5px 10px; border-radius: 4px; cursor: pointer; }
        .file-thumb { width: 200px; height: 200px; object-fit: cover; border-radius: 8px; cursor: pointer; border: 1px solid #ddd; transition: opacity 0.2s; background: #f0f0f0; }
        .file-thumb:hover { opacity: 0.8; }
        .file-status-accepted { color: #4CAF50; font-weight: bold; font-size: 12px; margin-top: 5px; }
        .file-status-declined { color: #F44336; font-weight: bold; font-size: 12px; margin-top: 5px; }
        .download-link { display: block; margin-top: 5px; font-size: 12px; color: #2196F3; text-decoration: none; cursor: pointer; }
        .download-link:hover { text-decoration: underline; }

        @keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
    </style>
</head>
<body>
    <div id=""chat-container""></div>
    <script>
        function scrollToBottom() {
            window.scrollTo(0, document.body.scrollHeight);
        }

        function smartScroll() {
            // Only scroll if we are near the bottom (user hasn't scrolled up to read history)
            const isAtBottom = (window.innerHeight + window.scrollY) >= (document.body.offsetHeight - 250);
            if (isAtBottom) {
                scrollToBottom();
            }
        }

        window.chrome.webview.addEventListener('message', event => {
            const data = event.data;
            if (data.type === 'audioListened') {
                const id = data.id;
                const readIndicator = document.getElementById('audio-read-' + id);
                if (readIndicator) {
                    readIndicator.style.display = 'block';
                }
            }
        });

        function addStatusMessage(htmlContent, statusClass) {
            const container = document.getElementById('chat-container');
            const msgDiv = document.createElement('div');
            msgDiv.className = 'status-message ' + statusClass;
            msgDiv.innerHTML = htmlContent;
            container.appendChild(msgDiv);
            scrollToBottom();
        }

        function playAudio(btn) {
            var container = btn.parentElement;
            var audio = container.querySelector('audio');
            var allAudios = document.getElementsByTagName('audio');
            
            // Stop others
            for(var i=0; i<allAudios.length; i++) {
                if(allAudios[i] !== audio) {
                    allAudios[i].pause();
                    var otherBtn = allAudios[i].parentElement.querySelector('button');
                    if(otherBtn) otherBtn.innerHTML = '▶';
                }
            }

            if (audio.paused) {
                audio.play();
                btn.innerHTML = '⏸';
            } else {
                audio.pause();
                btn.innerHTML = '▶';
            }
        }

        function resetAudio(audio) {
            var btn = audio.parentElement.querySelector('button');
            btn.innerHTML = '▶';
            audio.parentElement.querySelector('.progress').style.width = '0%';
        }

        function updateProgress(audio) {
            var percent = (audio.currentTime / audio.duration) * 100;
            audio.parentElement.querySelector('.progress').style.width = percent + '%';
        }

        function notifyPlayed(audio) {
            // Find message ID if possible, or just notify generic
            // We need the message ID to be passed in addMessage if we want to track specific messages
            // But addMessage currently doesn't take ID.
            // However, we can try to find the parent message div ID if it was set.
            // But addMessage creates a div without ID usually.
            // Wait, I updated OnPrivateMessageReceived to pass ID, but addMessage signature is (content, isMine, time).
            // I should update addMessage signature to (content, isMine, time, id).
        }

        function addMessage(contentBase64, isMine, time, id) {
            const container = document.getElementById('chat-container');
            const msgDiv = document.createElement('div');
            msgDiv.className = 'message ' + (isMine ? 'mine' : 'theirs');
            if (id) msgDiv.id = 'msg-' + id;
            
            let content = '';
            try { content = decodeURIComponent(escape(atob(contentBase64))); } catch (e) { content = atob(contentBase64); }

            content = content.replace(/\[smiley:(b_s_\d+\.png)\]/g, '<img src=""https://assets/Smiley/$1"" class=""smiley"" />');

            // Legacy Image Support
            if (content.startsWith('[IMAGE]')) {
                var url = content.substring(7);
                var safeUrl = url.replace(/\\/g, '\\\\').replace(/'/g, ""\\\\'"");
                content = '<div style=""position:relative; display:inline-block;"">' +
                          '<img src=""' + url + '"" class=""file-thumb"" onclick=""window.chrome.webview.postMessage(JSON.stringify({type: \'openImage\', url: \'' + safeUrl + '\'}))"" />' +
                          '</div>';
            }
            // Audio Message Support
            else if (content.startsWith('[AUDIO_MSG]')) {
                var parts = content.substring(11).split('|');
                var url = parts[0];
                var duration = parts.length > 1 ? parts[1] : '0';
                
                var sec = parseInt(duration);
                var min = Math.floor(sec / 60);
                var s = sec % 60;
                var timeStr = min + ':' + (s < 10 ? '0' + s : s);

                content = `
                    <div class=""audio-msg"" data-src=""${url}"" style=""display:flex; align-items:center; gap:10px; min-width:200px;"">
                        <button onclick=""playAudio(this)"" style=""background:none; border:none; cursor:pointer; font-size:24px; color:${isMine ? 'white' : '#333'}; outline:none;"">▶</button>
                        <div style=""flex-grow:1; height:4px; background:${isMine ? 'rgba(255,255,255,0.3)' : '#ddd'}; border-radius:2px; position:relative;"">
                            <div class=""progress"" style=""width:0%; height:100%; background:${isMine ? 'white' : '#2196F3'}; border-radius:2px;""></div>
                        </div>
                        <span style=""font-size:11px; min-width:30px;"">${timeStr}</span>
                        <audio src=""${url}"" onended=""resetAudio(this)"" ontimeupdate=""updateProgress(this)"" onplay=""notifyPlayed(this, ${id})"" preload=""metadata""></audio>
                    </div>`;
                
                // Add read receipt indicator for audio
                if (isMine) {
                     content += `<div id=""audio-read-${id}"" style=""font-size:10px; text-align:right; opacity:0.7; display:none;"">✅ Écouté</div>`;
                }
            }

            msgDiv.innerHTML = `<div class=""bubble"">${content}<div class=""timestamp"">${time}</div></div>`;
            container.appendChild(msgDiv);
            
            scrollToBottom();
            
            // Ensure scroll after images load
            const images = msgDiv.getElementsByTagName('img');
            for(let img of images) {
                img.onload = smartScroll;
            }
        }

        function notifyPlayed(audio, id) {
            if (id) {
                window.chrome.webview.postMessage(JSON.stringify({type: 'audioPlayed', id: id}));
            }
        }

        function addFileRequest(id, sender, filename, url, isMine, status) {
            var ext = filename.split('.').pop().toLowerCase();
            if (['mp4', 'avi', 'mov', 'mkv', 'webm'].indexOf(ext) >= 0) {
                addVideoRequest(id, sender, filename, url, isMine, status);
                return;
            }
            if (['mp3', 'wav', 'ogg', 'm4a', 'aac', 'wma', 'flac'].indexOf(ext) >= 0) {
                addAudioRequest(id, sender, filename, url, isMine, status);
                return;
            }
            if (['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp'].indexOf(ext) < 0) {
                addGenericFileRequest(id, sender, filename, url, isMine, status);
                return;
            }

            const container = document.getElementById('chat-container');
            const msgDiv = document.createElement('div');
            msgDiv.className = 'message ' + (isMine ? 'mine' : 'theirs');
            msgDiv.id = 'file-' + id;

            let innerHtml = '';
            if (isMine) {
                innerHtml = `
                    <div class=""bubble"">
                        <img src=""${url}"" class=""file-thumb"" style=""opacity:0.7"" onclick=""window.chrome.webview.postMessage(JSON.stringify({type: 'openImage', url: '${url}'}))"" />
                        <div id=""status-${id}"" style=""font-size:11px; color:#666; margin-top:5px;"">En attente...</div>
                    </div>`;
            } else {
                innerHtml = `
                    <div class=""bubble file-request"">
                        <div style=""font-weight:bold; margin-bottom:5px; display:flex; align-items:center; gap:5px;"">
                            <span style=""font-size:16px;"">📷</span> <span>${filename}</span>
                        </div>
                        <div id=""actions-${id}"" class=""file-actions"">
                            <button class=""btn-accept"" onclick=""respond(${id}, true)"">Accepter</button>
                            <button class=""btn-decline"" onclick=""respond(${id}, false)"">Refuser</button>
                        </div>
                        <div id=""content-${id}"" style=""display:none; margin-top:10px;"">
                            <img src=""${url}"" class=""file-thumb"" onclick=""window.chrome.webview.postMessage(JSON.stringify({type: 'openImage', url: '${url}'}))"" />
                            <div class=""download-link"" onclick=""window.chrome.webview.postMessage(JSON.stringify({type: 'saveImage', url: '${url}', filename: '${filename}'}))"">💾 Enregistrer sous...</div>
                        </div>
                    </div>`;
            }

            msgDiv.innerHTML = innerHtml;
            container.appendChild(msgDiv);
            
            // Apply status if not pending (0)
            if (status !== undefined && status !== 0) {
                updateFileStatus(id, status === 1, isMine);
            }

            scrollToBottom();
            
            // Ensure scroll after images load
            const images = msgDiv.getElementsByTagName('img');
            for(let img of images) {
                img.onload = smartScroll;
            }
        }

        function updateFileStatus(id, isAccepted, isMine) {
            if (isMine) {
                const statusDiv = document.getElementById('status-' + id);
                if (statusDiv) {
                    if (isAccepted) {
                        statusDiv.innerHTML = '<span class=""file-status-accepted"">Votre image a été acceptée</span>';
                        const img = document.querySelector(`#file-${id} img`);
                        if(img) img.style.opacity = '1';
                    } else {
                        statusDiv.innerHTML = '<span class=""file-status-declined"">Votre image a été refusée</span>';
                    }
                }
            } else {
                const actionsDiv = document.getElementById('actions-' + id);
                const contentDiv = document.getElementById('content-' + id);
                if (actionsDiv) actionsDiv.style.display = 'none';
                
                if (isAccepted) {
                    if (contentDiv) contentDiv.style.display = 'block';
                } else {
                    const bubble = document.querySelector(`#file-${id} .bubble`);
                    if(bubble) bubble.innerHTML = '<div style=""color:#F44336; font-style:italic;"">Image refusée</div>';
                }
            }
        }

        function respond(id, accepted) {
            window.chrome.webview.postMessage(JSON.stringify({type: 'respondImage', id: id.toString(), accepted: accepted.toString()}));
        }

        function addVideoRequest(id, sender, filename, url, isMine, status) {
            const container = document.getElementById('chat-container');
            const msgDiv = document.createElement('div');
            msgDiv.className = 'message ' + (isMine ? 'mine' : 'theirs');
            msgDiv.id = 'video-' + id;

            var safeUrl = url.replace(/\\/g, '\\\\').replace(/'/g, ""\\\\'"");
            var safeFilename = filename.replace(/'/g, ""\\\\'"");

            let innerHtml = '';
            // Video Player HTML (Hidden initially for receiver)
            const videoPlayer = `
                <div style=""width:300px; height:200px; border-radius:8px; overflow:hidden; background:black;"">
                    <video controls width=""100%"" height=""100%"" preload=""metadata"">
                        <source src=""${url}"" type=""video/mp4"">
                        Votre navigateur ne supporte pas la lecture vidéo.
                    </video>
                </div>`;

            // Request Card HTML
            const requestCard = `
                <div class=""bubble file-request"" style=""background: linear-gradient(135deg, #2b5876 0%, #4e4376 100%); color:white;"">
                    <div style=""font-weight:bold; margin-bottom:10px; display:flex; align-items:center; gap:8px;"">
                        <span style=""font-size:20px;"">🎥</span> 
                        <span style=""word-break:break-all;"">${filename}</span>
                    </div>
                    <div id=""v-actions-${id}"" class=""file-actions"" style=""margin-top:10px;"">
                        <button class=""btn-accept"" style=""background:rgba(255,255,255,0.2); color:white; border:1px solid rgba(255,255,255,0.5);"" onclick=""respondVideo(${id}, true)"">Accepter</button>
                        <button class=""btn-decline"" style=""background:rgba(255,255,255,0.1); color:#ffcccc; border:1px solid rgba(255,0,0,0.3);"" onclick=""respondVideo(${id}, false)"">Refuser</button>
                    </div>
                </div>`;

            if (isMine) {
                innerHtml = `
                    <div class=""bubble"" style=""padding:5px;"">
                        ${videoPlayer}
                        <div id=""v-status-${id}"" style=""font-size:11px; color:#666; margin-top:5px; text-align:right;"">En attente...</div>
                    </div>`;
            } else {
                innerHtml = `
                    <div id=""v-req-${id}"">
                        ${requestCard}
                    </div>
                    <div id=""v-content-${id}"" style=""display:none; margin-top:5px;"">
                        <div class=""bubble"" style=""padding:5px;"">
                            ${videoPlayer}
                            <div class=""download-link"" style=""margin-top:5px; text-align:right;"" onclick=""saveVideo('${safeUrl}', '${safeFilename}')"">💾 Enregistrer sous...</div>
                        </div>
                    </div>`;
            }

            msgDiv.innerHTML = innerHtml;
            container.appendChild(msgDiv);
            
            if (status !== undefined && status !== 0) {
                updateVideoStatus(id, status === 1, isMine);
            }

            msgDiv.scrollIntoView({behavior: 'smooth', block: 'end'});
        }

        function updateVideoStatus(id, isAccepted, isMine) {
            if (isMine) {
                const statusDiv = document.getElementById('v-status-' + id);
                if (statusDiv) {
                    if (isAccepted) {
                        statusDiv.innerHTML = '<span class=""file-status-accepted"">Vidéo acceptée</span>';
                    } else {
                        statusDiv.innerHTML = '<span class=""file-status-declined"">Vidéo refusée</span>';
                    }
                }
            } else {
                const reqDiv = document.getElementById('v-req-' + id);
                const contentDiv = document.getElementById('v-content-' + id);
                
                if (isAccepted) {
                    if (reqDiv) reqDiv.style.display = 'none';
                    if (contentDiv) contentDiv.style.display = 'block';
                } else {
                    if (reqDiv) reqDiv.innerHTML = '<div class=""bubble"" style=""color:#F44336; font-style:italic;"">Vidéo refusée</div>';
                }
            }
        }

        function respondVideo(id, accepted) {
            window.chrome.webview.postMessage(JSON.stringify({type: 'respondVideo', id: id.toString(), accepted: accepted.toString()}));
        }

        function saveVideo(url, filename) {
            window.chrome.webview.postMessage(JSON.stringify({type: 'saveVideo', url: url, filename: filename}));
        }

        function addAudioRequest(id, sender, filename, url, isMine, status) {
            const container = document.getElementById('chat-container');
            const msgDiv = document.createElement('div');
            msgDiv.className = 'message ' + (isMine ? 'mine' : 'theirs');
            msgDiv.id = 'audio-' + id;

            var safeUrl = url.replace(/\\/g, '\\\\').replace(/'/g, ""\\\\'"");
            var safeFilename = filename.replace(/'/g, ""\\\\'"");

            let innerHtml = '';
            // Audio Player HTML
            const audioPlayer = `
                <div style=""width:250px; border-radius:20px; background:#f1f1f1; padding:5px;"">
                    <audio controls style=""width:100%; height:30px;"">
                        <source src=""${url}"" type=""audio/mpeg"">
                        Votre navigateur ne supporte pas l'audio.
                    </audio>
                </div>`;

            // Request Card HTML
            const requestCard = `
                <div class=""bubble file-request"" style=""background: linear-gradient(135deg, #1DB954 0%, #191414 100%); color:white;"">
                    <div style=""font-weight:bold; margin-bottom:10px; display:flex; align-items:center; gap:8px;"">
                        <span style=""font-size:20px;"">🎵</span> 
                        <span style=""word-break:break-all;"">${filename}</span>
                    </div>
                    <div id=""a-actions-${id}"" class=""file-actions"" style=""margin-top:10px;"">
                        <button class=""btn-accept"" style=""background:rgba(255,255,255,0.2); color:white; border:1px solid rgba(255,255,255,0.5);"" onclick=""respondAudio(${id}, true)"">Accepter</button>
                        <button class=""btn-decline"" style=""background:rgba(255,255,255,0.1); color:#ffcccc; border:1px solid rgba(255,0,0,0.3);"" onclick=""respondAudio(${id}, false)"">Refuser</button>
                    </div>
                </div>`;

            if (isMine) {
                innerHtml = `
                    <div class=""bubble"" style=""padding:5px;"">
                        ${audioPlayer}
                        <div id=""a-status-${id}"" style=""font-size:11px; color:#666; margin-top:5px; text-align:right;"">En attente...</div>
                    </div>`;
            } else {
                innerHtml = `
                    <div id=""a-req-${id}"">
                        ${requestCard}
                    </div>
                    <div id=""a-content-${id}"" style=""display:none; margin-top:5px;"">
                        <div class=""bubble"" style=""padding:5px;"">
                            ${audioPlayer}
                            <div class=""download-link"" style=""margin-top:5px; text-align:right;"" onclick=""saveAudio('${safeUrl}', '${safeFilename}')"">💾 Enregistrer sous...</div>
                        </div>
                    </div>`;
            }

            msgDiv.innerHTML = innerHtml;
            container.appendChild(msgDiv);
            
            if (status !== undefined && status !== 0) {
                updateAudioStatus(id, status === 1, isMine);
            }

            msgDiv.scrollIntoView({behavior: 'smooth', block: 'end'});
        }

        function updateAudioStatus(id, isAccepted, isMine) {
            if (isMine) {
                const statusDiv = document.getElementById('a-status-' + id);
                if (statusDiv) {
                    if (isAccepted) {
                        statusDiv.innerHTML = '<span class=""file-status-accepted"">Audio accepté</span>';
                    } else {
                        statusDiv.innerHTML = '<span class=""file-status-declined"">Audio refusé</span>';
                    }
                }
            } else {
                const reqDiv = document.getElementById('a-req-' + id);
                const contentDiv = document.getElementById('a-content-' + id);
                
                if (isAccepted) {
                    if (reqDiv) reqDiv.style.display = 'none';
                    if (contentDiv) contentDiv.style.display = 'block';
                } else {
                    if (reqDiv) reqDiv.innerHTML = '<div class=""bubble"" style=""color:#F44336; font-style:italic;"">Audio refusé</div>';
                }
            }
        }

        function respondAudio(id, accepted) {
            window.chrome.webview.postMessage(JSON.stringify({type: 'respondAudio', id: id.toString(), accepted: accepted.toString()}));
        }

        function saveAudio(url, filename) {
            window.chrome.webview.postMessage(JSON.stringify({type: 'saveAudio', url: url, filename: filename}));
        }

        function addGenericFileRequest(id, sender, filename, url, isMine, status) {
            const container = document.getElementById('chat-container');
            const msgDiv = document.createElement('div');
            msgDiv.className = 'message ' + (isMine ? 'mine' : 'theirs');
            msgDiv.id = 'file-' + id;

            var safeUrl = url.replace(/\\/g, '\\\\').replace(/'/g, ""\\\\'"");
            var safeFilename = filename.replace(/'/g, ""\\\\'"");

            var icon = '📄';
            var ext = filename.split('.').pop().toLowerCase();
            if (['zip', 'rar', '7z'].indexOf(ext) >= 0) icon = '📦';
            else if (['pdf'].indexOf(ext) >= 0) icon = '📕';
            else if (['doc', 'docx'].indexOf(ext) >= 0) icon = '📘';
            else if (['xls', 'xlsx'].indexOf(ext) >= 0) icon = '📗';
            else if (['ppt', 'pptx'].indexOf(ext) >= 0) icon = '📙';
            else if (['txt'].indexOf(ext) >= 0) icon = '📝';
            else if (['mp3', 'wav', 'ogg'].indexOf(ext) >= 0) icon = '🎵';

            let innerHtml = '';
            if (isMine) {
                innerHtml = `
                    <div class=""bubble"">
                        <div style=""display:flex; align-items:center; gap:10px;"">
                            <div style=""font-size:24px;"">${icon}</div>
                            <div>
                                <div style=""font-weight:bold;"">${filename}</div>
                                <div id=""status-${id}"" style=""font-size:11px; color:#666;"">En attente...</div>
                            </div>
                        </div>
                    </div>`;
            } else {
                innerHtml = `
                    <div class=""bubble file-request"">
                        <div style=""font-weight:bold; margin-bottom:5px; display:flex; align-items:center; gap:5px;"">
                            <span style=""font-size:24px;"">${icon}</span> <span>${filename}</span>
                        </div>
                        <div id=""actions-${id}"" class=""file-actions"">
                            <button class=""btn-accept"" onclick=""respondFile(${id}, true)"">Accepter</button>
                            <button class=""btn-decline"" onclick=""respondFile(${id}, false)"">Refuser</button>
                        </div>
                        <div id=""content-${id}"" style=""display:none; margin-top:10px;"">
                            <div class=""download-link"" onclick=""window.chrome.webview.postMessage(JSON.stringify({type: 'saveImage', url: '${safeUrl}', filename: '${safeFilename}'}))"">💾 Enregistrer sous...</div>
                        </div>
                    </div>`;
            }

            msgDiv.innerHTML = innerHtml;
            container.appendChild(msgDiv);
            
            if (status !== undefined && status !== 0) {
                updateGenericFileStatus(id, status === 1, isMine);
            }

            scrollToBottom();
        }

        function updateGenericFileStatus(id, isAccepted, isMine) {
            if (isMine) {
                const statusDiv = document.getElementById('status-' + id);
                if (statusDiv) {
                    if (isAccepted) {
                        statusDiv.innerHTML = '<span class=""file-status-accepted"">Fichier accepté</span>';
                    } else {
                        statusDiv.innerHTML = '<span class=""file-status-declined"">Fichier refusé</span>';
                    }
                }
            } else {
                const actionsDiv = document.getElementById('actions-' + id);
                const contentDiv = document.getElementById('content-' + id);
                if (actionsDiv) actionsDiv.style.display = 'none';
                
                if (isAccepted) {
                    if (contentDiv) contentDiv.style.display = 'block';
                } else {
                    const bubble = document.querySelector(`#file-${id} .bubble`);
                    if(bubble) bubble.innerHTML = '<div style=""color:#F44336; font-style:italic;"">Fichier refusé</div>';
                }
            }
        }

        function respondFile(id, accepted) {
            window.chrome.webview.postMessage(JSON.stringify({type: 'respondFile', id: id.toString(), accepted: accepted.toString()}));
        }

        function clearChat() {
            document.getElementById('chat-container').innerHTML = '';
        }
    </script>

</body>
</html>";
                ChatWebView.NavigateToString(html);
                ChatWebView.CoreWebView2.DOMContentLoaded += async (s, e) => await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur critique du composant Chat (WebView2) : {ex.Message}\n\nAssurez-vous que 'WebView2 Runtime' est installé.", "Erreur Chat", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadHistoryAsync()
        {
            if (ChatWebView.CoreWebView2 == null) return;

            // Mark messages as read (Redundant but safe)
            await ApiService.Instance.MarkMessagesAsReadAsync(_partnerUser);

            var messages = await ApiService.Instance.GetChatHistoryAsync(_partnerUser);
            foreach (var msg in messages)
            {
                if (msg.Content.StartsWith("[FILE_REQUEST:"))
                {
                    try 
                    {
                        string data = msg.Content.Substring(14, msg.Content.Length - 15);
                        
                        // Format: Id:FileName:Url:Status
                        int lastColon = data.LastIndexOf(':');
                        if (lastColon > 0)
                        {
                            string statusStr = data.Substring(lastColon + 1);
                            string rest = data.Substring(0, lastColon);
                            
                            int firstColon = rest.IndexOf(':');
                            int secondColon = rest.IndexOf(':', firstColon + 1);
                            
                            if (firstColon > 0 && secondColon > 0)
                            {
                                string idStr = rest.Substring(0, firstColon);
                                string filename = rest.Substring(firstColon + 1, secondColon - firstColon - 1);
                                string url = rest.Substring(secondColon + 1);
                                
                                int id = int.Parse(idStr);
                                int status = int.Parse(statusStr);
                                bool isMine = msg.Sender == _currentUser;
                                
                                string safeUrl = url.Replace("\\", "\\\\").Replace("'", "\\'");
                                string safeFilename = filename.Replace("'", "\\'");
                                
                                string script = $"addFileRequest({id}, '{msg.Sender}', '{safeFilename}', '{safeUrl}', {(isMine ? "true" : "false")}, {status});";
                                await ChatWebView.ExecuteScriptAsync(script);
                                continue;
                            }
                        }
                        // Fallback for old format (Id:FileName:Url)
                        else 
                        {
                            int firstColon = data.IndexOf(':');
                            int secondColon = data.IndexOf(':', firstColon + 1);
                            if (firstColon > 0 && secondColon > 0)
                            {
                                string idStr = data.Substring(0, firstColon);
                                string filename = data.Substring(firstColon + 1, secondColon - firstColon - 1);
                                string url = data.Substring(secondColon + 1);
                                int id = int.Parse(idStr);
                                bool isMine = msg.Sender == _currentUser;
                                
                                string safeUrl = url.Replace("\\", "\\\\").Replace("'", "\\'");
                                string safeFilename = filename.Replace("'", "\\'");

                                string script = $"addFileRequest({id}, '{msg.Sender}', '{safeFilename}', '{safeUrl}', {(isMine ? "true" : "false")}, 0);";
                                await ChatWebView.ExecuteScriptAsync(script);
                                continue;
                            }
                        }
                    }
                    catch {}
                }
                else if (msg.Content == "[SYSTEM_BLOCK]")
                {
                    string text;
                    string partnerName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : _partnerUser;
                    
                    if (msg.Sender == _currentUser)
                        text = $"Vous avez bloqué {partnerName} – Blocage PERMANENT – {msg.Timestamp:dd/MM/yyyy HH:mm}";
                    else
                        text = $"{partnerName} vous a bloqué – Blocage PERMANENT – {msg.Timestamp:dd/MM/yyyy HH:mm}";
                    
                    string script = $"addStatusMessage('{text}', 'status-blocked');";
                    await ChatWebView.ExecuteScriptAsync(script);
                }
                else if (msg.Content == "[SYSTEM_UNBLOCK]")
                {
                    string text;
                    string partnerName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : _partnerUser;

                    if (msg.Sender == _currentUser)
                        text = $"Vous avez débloqué {partnerName} – {msg.Timestamp:dd/MM/yyyy HH:mm}";
                    else
                        text = $"{partnerName} vous a débloqué – {msg.Timestamp:dd/MM/yyyy HH:mm}";
                    
                    string script = $"addStatusMessage('{text}', 'status-unblocked');";
                    await ChatWebView.ExecuteScriptAsync(script);
                }
                else
                {
                    var chatMsg = new ChatMessage
                    {
                        Content = msg.Content,
                        IsMine = msg.Sender == _currentUser,
                        Timestamp = msg.Timestamp
                    };
                    AppendMessageToUi(chatMsg);
                }
                if (msg.Id > _lastMessageId) _lastMessageId = msg.Id;
            }
            
            // Initial Status Message (at the bottom)
            UpdateStatusMessageInChat();

            // Mark as read
            await ApiService.Instance.MarkMessagesAsReadAsync(_partnerUser);
        }

        private void UpdateStatusMessageInChat()
        {
            string status = PartnerStatus.Text;
            string name = PartnerName.Text;
            string cssClass = "status-offline";
            
            if (status.ToLower() == "en ligne") cssClass = "status-online";
            else if (status.ToLower() == "occupé" || status.ToLower() == "ne pas déranger") cssClass = "status-busy";
            else if (status.ToLower() == "absent") cssClass = "status-away";

            string message = $"{name} est actuellement {status}";
            string script = $"addStatusMessage('{message}', '{cssClass}');";
            ChatWebView.ExecuteScriptAsync(script);
        }

        private string GetHtmlTemplate()
        {
            return @"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <style>
                    body { font-family: 'Segoe UI', sans-serif; margin: 0; padding: 10px; padding-bottom: 20px; background-color: #f9f9f9; }
                    .message-container { display: flex; flex-direction: column; margin-bottom: 10px; }
                    .message-bubble { max-width: 70%; padding: 10px 15px; border-radius: 18px; position: relative; word-wrap: break-word; font-size: 14px; line-height: 1.4; }
                    .mine { align-self: flex-end; background-color: #0078D7; color: white; border-bottom-right-radius: 4px; }
                    .theirs { align-self: flex-start; background-color: #E0E0E0; color: #333; border-bottom-left-radius: 4px; }
                    .timestamp { font-size: 10px; margin-top: 4px; opacity: 0.7; text-align: right; }
                    .status-message { text-align: center; font-size: 12px; margin: 15px 0; font-style: italic; padding: 5px; border-radius: 10px; }
                    .status-online { color: #4CAF50; background-color: #E8F5E9; }
                    .status-offline { color: #9E9E9E; background-color: #F5F5F5; }
                    .status-busy { color: #F44336; background-color: #FFEBEE; }
                    .status-away { color: #FF9800; background-color: #FFF3E0; }
                    .status-blocked { color: #D32F2F; background-color: #FFCDD2; font-weight: bold; border: 1px solid #EF9A9A; }
                    
                    /* BUZZ STYLES */
                    .status-buzz-sent { color: #2196F3; background-color: #E3F2FD; font-weight: bold; border: 1px solid #90CAF9; animation: pulse 1s; }
                    .status-buzz-received { color: #FF9800; background-color: #FFF3E0; font-weight: bold; border: 1px solid #FFCC80; animation: shake 0.5s; }

                    @keyframes pulse { 0% { transform: scale(1); } 50% { transform: scale(1.05); } 100% { transform: scale(1); } }
                    @keyframes shake { 0% { transform: translate(1px, 1px) rotate(0deg); } 10% { transform: translate(-1px, -2px) rotate(-1deg); } 20% { transform: translate(-3px, 0px) rotate(1deg); } 30% { transform: translate(3px, 2px) rotate(0deg); } 40% { transform: translate(1px, -1px) rotate(1deg); } 50% { transform: translate(-1px, 2px) rotate(-1deg); } 60% { transform: translate(-3px, 1px) rotate(0deg); } 70% { transform: translate(3px, 1px) rotate(-1deg); } 80% { transform: translate(-1px, -1px) rotate(1deg); } 90% { transform: translate(1px, 2px) rotate(0deg); } 100% { transform: translate(1px, -2px) rotate(-1deg); } }
                </style>
                <script>
                    function saveFile(url, filename) {
                        window.chrome.webview.postMessage(JSON.stringify({type: 'saveImage', url: url, filename: filename}));
                    }
                    
                    function openImage(url) {
                        window.chrome.webview.postMessage(JSON.stringify({type: 'openImage', url: url}));
                    }
                    
                    function saveVideo(url, filename) {
                        window.chrome.webview.postMessage(JSON.stringify({type: 'saveVideo', url: url, filename: filename}));
                    }

                    function respondFile(id, accepted) {
                        window.chrome.webview.postMessage(JSON.stringify({type: 'respondFile', id: id.toString(), accepted: accepted.toString()}));
                    }

                    function respondImage(id, accepted) {
                        window.chrome.webview.postMessage(JSON.stringify({type: 'respondImage', id: id.toString(), accepted: accepted.toString()}));
                    }

                    function respondVideo(id, accepted) {
                        window.chrome.webview.postMessage(JSON.stringify({type: 'respondVideo', id: id.toString(), accepted: accepted.toString()}));
                    }

                    function respondAudio(id, accepted) {
                        window.chrome.webview.postMessage(JSON.stringify({type: 'respondAudio', id: id.toString(), accepted: accepted.toString()}));
                    }

                    function addMessage(base64Content, isMine, time) {
                        var content = decodeURIComponent(escape(window.atob(base64Content)));
                        var container = document.createElement('div');
                        container.className = 'message-container ' + (isMine ? 'mine' : 'theirs');
                        
                        var bubble = document.createElement('div');
                        bubble.className = 'message-bubble ' + (isMine ? 'mine' : 'theirs');
                        
                        // Legacy Image Support
                        if (content.startsWith('[IMAGE]')) {
                            var url = content.substring(7);
                            var safeUrl = url.replace(/'/g, ""\\\\'"");
                            content = '<img src=""' + url + '"" class=""file-thumb"" onclick=""openImage(\'' + safeUrl + '\')"" />';
                        }
                        
                        bubble.innerHTML = content;
                        
                        var ts = document.createElement('div');
                        ts.className = 'timestamp';
                        ts.innerText = time;
                        
                        bubble.appendChild(ts);
                        container.appendChild(bubble);
                        document.body.appendChild(container);
                        container.scrollIntoView({behavior: 'smooth', block: 'end'});
                    }

                    function addStatusMessage(text, cssClass) {
                        var div = document.createElement('div');
                        div.className = 'status-message ' + cssClass;
                        div.innerText = text;
                        document.body.appendChild(div);
                        window.scrollTo(0, document.body.scrollHeight);
                    }

                    function addGenericFileRequest(id, sender, filename, url, isMine, status) {
                        var container = document.createElement('div');
                        container.className = 'message-container ' + (isMine ? 'mine' : 'theirs');
                        
                        var bubble = document.createElement('div');
                        bubble.className = 'message-bubble ' + (isMine ? 'mine' : 'theirs');
                        bubble.id = 'f-bubble-' + id;
                        
                        var icon = '📄';
                        var ext = filename.split('.').pop().toLowerCase();
                        if (['zip', 'rar'].indexOf(ext) >= 0) icon = '📦';
                        else if (['pdf'].indexOf(ext) >= 0) icon = '📕';
                        else if (['doc', 'docx'].indexOf(ext) >= 0) icon = '📘';
                        else if (['xls', 'xlsx'].indexOf(ext) >= 0) icon = '📗';
                        else if (['ppt', 'pptx'].indexOf(ext) >= 0) icon = '📙';
                        else if (['txt'].indexOf(ext) >= 0) icon = '📝';
                        
                        var html = '<div style=""display:flex; align-items:center; gap:10px;"">';
                        html += '<div style=""font-size:24px;"">' + icon + '</div>';
                        html += '<div><div style=""font-weight:bold;"">' + filename + '</div>';
                        html += '<div style=""font-size:11px; opacity:0.7;"">Fichier ' + ext.toUpperCase() + '</div>';
                        
                        // Status Area
                        html += '<div id=""f-status-' + id + '"" style=""margin-top:5px;"">';
                        
                        if (status === undefined || status === 0) { // Pending
                            html += '<div id=""f-req-' + id + '"" style=""font-size:12px; font-style:italic;"">En attente...</div>';
                            if (!isMine) {
                                html += '<div id=""f-actions-' + id + '"" style=""margin-top:8px; display:flex; gap:5px;"">';
                                html += '<button onclick=""respondFile(' + id + ', true)"" style=""background:#4CAF50; color:white; border:none; padding:5px 10px; border-radius:4px; cursor:pointer;"">Accepter</button>';
                                html += '<button onclick=""respondFile(' + id + ', false)"" style=""background:#F44336; color:white; border:none; padding:5px 10px; border-radius:4px; cursor:pointer;"">Refuser</button>';
                                html += '</div>';
                            }
                        } else if (status === 1) { // Accepted
                            html += '<div style=""font-size:12px; color:' + (isMine ? '#E8F5E9' : '#2E7D32') + ';"">Accepté</div>';
                        } else if (status === 2) { // Declined
                            html += '<div style=""font-size:12px; color:' + (isMine ? '#FFCDD2' : '#C62828') + ';"">Refusé</div>';
                        }
                        html += '</div>'; // End Status Area

                        // Content Area (Download Button) - Hidden if pending
                        var displayStyle = (status === 1) ? 'block' : 'none';
                        html += '<div id=""f-content-' + id + '"" style=""display:' + displayStyle + '; margin-top:8px;"">';
                        if (url) {
                            var safeUrl = url.replace(/\\/g, '\\\\').replace(/'/g, ""\\\\'"");
                            var safeFilename = filename.replace(/'/g, ""\\\\'"");
                            html += '<button onclick=""saveFile(\'' + safeUrl + '\', \'' + safeFilename + '\')"" style=""background:#FF9800; color:white; border:none; padding:5px 10px; border-radius:4px; cursor:pointer; width:100%;"">💾 Enregistrer</button>';
                        }
                        html += '</div>';

                        html += '</div></div>';
                        bubble.innerHTML = html;
                        
                        container.appendChild(bubble);
                        document.body.appendChild(container);
                        container.scrollIntoView({behavior: 'smooth', block: 'end'});
                    }

                    function updateGenericFileStatus(id, isAccepted) {
                        var bubble = document.getElementById('f-bubble-' + id);
                        var isMine = bubble && bubble.classList.contains('mine');
                        
                        var statusDiv = document.getElementById('f-status-' + id);
                        var contentDiv = document.getElementById('f-content-' + id);
                        var actionsDiv = document.getElementById('f-actions-' + id);
                        var reqDiv = document.getElementById('f-req-' + id);

                        if (statusDiv) {
                            if (isAccepted) {
                                statusDiv.innerHTML = '<div style=""font-size:12px; color:' + (isMine ? '#E8F5E9' : '#2E7D32') + ';"">Accepté</div>';
                                if (contentDiv) contentDiv.style.display = 'block';
                            } else {
                                statusDiv.innerHTML = '<div style=""font-size:12px; color:' + (isMine ? '#FFCDD2' : '#C62828') + ';"">Refusé</div>';
                                if (actionsDiv) actionsDiv.style.display = 'none';
                                if (reqDiv) reqDiv.style.display = 'none';
                            }
                        }
                    }

                    function addFileRequest(id, sender, filename, url, isMine, status) {
                        var ext = filename.split('.').pop().toLowerCase();
                        if (['mp4', 'avi', 'mov', 'mkv', 'webm'].indexOf(ext) >= 0) {
                            addVideoRequest(id, sender, filename, url, isMine, status);
                            return;
                        }
                        if (['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp'].indexOf(ext) < 0) {
                            addGenericFileRequest(id, sender, filename, url, isMine, status);
                            return;
                        }

                        var container = document.createElement('div');
                        container.className = 'message-container ' + (isMine ? 'mine' : 'theirs');
                        
                        var bubble = document.createElement('div');
                        bubble.className = 'message-bubble ' + (isMine ? 'mine' : 'theirs');
                        
                        var icon = '📷';
                        var typeLabel = 'Image';
                        
                        var html = '<div style=""display:flex; align-items:center; gap:10px;"">';
                        html += '<div style=""font-size:24px;"">' + icon + '</div>';
                        html += '<div><div style=""font-weight:bold;"">' + filename + '</div>';
                        html += '<div style=""font-size:11px; opacity:0.7;"">' + typeLabel + '</div>';
                        
                        if (status === 0) { // Pending
                            html += '<div style=""margin-top:5px; font-size:12px; font-style:italic;"">En attente...</div>';
                            if (!isMine) {
                                html += '<div style=""margin-top:8px; display:flex; gap:5px;"">';
                                html += '<button onclick=""window.chrome.webview.postMessage(JSON.stringify({type:\'acceptFile\', id:' + id + ', sender:\'' + sender + '\'}))"" style=""background:#4CAF50; color:white; border:none; padding:5px 10px; border-radius:4px; cursor:pointer;"">Accepter</button>';
                                html += '<button onclick=""window.chrome.webview.postMessage(JSON.stringify({type:\'declineFile\', id:' + id + ', sender:\'' + sender + '\'}))"" style=""background:#F44336; color:white; border:none; padding:5px 10px; border-radius:4px; cursor:pointer;"">Refuser</button>';
                                html += '</div>';
                            }
                        } else if (status === 1) { // Accepted
                            html += '<div style=""margin-top:5px; font-size:12px; color:' + (isMine ? '#E8F5E9' : '#2E7D32') + ';"">Accepté</div>';
                            if (url) {
                                var safeUrl = url.replace(/\\/g, '\\\\').replace(/'/g, '\\\'');
                                var safeFilename = filename.replace(/'/g, '\\\'');
                                html += '<div style=""margin-top:8px; display:flex; gap:5px;"">';
                                html += '<button onclick=""openImage(\'' + safeUrl + '\')"" style=""background:#2196F3; color:white; border:none; padding:5px 10px; border-radius:4px; cursor:pointer;"">Voir</button>';
                                html += '<button onclick=""saveFile(\'' + safeUrl + '\', \'' + safeFilename + '\')"" style=""background:#FF9800; color:white; border:none; padding:5px 10px; border-radius:4px; cursor:pointer;"">Enregistrer</button>';
                                html += '</div>';
                            }
                        } else if (status === 2) { // Declined
                            html += '<div style=""margin-top:5px; font-size:12px; color:' + (isMine ? '#FFCDD2' : '#C62828') + ';"">Refusé</div>';
                        }
                        
                        html += '</div></div>';
                        bubble.innerHTML = html;
                        
                        container.appendChild(bubble);
                        document.body.appendChild(container);
                        container.scrollIntoView({behavior: 'smooth', block: 'end'});
                    }
                </script>
            </head>
            <body></body>
            </html>";
        }

        private async Task CheckBlockStatusAsync()
        {
            bool blockedByMe = await ApiService.Instance.IsUserBlockedAsync(_currentUser, _partnerUser);
            bool blockedByPartner = await ApiService.Instance.IsUserBlockedAsync(_partnerUser, _currentUser);

            if (blockedByMe != _isBlocked || blockedByPartner != _isBlockedByPartner)
            {
                _isBlocked = blockedByMe;
                _isBlockedByPartner = blockedByPartner;
                UpdateBlockUi();
            }
        }



        private async void Block_Click(object sender, RoutedEventArgs e)
        {
            if (_isBlocked)
            {
                // Unblock
                var result = await ApiService.Instance.UnblockUserAsync(_partnerUser);
                if (result.Success)
                {
                    _isBlocked = false;
                    // Message will be added via SignalR event
                }
                else
                {
                    new CustomAlertWindow(result.Message, "Erreur").ShowDialog();
                }
            }
            else
            {
                // Block
                var confirm = new CustomConfirmWindow($"Voulez-vous vraiment bloquer {_partnerUser} ?\nVous ne pourrez plus échanger de messages.", "Confirmer le blocage");
                if (confirm.ShowDialog() == true)
                {
                    // Pass a default reason
                    var result = await ApiService.Instance.BlockUserAsync(_partnerUser, 0, null, "Bloqué depuis le chat");
                    if (result.Success)
                    {
                        _isBlocked = true;
                        // Message will be added via SignalR event
                    }
                    else
                    {
                        new CustomAlertWindow(result.Message, "Erreur").ShowDialog();
                    }
                }
            }
            await CheckBlockStatusAsync(); // Refresh status immediately
        }



        private void AppendMessageToUi(ChatMessage msg)
        {
            // Encode content to Base64 to preserve HTML and avoid JS syntax errors
            string base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(msg.Content));
            string time = msg.Timestamp.ToString("HH:mm");
            string script = $"addMessage('{base64Content}', {(msg.IsMine ? "true" : "false")}, '{time}', {msg.Id});";
            ChatWebView.ExecuteScriptAsync(script);
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    return;
                }
                e.Handled = true;
                SendMessage();
            }
        }

        private async void MessageInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string text = GetTextFromRichTextBox(MessageInput).Trim();
                if (!string.IsNullOrEmpty(text) && (DateTime.Now - _lastTypingSent).TotalSeconds > 2)
                {
                    await ApiService.Instance.SendTypingIndicatorAsync(_currentUser, _partnerUser);
                    _lastTypingSent = DateTime.Now;
                }
            }
            catch { /* Ignore typing errors to prevent crash */ }
        }

        private string GetTextFromRichTextBox(RichTextBox rtb)
        {
            TextRange textRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
            return textRange.Text;
        }

        private void ApplyFormatting(DependencyProperty property, object? value)
        {
            if (MessageInput == null) return;
            MessageInput.Selection.ApplyPropertyValue(property, value);
            MessageInput.Focus();
        }

        private void FormatBold_Click(object sender, RoutedEventArgs e)
        {
            var currentWeight = MessageInput.Selection.GetPropertyValue(TextElement.FontWeightProperty);
            if (currentWeight != DependencyProperty.UnsetValue && (FontWeight)currentWeight == FontWeights.Bold)
                ApplyFormatting(TextElement.FontWeightProperty, FontWeights.Normal);
            else
                ApplyFormatting(TextElement.FontWeightProperty, FontWeights.Bold);
        }

        private void FormatItalic_Click(object sender, RoutedEventArgs e)
        {
            var currentStyle = MessageInput.Selection.GetPropertyValue(TextElement.FontStyleProperty);
            if (currentStyle != DependencyProperty.UnsetValue && (FontStyle)currentStyle == FontStyles.Italic)
                ApplyFormatting(TextElement.FontStyleProperty, FontStyles.Normal);
            else
                ApplyFormatting(TextElement.FontStyleProperty, FontStyles.Italic);
        }

        private void FormatUnderline_Click(object sender, RoutedEventArgs e)
        {
            var currentDeco = MessageInput.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
            TextDecorationCollection? decorations = currentDeco as TextDecorationCollection;
            if (decorations != null && decorations.Count > 0 && decorations.Any(d => d.Location == TextDecorationLocation.Underline))
                ApplyFormatting(Inline.TextDecorationsProperty, null);
            else
                ApplyFormatting(Inline.TextDecorationsProperty, TextDecorations.Underline);
        }
        
        private void Color_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string colorCode)
            {
                try 
                {
                    Color color = (Color)ColorConverter.ConvertFromString(colorCode);
                    ApplyFormatting(TextElement.ForegroundProperty, new SolidColorBrush(color));
                    BtnColor.IsChecked = false;
                }
                catch { }
            }
        }

        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            if (SmileyPanel.Children.Count == 0)
            {
                LoadSmileys();
            }
            SmileyPopup.IsOpen = !SmileyPopup.IsOpen;
        }

        private void LoadSmileys()
        {
            string smileyPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Smiley");
            if (System.IO.Directory.Exists(smileyPath))
            {
                var files = System.IO.Directory.GetFiles(smileyPath, "*.png");
                var sortedFiles = files.OrderBy(f => {
                    string name = System.IO.Path.GetFileNameWithoutExtension(f);
                    string numberPart = name.Replace("b_s_", "");
                    if (int.TryParse(numberPart, out int n)) return n;
                    return 999;
                });

                foreach (var file in sortedFiles)
                {
                    var btn = new Button
                    {
                        Width = 32,
                        Height = 32,
                        Margin = new Thickness(2),
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand,
                        Tag = System.IO.Path.GetFileName(file)
                    };

                    var img = new Image
                    {
                        Source = new BitmapImage(new Uri(file, UriKind.Absolute)),
                        Stretch = Stretch.Uniform
                    };
                    
                    btn.Content = img;
                    btn.Click += Smiley_Click;
                    SmileyPanel.Children.Add(btn);
                }
            }
        }

        private void Smiley_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filename)
            {
                string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Smiley", filename);
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                var image = new Image { Source = bitmap, Width = 30, Height = 30, Stretch = Stretch.Uniform, Tag = filename };
                
                // Insert into RichTextBox
                var container = new InlineUIContainer(image, MessageInput.CaretPosition);
                
                // Move caret after the image
                MessageInput.CaretPosition = container.ElementEnd;
                MessageInput.Focus();
            }
        }

        private string ConvertRichTextBoxToHtml(RichTextBox rtb)
        {
            StringBuilder html = new StringBuilder();
            foreach (Block block in rtb.Document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    foreach (Inline inline in paragraph.Inlines)
                    {
                        if (inline is InlineUIContainer uiContainer && uiContainer.Child is Image img && img.Tag is string filename)
                        {
                            html.Append($"[smiley:{filename}]");
                        }
                        else if (inline is Run run)
                        {
                            string text = System.Net.WebUtility.HtmlEncode(run.Text);
                            if (run.FontWeight == FontWeights.Bold) text = $"<b>{text}</b>";
                            if (run.FontStyle == FontStyles.Italic) text = $"<i>{text}</i>";
                            if (run.TextDecorations.Any(d => d.Location == TextDecorationLocation.Underline)) text = $"<u>{text}</u>";
                            
                            if (run.Foreground is SolidColorBrush brush && brush.Color != Colors.Black && brush.Color != (Color)ColorConverter.ConvertFromString("#FF333333"))
                            {
                                text = $"<span style='color:{brush.Color}'>{text}</span>";
                            }
                            html.Append(text);
                        }
                        else if (inline is LineBreak)
                        {
                            html.Append("<br/>");
                        }
                    }
                    html.Append("<br/>");
                }
            }
            string result = html.ToString();
            if (result.EndsWith("<br/>")) result = result.Substring(0, result.Length - 5);
            return result;
        }

        private async void SendMessage()
        {
            if (_isBlocked || _isBlockedByPartner)
            {
                new CustomAlertWindow("Vous ne pouvez pas envoyer de message à cet utilisateur.").ShowDialog();
                return;
            }

            string content = ConvertRichTextBoxToHtml(MessageInput);
            string plainText = GetTextFromRichTextBox(MessageInput).Trim();
            
            if (string.IsNullOrEmpty(plainText) && !content.Contains("[smiley:")) return;

            // Capture current formatting to persist it
            object fontWeight = MessageInput.Selection.GetPropertyValue(TextElement.FontWeightProperty);
            object fontStyle = MessageInput.Selection.GetPropertyValue(TextElement.FontStyleProperty);
            object textDecorations = MessageInput.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
            object foreground = MessageInput.Selection.GetPropertyValue(TextElement.ForegroundProperty);

            // If mixed, try to get from the last run
            if (fontWeight == DependencyProperty.UnsetValue || fontStyle == DependencyProperty.UnsetValue || 
                textDecorations == DependencyProperty.UnsetValue || foreground == DependencyProperty.UnsetValue)
            {
                var lastBlock = MessageInput.Document.Blocks.LastBlock as Paragraph;
                if (lastBlock != null && lastBlock.Inlines.LastInline is Run lastRun)
                {
                    if (fontWeight == DependencyProperty.UnsetValue) fontWeight = lastRun.FontWeight;
                    if (fontStyle == DependencyProperty.UnsetValue) fontStyle = lastRun.FontStyle;
                    if (textDecorations == DependencyProperty.UnsetValue) textDecorations = lastRun.TextDecorations;
                    if (foreground == DependencyProperty.UnsetValue) foreground = lastRun.Foreground;
                }
            }

            try
            {
                // Send via API
                await ApiService.Instance.SendPrivateMessageAsync(_partnerUser, content);
                
                // Clear and restore formatting
                MessageInput.Document.Blocks.Clear();
                Paragraph p = new Paragraph();
                Run r = new Run();
                p.Inlines.Add(r);
                MessageInput.Document.Blocks.Add(p);

                if (fontWeight != DependencyProperty.UnsetValue && fontWeight != null) r.FontWeight = (FontWeight)fontWeight;
                if (fontStyle != DependencyProperty.UnsetValue && fontStyle != null) r.FontStyle = (FontStyle)fontStyle;
                if (textDecorations != DependencyProperty.UnsetValue && textDecorations != null) r.TextDecorations = (TextDecorationCollection)textDecorations;
                if (foreground != DependencyProperty.UnsetValue && foreground != null) r.Foreground = (Brush)foreground;

                MessageInput.CaretPosition = r.ContentEnd;
                MessageInput.Focus();

                var tempMsg = new ChatMessage 
                { 
                    Content = content, 
                    IsMine = true, 
                    Timestamp = DateTime.Now 
                };
                AppendMessageToUi(tempMsg);
            }
            catch (Exception)
            {
                new CustomAlertWindow("Une erreur est survenue lors de l'envoi du message. Veuillez réessayer.", "Erreur d'envoi").ShowDialog();
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class ChatMessage
    {
        public int Id { get; set; }
        public string Content { get; set; } = "";
        public bool IsMine { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
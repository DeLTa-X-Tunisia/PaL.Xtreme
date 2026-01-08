using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using PaLX.Client.Services;
using PaLX.Client.Models;

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
        private MediaPlayer _audioPlayer = new MediaPlayer();
        private bool _isBlocked = false;
        private bool _isBlockedByPartner = false;
        private int _lastMessageId = 0;
        private bool _isPartnerOnline = false;
        private int _partnerStatus = 6; // Default Offline
        private bool _dndOverride = false;
        private int _partnerRoleLevel = 7; // Default to User
        private string? _partnerAvatarPath;
        
        // Audio Recording
        private AudioRecorder _audioRecorder = new AudioRecorder();
        private bool _isRecording = false;
        private DispatcherTimer _recordingTimer;
        private int _recordingSeconds = 0;

        // Voice Call
        private VoiceCallService _voiceService;

        // Video Call
        private VideoCallService? _videoService;

        // WPF Native Messages Collection
        private ObservableCollection<ChatMessageItem> _messages = new ObservableCollection<ChatMessageItem>();
        private ChatMessageItem? _currentlyPlayingAudio = null;
        private bool _hasMoreMessages = false;
        private int _currentPage = 0;
        private const int PageSize = 50;

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

            // Init Video Service - only for outgoing calls, incoming handled by MainView
            _videoService = ApiService.Instance.VideoService;

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

            // History will be loaded in Loaded event after LoadPartnerInfo()
            
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
                await LoadPartnerInfo();  // Load partner info FIRST
                await LoadHistoryAsync(); // Then load history (uses PartnerName.Text)
                await ApiService.Instance.MarkMessagesAsReadAsync(_partnerUser);
                await CheckBlockStatusAsync();
                CheckBuzzAvailability();
                UpdateDndState();
            };

            Closing += async (s, e) =>
            {
                try { _audioRecorder.CancelRecording(); } catch { }
                try { _audioPlayer.Stop(); } catch { }
                await ApiService.Instance.LeaveChatAsync(_partnerUser);
                
                // Unsubscribe all events
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
                
                // Unsubscribe transfer events (CRITICAL - was missing!)
                ApiService.Instance.OnImageRequestReceived -= OnImageRequestReceived;
                ApiService.Instance.OnImageRequestSent -= OnImageRequestSent;
                ApiService.Instance.OnImageTransferUpdated -= OnImageTransferUpdated;
                ApiService.Instance.OnVideoRequestReceived -= OnVideoRequestReceived;
                ApiService.Instance.OnVideoRequestSent -= OnVideoRequestSent;
                ApiService.Instance.OnVideoTransferUpdated -= OnVideoTransferUpdated;
                ApiService.Instance.OnAudioRequestReceived -= OnAudioRequestReceived;
                ApiService.Instance.OnAudioRequestSent -= OnAudioRequestSent;
                ApiService.Instance.OnAudioTransferUpdated -= OnAudioTransferUpdated;
                ApiService.Instance.OnFileRequestReceived -= OnFileRequestReceived;
                ApiService.Instance.OnFileRequestSent -= OnFileRequestSent;
                ApiService.Instance.OnFileTransferUpdated -= OnFileTransferUpdated;
            };
            
            // Initialize WPF native messages
            MessagesListBox.ItemsSource = _messages;
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
                ToastService.Error("Impossible d'appeler cet utilisateur.");
                return;
            }
            
            // Check if partner is offline (status 6 = Hors ligne)
            string displayName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : _partnerUser;
            if (_partnerStatus == 6)
            {
                new CustomAlertWindow(
                    $"{displayName} est actuellement hors ligne.\n\nVous ne pouvez pas l'appeler pour le moment.",
                    "Utilisateur hors ligne"
                ).ShowDialog();
                return;
            }
            
            // Check if partner is already in a call
            if (_partnerStatus == 3) // En appel
            {
                ShowUserInCallDialog(displayName);
                return;
            }
            
            var win = new VoiceCallWindow(_voiceService, _partnerUser, false);
            win.Show();
            _voiceService.RequestCall(_partnerUser);
        }

        private void VideoCall_Click(object sender, RoutedEventArgs e)
        {
            if (_isBlocked || _isBlockedByPartner)
            {
                ToastService.Error("Impossible d'appeler cet utilisateur.");
                return;
            }

            if (_videoService == null)
            {
                ToastService.Error("Service vidéo non disponible");
                return;
            }

            if (_videoService.IsCallActive)
            {
                ToastService.Warning("Un appel vidéo est déjà en cours");
                return;
            }

            // Check if partner is offline (status 6 = Hors ligne)
            string displayName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : _partnerUser;
            if (_partnerStatus == 6)
            {
                new CustomAlertWindow(
                    $"{displayName} est actuellement hors ligne.\n\nVous ne pouvez pas l'appeler pour le moment.",
                    "Utilisateur hors ligne"
                ).ShowDialog();
                return;
            }

            // Check if partner is already in a call
            if (_partnerStatus == 3) // En appel
            {
                ShowUserInCallDialog(displayName);
                return;
            }

            // Open video call window - use display name for UI, username for signaling
            var videoWindow = new VideoCallWindow(_videoService, _partnerUser, displayName, _partnerAvatarPath);
            videoWindow.Show();
        }

        private void ShowUserInCallDialog(string displayName)
        {
            new CustomAlertWindow(
                $"📞 {displayName} est actuellement en appel.\n\nVeuillez réessayer plus tard.",
                "Utilisateur en appel"
            ).ShowDialog();
        }

        private void OnImageRequestReceived(int id, string sender, string filename, string url)
        {
            if (sender == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    try { _messageSound?.Play(); } catch { }
                    string senderDisplayName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : sender;
                    var msg = ChatMessageItem.CreateFileTransfer(
                        id, senderDisplayName, false, filename, url, "Image", TransferStatus.Pending, DateTime.Now);
                    msg.Type = ChatMessageType.Image;
                    _messages.Add(msg);
                    ScrollToBottom();
                });
            }
        }

        private void OnImageRequestSent(int id, string receiver, string filename, string url)
        {
            if (string.Equals(receiver, _partnerUser, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() =>
                {
                    var msg = ChatMessageItem.CreateFileTransfer(
                        id, _currentUserDisplayName, true, filename, url, "Image", TransferStatus.Pending, DateTime.Now);
                    msg.Type = ChatMessageType.Image;
                    _messages.Add(msg);
                    ScrollToBottom();
                });
            }
        }

        private void OnImageTransferUpdated(int id, bool isAccepted, string url)
        {
            Dispatcher.Invoke(() =>
            {
                var msg = _messages.FirstOrDefault(m => m.TransferId == id);
                if (msg != null)
                {
                    msg.TransferStatus = isAccepted ? TransferStatus.Accepted : TransferStatus.Declined;
                    if (isAccepted && !string.IsNullOrEmpty(url))
                    {
                        msg.ImageUrl = url;
                    }
                    
                    // Force template refresh by removing and re-adding the item
                    int index = _messages.IndexOf(msg);
                    if (index >= 0)
                    {
                        _messages.RemoveAt(index);
                        _messages.Insert(index, msg);
                    }
                }
            });
        }

        private void OnVideoRequestReceived(int id, string sender, string filename, string url)
        {
            if (sender == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    try { _messageSound?.Play(); } catch { }
                    string senderDisplayName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : sender;
                    var msg = ChatMessageItem.CreateFileTransfer(
                        id, senderDisplayName, false, filename, url, "Video", TransferStatus.Pending, DateTime.Now);
                    msg.Type = ChatMessageType.Video;
                    msg.VideoUrl = url;
                    _messages.Add(msg);
                    ScrollToBottom();
                });
            }
        }

        private void OnVideoRequestSent(int id, string receiver, string filename, string url)
        {
            if (string.Equals(receiver, _partnerUser, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() =>
                {
                    var msg = ChatMessageItem.CreateFileTransfer(
                        id, _currentUserDisplayName, true, filename, url, "Video", TransferStatus.Pending, DateTime.Now);
                    msg.Type = ChatMessageType.Video;
                    msg.VideoUrl = url;
                    _messages.Add(msg);
                    ScrollToBottom();
                });
            }
        }

        private void OnVideoTransferUpdated(int id, bool isAccepted, string url)
        {
            Dispatcher.Invoke(() =>
            {
                var msg = _messages.FirstOrDefault(m => m.TransferId == id);
                if (msg != null)
                {
                    msg.TransferStatus = isAccepted ? TransferStatus.Accepted : TransferStatus.Declined;
                    if (isAccepted && !string.IsNullOrEmpty(url))
                    {
                        msg.VideoUrl = url;
                    }
                    
                    // Force template refresh by removing and re-adding the item
                    int index = _messages.IndexOf(msg);
                    if (index >= 0)
                    {
                        _messages.RemoveAt(index);
                        _messages.Insert(index, msg);
                    }
                }
            });
        }

        private void OnAudioRequestReceived(int id, string sender, string filename, string url)
        {
            if (sender == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    try { _messageSound?.Play(); } catch { }
                    string senderDisplayName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : sender;
                    var msg = ChatMessageItem.CreateFileTransfer(
                        id, senderDisplayName, false, filename, url, "Audio", TransferStatus.Pending, DateTime.Now);
                    msg.Type = ChatMessageType.AudioFile;  // Use AudioFile for transferred audio
                    msg.AudioUrl = url;
                    _messages.Add(msg);
                    ScrollToBottom();
                });
            }
        }

        private void OnAudioRequestSent(int id, string receiver, string filename, string url)
        {
            if (string.Equals(receiver, _partnerUser, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() =>
                {
                    var msg = ChatMessageItem.CreateFileTransfer(
                        id, _currentUserDisplayName, true, filename, url, "Audio", TransferStatus.Pending, DateTime.Now);
                    msg.Type = ChatMessageType.AudioFile;  // Use AudioFile for transferred audio
                    msg.AudioUrl = url;
                    _messages.Add(msg);
                    ScrollToBottom();
                });
            }
        }

        private void OnAudioTransferUpdated(int id, bool isAccepted, string url)
        {
            Dispatcher.Invoke(() =>
            {
                var msg = _messages.FirstOrDefault(m => m.TransferId == id);
                if (msg != null)
                {
                    msg.TransferStatus = isAccepted ? TransferStatus.Accepted : TransferStatus.Declined;
                    if (isAccepted && !string.IsNullOrEmpty(url))
                    {
                        msg.AudioUrl = url;
                    }
                    
                    // Force template refresh by removing and re-adding the item
                    int index = _messages.IndexOf(msg);
                    if (index >= 0)
                    {
                        _messages.RemoveAt(index);
                        _messages.Insert(index, msg);
                    }
                }
            });
        }

        private void OnFileRequestReceived(int id, string sender, string filename, string url)
        {
            if (sender == _partnerUser)
            {
                Dispatcher.Invoke(() =>
                {
                    try { _messageSound?.Play(); } catch { }
                    string senderDisplayName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : sender;
                    var msg = ChatMessageItem.CreateFileTransfer(
                        id, senderDisplayName, false, filename, url, "Fichier", TransferStatus.Pending, DateTime.Now);
                    _messages.Add(msg);
                    ScrollToBottom();
                });
            }
        }

        private void OnFileRequestSent(int id, string receiver, string filename, string url)
        {
            if (string.Equals(receiver, _partnerUser, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() =>
                {
                    var msg = ChatMessageItem.CreateFileTransfer(
                        id, _currentUserDisplayName, true, filename, url, "Fichier", TransferStatus.Pending, DateTime.Now);
                    _messages.Add(msg);
                    ScrollToBottom();
                });
            }
        }

        private void OnFileTransferUpdated(int id, bool isAccepted, string url)
        {
            Dispatcher.Invoke(() =>
            {
                var msg = _messages.FirstOrDefault(m => m.TransferId == id);
                if (msg != null)
                {
                    msg.TransferStatus = isAccepted ? TransferStatus.Accepted : TransferStatus.Declined;
                    if (isAccepted && !string.IsNullOrEmpty(url))
                    {
                        msg.FileUrl = url;
                    }
                    
                    // Force template refresh by removing and re-adding the item
                    int index = _messages.IndexOf(msg);
                    if (index >= 0)
                    {
                        _messages.RemoveAt(index);
                        _messages.Insert(index, msg);
                    }
                }
            });
        }

        private void OnUserStatusChanged(string username, string status)
        {
            if (username == _partnerUser)
            {
                Dispatcher.Invoke(() => 
                {
                    PartnerStatus.Text = status;
                    _isPartnerOnline = status == "En ligne";
                    CheckBuzzAvailability();

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

                    // Add status message to chat with status-appropriate color
                    string displayName = PartnerName.Text;
                    string message = $"Ton ami {displayName} est passé en {status}";

                    if (status == "En ligne")
                        message = $"Ton ami {displayName} est de retour En ligne";

                    string statusClass = status switch
                    {
                        "En ligne" => "status-online",
                        "Occupé" => "status-busy",
                        "Absent" => "status-away",
                        "En appel" => "status-call",
                        "Ne pas déranger" => "status-dnd",
                        _ => "status-offline"
                    };

                    var statusMsg = ChatMessageItem.CreateStatusMessage(message, statusClass);
                    _messages.Add(statusMsg);
                    ScrollToBottom();
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

            // Show message locally
            string msg = "======== Vous avez envoyé un BUZZ à " + PartnerName.Text + " ========";
            var buzzMsg = ChatMessageItem.CreateBuzzMessage(msg, true, DateTime.Now);
            _messages.Add(buzzMsg);
            ScrollToBottom();

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

                // Show message
                string name = string.IsNullOrEmpty(PartnerName.Text) ? _partnerUser : PartnerName.Text;
                string msg = $"======== {name} vous a envoyé un BUZZ ========";
                var buzzMsg = ChatMessageItem.CreateBuzzMessage(msg, false, DateTime.Now);
                _messages.Add(buzzMsg);
                ScrollToBottom();
            });
        }

        private void PlayBuzzSound()
        {
            try
            {
                string soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sounds", "doorbell.mp3");
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

                    string text = $"🔒 Vous avez bloqué {PartnerName.Text} • Blocage permanent • {DateTime.Now:HH:mm}";
                    var blockMsg = ChatMessageItem.CreateBlockMessage(text, true, DateTime.Now);
                    _messages.Add(blockMsg);
                    ScrollToBottom();
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

                    string text = $"🔒 {PartnerName.Text} vous a bloqué • Blocage permanent • {DateTime.Now:HH:mm}";
                    var blockMsg = ChatMessageItem.CreateBlockMessage(text, false, DateTime.Now);
                    _messages.Add(blockMsg);
                    ScrollToBottom();
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

                    string text = $"🔓 Vous avez débloqué {PartnerName.Text} • Accès rétabli • {DateTime.Now:HH:mm}";
                    var statusMsg = ChatMessageItem.CreateStatusMessage(text, "status-unblock");
                    _messages.Add(statusMsg);
                    ScrollToBottom();
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

                    string text = $"🔓 {PartnerName.Text} vous a débloqué • Accès rétabli • {DateTime.Now:HH:mm}";
                    var statusMsg = ChatMessageItem.CreateStatusMessage(text, "status-unblock");
                    _messages.Add(statusMsg);
                    ScrollToBottom();
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
                Dispatcher.Invoke(() =>
                {
                    _messages.Clear();
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
                    _partnerAvatarPath = profile.AvatarPath;
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

                    // Use display name instead of username
                    string senderDisplayName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : sender;
                    var msg = CreateMessageFromContent(message, senderDisplayName, false, id, DateTime.Now);
                    _messages.Add(msg);
                    ScrollToBottom();
                    
                    if (!this.IsActive)
                    {
                        _messageSound?.Play();
                    }
                    else
                    {
                        await ApiService.Instance.MarkMessagesAsReadAsync(_partnerUser);
                    }
                });
            }
        }

        private void OnAudioListened(int messageId)
        {
            Dispatcher.Invoke(() =>
            {
                var msg = _messages.FirstOrDefault(m => m.Id == messageId);
                if (msg != null)
                {
                    msg.IsAudioListened = true;
                }
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
                    
                    // Add to UI immediately
                    var msg = ChatMessageItem.CreateAudioMessage(_currentUserDisplayName, true, url, 
                        TimeSpan.FromSeconds(_recordingSeconds).ToString(@"m\:ss"), DateTime.Now);
                    _messages.Add(msg);
                    ScrollToBottom();
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
                        try 
                        { 
                            await ApiService.Instance.SendVideoRequestAsync(_partnerUser, fullUrl, fileInfo.Name, fileInfo.Length);
                            // Scroll après envoi pour garantir visibilité
                            await Task.Delay(100);
                            ScrollToBottom();
                        }
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
                        try 
                        { 
                            await ApiService.Instance.SendAudioRequestAsync(_partnerUser, fullUrl, fileInfo.Name, fileInfo.Length);
                            await Task.Delay(100);
                            ScrollToBottom();
                        }
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
                        try 
                        { 
                            await ApiService.Instance.SendImageRequestAsync(_partnerUser, fullUrl, fileInfo.Name, fileInfo.Length);
                            await Task.Delay(100);
                            ScrollToBottom();
                        }
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
                        try 
                        { 
                            await ApiService.Instance.SendFileRequestAsync(_partnerUser, fullUrl, fileInfo.Name, fileInfo.Length);
                            await Task.Delay(100);
                            ScrollToBottom();
                        }
                        catch (Exception ex) { MessageBox.Show($"Erreur: {ex.Message}"); }
                    }
                    else
                    {
                        MessageBox.Show("Échec du téléchargement du fichier.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void InitializeChat()
        {
            // Load history
            _ = LoadHistoryAsync();
        }

        private async Task LoadHistoryAsync()
        {
            // Mark messages as read
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
                                string senderDisplayName = isMine ? _currentUserDisplayName : 
                                    (!string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : msg.Sender);
                                
                                // Detect file type by extension
                                string ext = System.IO.Path.GetExtension(filename)?.ToLower() ?? "";
                                bool isImage = ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".bmp" || ext == ".webp";
                                bool isVideo = ext == ".mp4" || ext == ".avi" || ext == ".mov" || ext == ".mkv" || ext == ".webm";
                                bool isAudio = ext == ".mp3" || ext == ".wav" || ext == ".ogg" || ext == ".m4a" || ext == ".aac" || ext == ".wma" || ext == ".flac";
                                
                                var fileMsg = ChatMessageItem.CreateFileTransfer(
                                    id, senderDisplayName, isMine, filename, url, 
                                    isImage ? "Image" : isVideo ? "Video" : isAudio ? "Audio" : "Fichier", 
                                    (TransferStatus)status, msg.Timestamp);
                                
                                // Set the correct type for proper template selection
                                if (isImage)
                                {
                                    fileMsg.Type = ChatMessageType.Image;
                                    fileMsg.ImageUrl = url;
                                }
                                else if (isVideo)
                                {
                                    fileMsg.Type = ChatMessageType.Video;
                                    fileMsg.VideoUrl = url;
                                }
                                else if (isAudio)
                                {
                                    fileMsg.Type = ChatMessageType.AudioFile;
                                    fileMsg.AudioUrl = url;
                                    // Set a default duration display for audio files
                                    fileMsg.AudioDuration = 0;
                                }
                                
                                _messages.Add(fileMsg);
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
                                string senderDisplayName = isMine ? _currentUserDisplayName : 
                                    (!string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : msg.Sender);

                                var fileMsg = ChatMessageItem.CreateFileTransfer(
                                    id, senderDisplayName, isMine, filename, url, "Fichier", 
                                    TransferStatus.Pending, msg.Timestamp);
                                _messages.Add(fileMsg);
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
                        text = $"🔒 Vous avez bloqué {partnerName} – Blocage PERMANENT – {msg.Timestamp:dd/MM/yyyy HH:mm}";
                    else
                        text = $"🔒 {partnerName} vous a bloqué – Blocage PERMANENT – {msg.Timestamp:dd/MM/yyyy HH:mm}";
                    
                    var blockMsg = ChatMessageItem.CreateBlockMessage(text, msg.Sender == _currentUser, msg.Timestamp);
                    _messages.Add(blockMsg);
                }
                else if (msg.Content == "[SYSTEM_UNBLOCK]")
                {
                    string text;
                    string partnerName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : _partnerUser;

                    if (msg.Sender == _currentUser)
                        text = $"🔓 Vous avez débloqué {partnerName} – {msg.Timestamp:dd/MM/yyyy HH:mm}";
                    else
                        text = $"🔓 {partnerName} vous a débloqué – {msg.Timestamp:dd/MM/yyyy HH:mm}";
                    
                    var statusMsg = ChatMessageItem.CreateStatusMessage(text, "status-unblock");
                    statusMsg.Timestamp = msg.Timestamp;
                    _messages.Add(statusMsg);
                }
                else
                {
                    // Use display name instead of username
                    string senderDisplayName;
                    if (msg.Sender == _currentUser)
                        senderDisplayName = _currentUserDisplayName;
                    else
                        senderDisplayName = !string.IsNullOrEmpty(PartnerName.Text) ? PartnerName.Text : msg.Sender;
                    
                    var chatMsg = CreateMessageFromContent(msg.Content, senderDisplayName, msg.Sender == _currentUser, msg.Id, msg.Timestamp);
                    _messages.Add(chatMsg);
                }
                if (msg.Id > _lastMessageId) _lastMessageId = msg.Id;
            }
            
            // Initial Status Message (at the bottom)
            UpdateStatusMessageInChat();
            ScrollToBottom();

            // Mark as read
            await ApiService.Instance.MarkMessagesAsReadAsync(_partnerUser);
        }

        private void UpdateStatusMessageInChat()
        {
            string status = PartnerStatus.Text;
            string name = PartnerName.Text;
            string message = $"{name} est actuellement {status}";
            
            string statusClass = status switch
            {
                "En ligne" => "status-online",
                "Occupé" => "status-busy",
                "Absent" => "status-away",
                "En appel" => "status-call",
                "Ne pas déranger" => "status-dnd",
                _ => "status-offline"
            };
            
            var statusMsg = ChatMessageItem.CreateStatusMessage(message, statusClass);
            _messages.Add(statusMsg);
        }

        private ChatMessageItem CreateMessageFromContent(string content, string sender, bool isMine, int id, DateTime timestamp)
        {
            // Audio Message - process BEFORE emoji conversion to preserve URLs
            if (content.StartsWith("[AUDIO_MSG]"))
            {
                var parts = content.Substring(11).Split('|');
                var url = parts[0];
                var duration = parts.Length > 1 ? parts[1] : "0";
                var sec = int.Parse(duration);
                var min = sec / 60;
                var s = sec % 60;
                var timeStr = $"{min}:{s:D2}";
                
                var msg = ChatMessageItem.CreateAudioMessage(sender, isMine, url, timeStr, timestamp);
                msg.Id = id;
                return msg;
            }
            
            // Legacy Image - process BEFORE emoji conversion to preserve URLs
            if (content.StartsWith("[IMAGE]"))
            {
                var url = content.Substring(7);
                var msg = ChatMessageItem.CreateImageMessage(sender, isMine, url, timestamp);
                msg.Id = id;
                return msg;
            }
            
            // Convert emoji text patterns ONLY for regular text messages
            content = EmojiConverter.ConvertEmojis(content);
            
            // Regular Text Message
            return ChatMessageItem.CreateTextMessage(sender, isMine, content, timestamp, id);
        }

        private void ScrollToBottom()
        {
            // Use BeginInvoke with lower priority to ensure UI has rendered the new item first
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                if (MessagesListBox.Items.Count > 0)
                {
                    MessagesListBox.ScrollIntoView(MessagesListBox.Items[MessagesListBox.Items.Count - 1]);
                }
            }));
        }

        // WPF Event Handlers for Templates
        private async void Image_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string url)
            {
                await OpenMediaAsync(url, ".jpg");
            }
        }

        // Video playback state tracking
        private MediaElement? _currentVideoPlayer = null;
        private DateTime _lastVideoClickTime = DateTime.MinValue;
        private const int DoubleClickThresholdMs = 300;

        private async void Video_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string url)
            {
                await OpenMediaAsync(url, ".mp4");
            }
        }

        private void VideoPlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Track for double-click detection
            e.Handled = false;
        }

        private async void VideoPlay_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border overlay && overlay.Tag is string url)
            {
                // Check for double-click
                var now = DateTime.Now;
                if ((now - _lastVideoClickTime).TotalMilliseconds < DoubleClickThresholdMs)
                {
                    // Double-click: Open in external player
                    _lastVideoClickTime = DateTime.MinValue;
                    
                    // Pause the video in chat before opening external player
                    if (overlay.Parent is Grid container)
                    {
                        var mediaElement = FindChild<MediaElement>(container);
                        if (mediaElement != null)
                        {
                            mediaElement.Pause();
                            SetVideoPlayButtonVisible(overlay, true);
                        }
                    }
                    
                    await OpenMediaAsync(url, ".mp4");
                    return;
                }
                _lastVideoClickTime = now;

                // Single click: Play/Pause in embedded player
                try
                {
                    // Find the MediaElement in the parent Grid
                    if (overlay.Parent is Grid container)
                    {
                        var mediaElement = FindChild<MediaElement>(container);
                        if (mediaElement != null)
                        {
                            // Stop any other playing video and show its overlay
                            if (_currentVideoPlayer != null && _currentVideoPlayer != mediaElement)
                            {
                                _currentVideoPlayer.Stop();
                                ShowVideoOverlay(_currentVideoPlayer, true);
                            }

                            // Toggle play/pause
                            var state = GetMediaState(mediaElement);
                            if (state == MediaState.Play)
                            {
                                mediaElement.Pause();
                                // Show play button when paused
                                SetVideoPlayButtonVisible(overlay, true);
                            }
                            else
                            {
                                // Set source if needed
                                if (mediaElement.Source == null && !string.IsNullOrEmpty(url))
                                {
                                    mediaElement.Source = new Uri(url);
                                }
                                mediaElement.Play();
                                _currentVideoPlayer = mediaElement;
                                
                                // Hide play button when playing (but keep overlay clickable)
                                SetVideoPlayButtonVisible(overlay, false);
                                
                                // Scroll to make the video visible
                                mediaElement.BringIntoView();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Video play error: {ex.Message}");
                }
            }
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaElement mediaElement)
            {
                mediaElement.Stop();
                mediaElement.Position = TimeSpan.Zero;
                // Show play button when video ends
                ShowVideoOverlay(mediaElement, true);
            }
        }

        /// <summary>
        /// Show or hide the video play button for a MediaElement
        /// </summary>
        private void ShowVideoOverlay(MediaElement mediaElement, bool show)
        {
            if (mediaElement.Parent is Grid container)
            {
                // Find the overlay Border
                foreach (var child in container.Children)
                {
                    if (child is Border border && border.Name == "VideoOverlay")
                    {
                        SetVideoPlayButtonVisible(border, show);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Set visibility of play button elements inside overlay
        /// </summary>
        private void SetVideoPlayButtonVisible(Border overlay, bool visible)
        {
            // Set overlay background
            overlay.Background = visible 
                ? new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x00, 0x00))  // Semi-transparent when paused
                : new SolidColorBrush(Colors.Transparent);  // Transparent when playing
            
            // Find and hide/show the PlayButtonContainer
            if (overlay.Child is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is Grid playButtonContainer && playButtonContainer.Name == "PlayButtonContainer")
                    {
                        playButtonContainer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                    }
                    // Also hide the bottom bar during playback
                    if (child is Border bottomBar && bottomBar.VerticalAlignment == VerticalAlignment.Bottom)
                    {
                        bottomBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }

        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                var found = FindChild<T>(child);
                if (found != null)
                    return found;
            }
            return null;
        }

        private enum MediaState { Play, Pause, Stop }
        
        private MediaState GetMediaState(MediaElement media)
        {
            try
            {
                var hlp = typeof(MediaElement).GetField("_helper", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hlp != null)
                {
                    var helperObj = hlp.GetValue(media);
                    if (helperObj != null)
                    {
                        var stateField = helperObj.GetType().GetField("_currentState", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (stateField != null)
                        {
                            var state = stateField.GetValue(helperObj);
                            if (state != null && state.ToString() == "Play")
                                return MediaState.Play;
                        }
                    }
                }
            }
            catch { }
            return MediaState.Stop;
        }

        private void AudioPlay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // Get DataContext which contains the message data
                var dataContext = btn.DataContext as ChatMessageItem;
                
                // Check if this audio is already playing - toggle pause
                if (dataContext != null && dataContext == _currentlyPlayingAudio && dataContext.IsPlaying)
                {
                    // Pause the current audio
                    _audioPlayer.Pause();
                    dataContext.IsPlaying = false;
                    return;
                }
                
                // Get URL from DataContext directly (more reliable than Tag binding)
                string? url = dataContext?.FileUrl;
                
                // Fallback to Tag if DataContext doesn't work
                if (string.IsNullOrEmpty(url))
                {
                    url = btn.Tag as string;
                }
                
                if (string.IsNullOrEmpty(url))
                {
                    MessageBox.Show($"URL audio non disponible.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                try
                {
                    // Stop previous audio if different
                    if (_currentlyPlayingAudio != null && _currentlyPlayingAudio != dataContext)
                    {
                        _audioPlayer.Stop();
                        _currentlyPlayingAudio.IsPlaying = false;
                    }
                    
                    // Check if we're resuming the same audio that was paused
                    if (dataContext != null && dataContext == _currentlyPlayingAudio && !dataContext.IsPlaying)
                    {
                        // Resume playback
                        _audioPlayer.Play();
                        dataContext.IsPlaying = true;
                        return;
                    }
                    
                    // Build absolute URL if relative
                    string absoluteUrl = url;
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    {
                        // Relative URL - prepend API base URL
                        var baseUrl = ApiService.Instance.GetBaseUrl().TrimEnd('/');
                        absoluteUrl = url.StartsWith("/") ? $"{baseUrl}{url}" : $"{baseUrl}/{url}";
                    }
                    
                    _audioPlayer.Open(new Uri(absoluteUrl, UriKind.Absolute));
                    _audioPlayer.Play();
                    
                    // Update IsPlaying
                    if (dataContext != null)
                    {
                        dataContext.IsPlaying = true;
                        _currentlyPlayingAudio = dataContext;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erreur de lecture audio: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void AcceptTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int transferId)
            {
                var msg = _messages.FirstOrDefault(m => m.TransferId == transferId);
                if (msg != null)
                {
                    msg.TransferStatus = TransferStatus.Accepted;
                    
                    // Call appropriate API based on type
                    if (msg.Type == ChatMessageType.Image)
                        await ApiService.Instance.RespondToImageRequestAsync(transferId, true);
                    else if (msg.Type == ChatMessageType.Video)
                        await ApiService.Instance.RespondToVideoRequestAsync(transferId, true);
                    else if (msg.Type == ChatMessageType.AudioMessage || msg.Type == ChatMessageType.AudioFile)
                        await ApiService.Instance.RespondToAudioRequestAsync(transferId, true);
                    else
                        await ApiService.Instance.RespondToFileRequestAsync(transferId, true);
                }
            }
        }

        private async void DeclineTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int transferId)
            {
                var msg = _messages.FirstOrDefault(m => m.TransferId == transferId);
                if (msg != null)
                {
                    msg.TransferStatus = TransferStatus.Declined;
                    
                    // Call appropriate API based on type
                    if (msg.Type == ChatMessageType.Image)
                        await ApiService.Instance.RespondToImageRequestAsync(transferId, false);
                    else if (msg.Type == ChatMessageType.Video)
                        await ApiService.Instance.RespondToVideoRequestAsync(transferId, false);
                    else if (msg.Type == ChatMessageType.AudioMessage || msg.Type == ChatMessageType.AudioFile)
                        await ApiService.Instance.RespondToAudioRequestAsync(transferId, false);
                    else
                        await ApiService.Instance.RespondToFileRequestAsync(transferId, false);
                }
            }
        }

        private async void OpenFile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ChatMessageItem msg)
            {
                // L'expéditeur peut toujours ouvrir, le destinataire seulement si accepté
                if (!msg.IsMine && msg.TransferStatus != TransferStatus.Accepted)
                {
                    return; // Le destinataire ne peut pas ouvrir avant acceptation
                }
                
                string? url = msg.FileUrl;
                if (string.IsNullOrEmpty(url))
                {
                    return;
                }
                
                // Obtenir l'extension du fichier original
                string ext = System.IO.Path.GetExtension(msg.FileName);
                if (string.IsNullOrEmpty(ext))
                {
                    ext = ".bin"; // Extension par défaut
                }
                
                await OpenMediaAsync(url, ext);
            }
        }

        private async Task OpenMediaAsync(string url, string defaultExt)
        {
            try
            {
                string ext = System.IO.Path.GetExtension(url);
                if (string.IsNullOrEmpty(ext)) ext = defaultExt;
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
                MessageBox.Show($"Impossible d'ouvrir le fichier : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadMore_Click(object sender, RoutedEventArgs e)
        {
            // API ne supporte pas la pagination pour le moment
            // Cacher le bouton car tous les messages sont déjà chargés
            LoadMoreButton.Visibility = Visibility.Collapsed;
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
            // Convert content and create ChatMessageItem
            var chatItem = CreateMessageFromContent(msg.Content, msg.IsMine ? _currentUser : _partnerUser, msg.IsMine, msg.Id, msg.Timestamp);
            _messages.Add(chatItem);
            ScrollToBottom();
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

        #region Context Menu - Copier / Répondre
        
        /// <summary>
        /// Copie le contenu du message dans le presse-papiers
        /// </summary>
        private void CopyMessage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem menuItem && menuItem.Tag is ChatMessageItem message)
                {
                    // Nettoyer le contenu HTML pour ne garder que le texte
                    string cleanContent = StripHtmlTags(message.Content);
                    Clipboard.SetText(cleanContent);
                    ToastService.Success("Message copié !", "📋");
                }
            }
            catch (Exception ex)
            {
                ToastService.Error($"Erreur lors de la copie : {ex.Message}");
            }
        }

        /// <summary>
        /// Ajoute une citation du message dans la zone de saisie
        /// </summary>
        private void ReplyMessage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem menuItem && menuItem.Tag is ChatMessageItem message)
                {
                    string cleanContent = StripHtmlTags(message.Content);
                    string senderName = message.IsMine ? "Moi" : message.SenderName;
                    
                    // Tronquer le message s'il est trop long
                    string truncatedContent = cleanContent.Length > 100 
                        ? cleanContent.Substring(0, 100) + "..." 
                        : cleanContent;
                    
                    // Créer la citation
                    string quote = $"« {truncatedContent} » — {senderName}\n\n";
                    
                    // Ajouter au début de la zone de saisie
                    var textRange = new TextRange(MessageInput.Document.ContentStart, MessageInput.Document.ContentStart);
                    textRange.Text = quote;
                    
                    // Mettre le focus sur la zone de saisie
                    MessageInput.Focus();
                    MessageInput.CaretPosition = MessageInput.Document.ContentEnd;
                }
            }
            catch (Exception ex)
            {
                ToastService.Error($"Erreur : {ex.Message}");
            }
        }

        /// <summary>
        /// Supprime les balises HTML du texte
        /// </summary>
        private string StripHtmlTags(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            
            // Supprimer les balises HTML
            string result = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", "");
            
            // Supprimer les smileys [smiley:xxx]
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\[smiley:[^\]]+\]", "😊");
            
            // Décoder les entités HTML
            result = System.Net.WebUtility.HtmlDecode(result);
            
            return result.Trim();
        }
        
        #endregion
    }

    public class ChatMessage
    {
        public int Id { get; set; }
        public string Content { get; set; } = "";
        public bool IsMine { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
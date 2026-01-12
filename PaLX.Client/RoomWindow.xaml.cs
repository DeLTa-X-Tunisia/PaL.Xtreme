using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PaLX.Client.Services;
using PaLX.Client.Controls;

namespace PaLX.Client
{
    public partial class RoomWindow : Window
    {
        private readonly int _roomId;
        private readonly RoomViewModel _room;
        private readonly ApiService _apiService;
        private RoomVideoPeerService? _roomVideoService;
        private RoomVideoWindow? _videoWindow;
        private readonly Dictionary<int, PeerVideoWindow> _peerVideoWindows = new(); // Fenêtres de visionnage par userId
        private DispatcherTimer _speakingTimer; // Local user timer
        private DispatcherTimer _globalTimer;   // All users timer
        private DispatcherTimer _uptimeTimer;   // Room uptime timer
        private DateTime _speakingStartTime;
        private bool _isInvisibleMode = false;
        private bool _smileysLoaded = false;

        public ObservableCollection<RoomMemberViewModel> Members { get; set; } = new ObservableCollection<RoomMemberViewModel>();
        public ObservableCollection<RoomMessageViewModel> Messages { get; set; } = new ObservableCollection<RoomMessageViewModel>();

        public RoomWindow(RoomViewModel room, bool isInvisible = false)
        {
            InitializeComponent();
            _room = room;
            _roomId = room.Id;
            _apiService = ApiService.Instance;
            _isInvisibleMode = isInvisible;

            // Setup Header
            RoomNameText.Text = room.Name;
            CategoryText.Text = room.CategoryName;
            OwnerNameText.Text = room.OwnerName;
            
            // Afficher l'indicateur de mode invisible si activé
            if (_isInvisibleMode && InvisibleModeBadge != null)
            {
                InvisibleModeBadge.Visibility = Visibility.Visible;
            }
            
            // Show 18+ badge if adult room
            if (room.Is18Plus && AdultBadge != null)
            {
                AdultBadge.Visibility = Visibility.Visible;
            }
            
            // Show Room Settings button for authorized users
            UpdateRoomSettingsButtonVisibility();
            
            // Setup Uptime Timer
            _uptimeTimer = new DispatcherTimer();
            _uptimeTimer.Interval = TimeSpan.FromSeconds(1);
            _uptimeTimer.Tick += UptimeTimer_Tick;
            _uptimeTimer.Start();
            UptimeTimer_Tick(null, null); // Initial update

            MembersList.ItemsSource = Members;
            MessagesList.ItemsSource = Messages;

            // Local Timer Init
            _speakingTimer = new DispatcherTimer();
            _speakingTimer.Interval = TimeSpan.FromSeconds(1);
            _speakingTimer.Tick += SpeakingTimer_Tick;

            // Global Timer Init (for other speakers)
            _globalTimer = new DispatcherTimer();
            _globalTimer.Interval = TimeSpan.FromSeconds(1);
            _globalTimer.Tick += GlobalTimer_Tick;
            _globalTimer.Start();

            // Join SignalR Group (avec retry si nécessaire)
            _ = JoinRoomGroupWithRetryAsync();

            // Default Mute
            if (_apiService.VoiceService != null)
            {
                _apiService.VoiceService.SetMute(true);
            }

            LoadMembers();
            LoadMessages();
            
            // Subscribe to SignalR events
            _apiService.OnRoomMessageReceived += OnMessageReceived;
            _apiService.OnRoomUserJoined += OnUserJoined;
            _apiService.OnRoomUserLeft += OnUserLeft;
            _apiService.OnRoomMemberStatusUpdated += OnStatusUpdated;
            _apiService.OnMemberRoleUpdated += OnMemberRoleUpdated;
            
            // Subscribe to Whisper events
            _apiService.OnWhisperReceived += OnWhisperReceived;
            _apiService.OnWhisperModView += OnWhisperModView;
            
            // Initialize Room Video Service
            InitializeRoomVideoService();
            
            this.Closed += RoomWindow_Closed;
        }

        private void InitializeRoomVideoService()
        {
            try
            {
                // Vérifier que la connexion SignalR RoomHub est disponible
                if (_apiService.RoomHubConnection == null) 
                {
                    System.Diagnostics.Debug.WriteLine("[RoomVideoService] RoomHubConnection not available");
                    return;
                }
                
                // Déterminer si l'utilisateur a un abonnement premium (niveau > 7 ou abonnement actif)
                bool isPremium = _apiService.CurrentUserRoleLevel < 7 || _apiService.HasPremiumSubscription;
                
                _roomVideoService = new RoomVideoPeerService(
                    _apiService,
                    _roomId,
                    _apiService.CurrentUserId,
                    _apiService.CurrentUsername,
                    isPremium
                );
                
                // Abonnement aux événements vidéo pour la fenêtre flottante
                _roomVideoService.OnLocalVideoFrame += OnLocalVideoFrameReceived;
                _roomVideoService.OnRemoteVideoFrame += OnRemoteVideoFrameReceived;
                _roomVideoService.OnPeerCameraStarted += OnPeerCameraStarted;
                _roomVideoService.OnPeerCameraStopped += OnPeerCameraStopped;
                _roomVideoService.OnError += OnVideoError;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RoomVideoService] Init error: {ex.Message}");
            }
        }
        
        #region Video Event Handlers
        
        private void OnLocalVideoFrameReceived(BitmapSource? frame)
        {
            Dispatcher.Invoke(() =>
            {
                _videoWindow?.UpdateLocalVideo(frame, _apiService.CurrentUsername);
            });
        }
        
        private void OnRemoteVideoFrameReceived(int userId, BitmapSource? frame)
        {
            Dispatcher.Invoke(() =>
            {
                // Mettre à jour la fenêtre de visionnage si elle existe pour ce peer
                if (_peerVideoWindows.TryGetValue(userId, out var peerWindow))
                {
                    peerWindow.UpdateVideoFrame(frame);
                }
                
                // Aussi mettre à jour dans la fenêtre principale si elle affiche ce peer
                _videoWindow?.AddOrUpdateVideo(userId, "", frame, false);
            });
        }
        
        private void OnPeerCameraStarted(int userId, string username)
        {
            Dispatcher.Invoke(() =>
            {
                // Mettre à jour l'icône caméra du membre dans la liste
                var member = Members.FirstOrDefault(m => m.UserId == userId);
                if (member != null)
                {
                    member.IsCamOn = true;
                }
                
                // Si la fenêtre vidéo est déjà ouverte, elle recevra les frames automatiquement
                // Ne pas ouvrir automatiquement - l'utilisateur doit cliquer sur l'icône caméra
            });
        }
        
        private void OnPeerCameraStopped(int userId)
        {
            Dispatcher.Invoke(() =>
            {
                // Mettre à jour l'icône caméra du membre dans la liste
                var member = Members.FirstOrDefault(m => m.UserId == userId);
                if (member != null)
                {
                    member.IsCamOn = false;
                }
                
                // Retirer la vidéo de la fenêtre si elle est ouverte
                _videoWindow?.RemoveVideo(userId);
            });
        }
        
        private void OnVideoError(string error)
        {
            System.Diagnostics.Debug.WriteLine($"[RoomVideo] Error: {error}");
        }
        
        private void OpenVideoWindow()
        {
            // Vérifier si une fenêtre existe déjà (évite les doublons)
            if (_videoWindow != null)
            {
                // Si elle existe, juste la rendre visible et l'activer
                if (!_videoWindow.IsVisible)
                {
                    _videoWindow.Show();
                }
                _videoWindow.Activate();
                return;
            }
            
            // Créer une nouvelle fenêtre seulement si elle n'existe pas
            _videoWindow = new RoomVideoWindow(_room.Name);
            _videoWindow.OnCameraToggled += async (isOn) =>
            {
                    if (_roomVideoService == null) return;
                    
                    try
                    {
                        if (isOn)
                        {
                            await _roomVideoService.StartCameraAsync();
                        }
                        else
                        {
                            await _roomVideoService.StopCameraAsync();
                            _videoWindow?.RemoveLocalVideo();
                        }
                        
                        // Synchroniser le bouton dans RoomWindow
                        CamToggle.IsChecked = isOn;
                        CamIcon.Foreground = new SolidColorBrush(isOn ? Colors.Green : (Color)ColorConverter.ConvertFromString("#6B7280"));
                        
                        // Mettre à jour immédiatement le membre local pour que l'icône change
                        var currentMember = Members.FirstOrDefault(m => m.UserId == _apiService.CurrentUserId);
                        if (currentMember != null) currentMember.IsCamOn = isOn;
                        
                        // Mettre à jour le serveur (ignorer les erreurs)
                        try
                        {
                            await _apiService.UpdateRoomStatusAsync(_roomId, isOn, null, null);
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoWindow] Camera error: {ex.Message}");
                    }
                };
                
                _videoWindow.Closed += (s, e) =>
                {
                    // Exécuter le nettoyage de manière sécurisée
                    _ = CleanupVideoWindowAsync();
                };
                
                // Positionner la fenêtre à droite de RoomWindow
                _videoWindow.Left = this.Left + this.Width + 10;
                _videoWindow.Top = this.Top;
                _videoWindow.Show();
        }
        
        /// <summary>
        /// Nettoie les ressources après fermeture de la fenêtre vidéo
        /// </summary>
        private async Task CleanupVideoWindowAsync()
        {
            try
            {
                // Si la caméra était active, la désactiver
                if (_roomVideoService?.IsCameraEnabled == true)
                {
                    await _roomVideoService.StopCameraAsync();
                    
                    // Mettre à jour l'UI via le Dispatcher
                    await Dispatcher.InvokeAsync(() =>
                    {
                        CamToggle.IsChecked = false;
                        CamIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
                        
                        // Mettre à jour immédiatement le membre local pour que l'icône disparaisse
                        var currentMember = Members.FirstOrDefault(m => m.UserId == _apiService.CurrentUserId);
                        if (currentMember != null)
                        {
                            currentMember.IsCamOn = false;
                        }
                    });
                    
                    // Mettre à jour le statut sur le serveur pour notifier les autres
                    try
                    {
                        await _apiService.UpdateRoomStatusAsync(_roomId, false, null, null);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoWindow] Server update error (ignored): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoWindow] Cleanup error: {ex.Message}");
            }
            finally
            {
                _videoWindow = null;
            }
        }
        
        #endregion

        #region SignalR Group Management
        
        /// <summary>
        /// Rejoint le groupe SignalR avec retry si la connexion n'est pas prête
        /// </summary>
        private async Task JoinRoomGroupWithRetryAsync()
        {
            const int maxRetries = 3;
            const int delayMs = 1000;
            
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await _apiService.JoinRoomGroupAsync(_roomId);
                    System.Diagnostics.Debug.WriteLine($"[RoomWindow] Successfully joined room group {_roomId}");
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RoomWindow] Failed to join room group (attempt {i + 1}): {ex.Message}");
                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(delayMs);
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[RoomWindow] Could not join room group after {maxRetries} attempts");
        }
        
        #endregion

        private void UptimeTimer_Tick(object? sender, EventArgs? e)
        {
            var elapsed = DateTime.Now - _room.CreatedAt;
            // Format: "1j 2h 30m" or "02:30:00"
            if (elapsed.TotalDays >= 1)
                UptimeText.Text = $"{(int)elapsed.TotalDays}j {elapsed.Hours}h {elapsed.Minutes}m";
            else
                UptimeText.Text = elapsed.ToString(@"hh\:mm\:ss");
        }

        private void UpdateCounts()
        {
            int total = Members.Count;
            int men = Members.Count(m => m.Gender == "Male" || m.Gender == "Homme");
            int women = Members.Count(m => m.Gender == "Female" || m.Gender == "Femme");
            int other = total - men - women;
            
            TotalCountText.Text = total.ToString();
            MenCountText.Text = men.ToString();
            WomenCountText.Text = women.ToString();
            OtherCountText.Text = other.ToString();
            
            // Update sidebar badge
            if (MemberCountBadge != null) 
                MemberCountBadge.Text = total.ToString();
        }

        // Window Management
        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    // Double-click to maximize/restore
                    Maximize_Click(sender, e);
                }
                else
                {
                    this.DragMove();
                }
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                MaximizeIcon.Text = "\uE922"; // Maximize icon
                MaximizeButton.ToolTip = "Agrandir";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                MaximizeIcon.Text = "\uE923"; // Restore icon
                MaximizeButton.ToolTip = "Restaurer";
            }
        }

        /// <summary>
        /// Met à jour la visibilité du bouton de paramètres du salon
        /// Visible uniquement pour les admins système (niveaux 1-6) et les admins du salon
        /// </summary>
        private void UpdateRoomSettingsButtonVisibility()
        {
            if (RoomSettingsButton == null) return;
            
            bool canManageRoom = CanUserManageRoom();
            RoomSettingsButton.Visibility = canManageRoom ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Détermine si l'utilisateur actuel peut gérer le salon
        /// Autorisé pour:
        /// - Admins système: ServerMaster(1), ServerEditor(2), ServerSuperAdmin(3), ServerAdmin(4), ServerModerator(5), ServerHelp(6)
        /// - Admins du salon: RoomOwner, RoomSuperAdmin, RoomAdmin, RoomModerator
        /// </summary>
        private bool CanUserManageRoom()
        {
            // Vérifier si l'utilisateur est un admin système (RoleLevel 1-6)
            int systemRoleLevel = _apiService.CurrentUserRoleLevel;
            if (systemRoleLevel >= 1 && systemRoleLevel <= 6)
            {
                return true;
            }
            
            // Vérifier si l'utilisateur est le propriétaire du salon
            if (_room.OwnerId == _apiService.CurrentUserId)
            {
                return true;
            }
            
            // Vérifier si l'utilisateur a un rôle d'admin dans le salon (SuperAdmin, Admin, Moderator)
            if (!string.IsNullOrEmpty(_room.UserRole))
            {
                string role = _room.UserRole.ToLowerInvariant();
                if (role == "superadmin" || role == "admin" || role == "moderator")
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Ouvre la fenêtre de modification du salon (RoomStudioWindow)
        /// </summary>
        private void RoomSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var studioWindow = new RoomStudioWindow();
                studioWindow.Owner = this;
                studioWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RoomSettings] Error opening studio: {ex.Message}");
            }
        }

        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                Send_Click(sender, e);
            }
        }

        private void SpeakingTimer_Tick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _speakingStartTime;
            if (TimerText != null) TimerText.Text = elapsed.ToString(@"mm\:ss");
        }

        private void GlobalTimer_Tick(object? sender, EventArgs e)
        {
            foreach (var member in Members)
            {
                if (member.IsMicOn)
                {
                    var elapsed = DateTime.Now - member.LastMicOnTime;
                    member.SpeakingTime = elapsed.ToString(@"mm\:ss");
                }
                else
                {
                    member.SpeakingTime = "";
                }
            }
        }

        private async void RoomWindow_Closed(object? sender, EventArgs e)
        {
            _speakingTimer.Stop();
            _globalTimer.Stop();
            _uptimeTimer.Stop();
            _apiService.OnRoomMessageReceived -= OnMessageReceived;
            _apiService.OnRoomUserJoined -= OnUserJoined;
            _apiService.OnRoomUserLeft -= OnUserLeft;
            _apiService.OnRoomMemberStatusUpdated -= OnStatusUpdated;
            _apiService.OnMemberRoleUpdated -= OnMemberRoleUpdated;
            _apiService.OnWhisperReceived -= OnWhisperReceived;
            _apiService.OnWhisperModView -= OnWhisperModView;
            
            // Fermer toutes les fenêtres de visionnage peer
            foreach (var peerWindow in _peerVideoWindows.Values.ToList())
            {
                try { peerWindow.Close(); } catch { }
            }
            _peerVideoWindows.Clear();
            
            // Cleanup Video Window
            if (_videoWindow != null)
            {
                _videoWindow.Close();
                _videoWindow = null;
            }
            
            // Cleanup Video Service
            if (_roomVideoService != null)
            {
                _roomVideoService.OnLocalVideoFrame -= OnLocalVideoFrameReceived;
                _roomVideoService.OnRemoteVideoFrame -= OnRemoteVideoFrameReceived;
                _roomVideoService.OnPeerCameraStarted -= OnPeerCameraStarted;
                _roomVideoService.OnPeerCameraStopped -= OnPeerCameraStopped;
                _roomVideoService.OnError -= OnVideoError;
                _roomVideoService.Dispose();
                _roomVideoService = null;
            }
            
            if (_apiService.VoiceService != null)
            {
                _apiService.VoiceService.EndCall();
            }

            try
            {
                await _apiService.LeaveRoomAsync(_roomId);
            }
            catch { }

            await _apiService.LeaveRoomGroupAsync(_roomId);
        }

        private async void LoadMembers()
        {
            try
            {
                var members = await _apiService.GetRoomMembersAsync(_roomId);
                Members.Clear();
                foreach (var m in members)
                {
                    Members.Add(MapMember(m));
                    if (m.Username != _apiService.CurrentUsername && _apiService.VoiceService != null)
                    {
                        _ = _apiService.VoiceService.ConnectToPeer(m.Username);
                    }
                }
                UpdateCounts();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur chargement membres: {ex.Message}");
            }
        }

        private async void LoadMessages()
        {
            try
            {
                // Ne pas charger l'historique des messages pour les chatrooms collectifs
                // L'utilisateur voit uniquement le message de bienvenue
                Messages.Clear();
                
                // Welcome Message
                Messages.Add(new RoomMessageViewModel
                {
                    DisplayName = "Système",
                    Content = $"Bienvenu dans votre salon {_room.Name}",
                    Timestamp = DateTime.Now,
                    MessageType = "System",
                    RoleColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E90FF")) // DodgerBlue
                });

                if (Messages.Count > 0) MessagesList.ScrollIntoView(Messages.Last());
                UpdateCounts();
            }
            catch { }
        }

        /// <summary>
        /// Construit l'URL complète de l'avatar à partir d'un chemin relatif
        /// </summary>
        private string? BuildAvatarUrl(string? avatarPath)
        {
            if (string.IsNullOrEmpty(avatarPath))
                return null;
            
            // Si c'est déjà une URL complète
            if (avatarPath.StartsWith("http://") || avatarPath.StartsWith("https://"))
                return avatarPath;
            
            // Si c'est un chemin local qui existe
            if ((avatarPath.Contains(":\\") || avatarPath.StartsWith("/") || avatarPath.StartsWith("\\")) && System.IO.File.Exists(avatarPath))
                return avatarPath;
            
            // Sinon c'est un chemin relatif du serveur
            return $"{ApiService.BaseUrl}/{avatarPath.TrimStart('/', '\\')}";
        }

        private RoomMemberViewModel MapMember(RoomMemberDto m)
        {
            return new RoomMemberViewModel
            {
                UserId = m.UserId,
                Username = m.Username,
                DisplayName = m.DisplayName,
                AvatarPath = BuildAvatarUrl(m.AvatarPath) ?? string.Empty,
                RoleName = m.RoleName,
                RoleColor = new BrushConverter().ConvertFrom(m.RoleColor ?? "#808080") as SolidColorBrush ?? Brushes.Gray,
                IsMicOn = m.IsMicOn,
                IsCamOn = m.IsCamOn,
                HasHandRaised = m.HasHandRaised,
                Gender = m.Gender,
                IsInvisible = m.IsInvisible
            };
        }

        private RoomMessageViewModel MapMessage(RoomMessageDto m)
        {
            return new RoomMessageViewModel
            {
                Id = m.Id,
                DisplayName = m.DisplayName,
                AvatarPath = BuildAvatarUrl(m.AvatarPath) ?? string.Empty,
                Content = m.Content,
                Timestamp = m.Timestamp,
                RoleColor = new BrushConverter().ConvertFrom(m.RoleColor ?? "#808080") as SolidColorBrush ?? Brushes.Gray,
                RoleName = m.RoleName,
                MessageType = m.MessageType
            };
        }

        private void AddSystemMessage(string content)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new RoomMessageViewModel
                {
                    DisplayName = "Système",
                    Content = content,
                    Timestamp = DateTime.Now,
                    MessageType = "System",
                    RoleColor = Brushes.Gray
                });
                MessagesList.ScrollIntoView(Messages.Last());
                UpdateCounts();
            });
        }

        // SignalR Handlers
        private void OnMessageReceived(RoomMessageDto dto)
        {
            System.Diagnostics.Debug.WriteLine($"[RoomWindow] OnMessageReceived: RoomId={dto.RoomId}, MyRoomId={_roomId}, UserId={dto.UserId}");
            if (dto.RoomId != _roomId) 
            {
                System.Diagnostics.Debug.WriteLine($"[RoomWindow] Message ignored - wrong room");
                return;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[RoomWindow] Adding message to UI: {dto.Content?.Substring(0, Math.Min(20, dto.Content?.Length ?? 0))}...");
                Messages.Add(MapMessage(dto));
                MessagesList.ScrollIntoView(Messages.Last());
                UpdateCounts();
            });
        }

        private void OnUserJoined(RoomMemberDto member)
        {
            Application.Current.Dispatcher.Invoke(() => 
            {
                if (!Members.Any(m => m.UserId == member.UserId))
                {
                    Members.Add(MapMember(member));
                    UpdateCounts();
                    AddSystemMessage($"{member.DisplayName} a rejoint le salon.");
                }
            });
        }

        private void OnUserLeft(int userId)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var member = Members.FirstOrDefault(m => m.UserId == userId);
                if (member != null) 
                {
                    if (_apiService.VoiceService != null)
                    {
                        _apiService.VoiceService.DisconnectPeer(member.Username);
                    }
                    Members.Remove(member);
                    UpdateCounts();
                    AddSystemMessage($"{member.DisplayName} a quitté le salon.");
                }
                else
                {
                    // Force refresh if member not found (sync issue)
                    LoadMembers();
                }
            });
        }

        private void OnStatusUpdated(int userId, bool? cam, bool? mic, bool? hand)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var member = Members.FirstOrDefault(m => m.UserId == userId);
                if (member != null)
                {
                    if (cam.HasValue) member.IsCamOn = cam.Value;
                    if (mic.HasValue) member.IsMicOn = mic.Value;
                    if (hand.HasValue) 
                    {
                        if (hand.Value && !member.HasHandRaised)
                        {
                            AddSystemMessage($"{member.DisplayName} a levé la main ✋");
                        }
                        member.HasHandRaised = hand.Value;
                    }
                }
            });
        }

        /// <summary>
        /// Handler pour la mise à jour du rôle d'un membre en temps réel
        /// </summary>
        private void OnMemberRoleUpdated(int userId, string roleName, string color, string icon)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var member = Members.FirstOrDefault(m => m.UserId == userId);
                if (member != null)
                {
                    member.RoleName = roleName;
                    member.RoleColor = (SolidColorBrush)new BrushConverter().ConvertFrom(color)!;
                    AddSystemMessage($"{member.DisplayName} est maintenant {roleName}");
                }
            });
        }

        /// <summary>
        /// Handler pour la réception d'un chuchotement privé
        /// </summary>
        private void OnWhisperReceived(int roomId, int fromUserId, string fromDisplayName, string message)
        {
            System.Diagnostics.Debug.WriteLine($"[RoomWindow] OnWhisperReceived called: roomId={roomId}, _roomId={_roomId}, from={fromDisplayName}");
            
            if (roomId != _roomId) 
            {
                System.Diagnostics.Debug.WriteLine($"[RoomWindow] Whisper ignored - wrong room");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"[RoomWindow] Displaying received whisper from {fromDisplayName}");
            DisplayReceivedWhisper(fromDisplayName, message);
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            string content = ConvertRichTextBoxToHtml(MessageInput);
            string plainText = GetTextFromRichTextBox(MessageInput).Trim();
            
            if (string.IsNullOrWhiteSpace(plainText) && !content.Contains("[smiley:")) return;
            
            try
            {
                // Capture current formatting to persist it
                object fontWeight = MessageInput.Selection.GetPropertyValue(TextElement.FontWeightProperty);
                object fontStyle = MessageInput.Selection.GetPropertyValue(TextElement.FontStyleProperty);
                object textDecorations = MessageInput.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
                object foreground = MessageInput.Selection.GetPropertyValue(TextElement.ForegroundProperty);

                await _apiService.SendRoomMessageAsync(_roomId, content);
                
                // Clear input but preserve formatting
                MessageInput.Document.Blocks.Clear();
                var para = new Paragraph();
                var run = new Run();
                
                // Apply preserved formatting
                if (fontWeight is FontWeight fw) run.FontWeight = fw;
                if (fontStyle is FontStyle fs) run.FontStyle = fs;
                if (textDecorations is TextDecorationCollection td) run.TextDecorations = td;
                if (foreground is Brush br) run.Foreground = br;
                
                para.Inlines.Add(run);
                MessageInput.Document.Blocks.Add(para);
                MessageInput.CaretPosition = MessageInput.Document.ContentEnd;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur envoi: {ex.Message}");
            }
        }
        
        private string ConvertRichTextBoxToHtml(RichTextBox rtb)
        {
            StringBuilder sb = new StringBuilder();
            
            foreach (Block block in rtb.Document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    foreach (Inline inline in paragraph.Inlines)
                    {
                        ProcessInline(inline, sb);
                    }
                }
            }
            
            return sb.ToString().TrimEnd('\n');
        }
        
        private void ProcessInline(Inline inline, StringBuilder sb)
        {
            if (inline is InlineUIContainer uiContainer && uiContainer.Child is Image img && img.Tag is string filename)
            {
                sb.Append($"[smiley:{filename}]");
            }
            else if (inline is Run run)
            {
                if (string.IsNullOrEmpty(run.Text)) return;
                
                string text = run.Text;
                bool hasBold = run.FontWeight == FontWeights.Bold;
                bool hasItalic = run.FontStyle == FontStyles.Italic;
                bool hasUnderline = run.TextDecorations?.Contains(TextDecorations.Underline[0]) ?? false;
                bool hasColor = run.Foreground is SolidColorBrush brush && brush.Color != Colors.Black && brush.Color != ((SolidColorBrush)SystemColors.ControlTextBrush).Color;
                
                // Build HTML tags
                if (hasColor && run.Foreground is SolidColorBrush colorBrush)
                {
                    sb.Append($"<span style='color:{colorBrush.Color.ToString()}'>");
                }
                if (hasBold) sb.Append("<b>");
                if (hasItalic) sb.Append("<i>");
                if (hasUnderline) sb.Append("<u>");
                
                sb.Append(text);
                
                if (hasUnderline) sb.Append("</u>");
                if (hasItalic) sb.Append("</i>");
                if (hasBold) sb.Append("</b>");
                if (hasColor) sb.Append("</span>");
            }
            else if (inline is LineBreak)
            {
                sb.Append("\n");
            }
            else if (inline is Span span)
            {
                foreach (var child in span.Inlines)
                {
                    ProcessInline(child, sb);
                }
            }
        }
        
        private string GetTextFromRichTextBox(RichTextBox rtb)
        {
            TextRange textRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
            return textRange.Text;
        }

        private async void Leave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _apiService.LeaveRoomAsync(_roomId);
            }
            catch { }
            Close();
        }

        // Toggle Actions
        private async void ToggleMic_Click(object sender, RoutedEventArgs e)
        {
            bool newState = MicToggle.IsChecked == true;
            
            if (newState)
            {
                _speakingStartTime = DateTime.Now;
                if (SpeakingTimerPanel != null) SpeakingTimerPanel.Visibility = Visibility.Visible;
                _speakingTimer.Start();
            }
            else
            {
                _speakingTimer.Stop();
                if (SpeakingTimerPanel != null) SpeakingTimerPanel.Visibility = Visibility.Collapsed;
            }

            if (_apiService.VoiceService != null)
            {
                _apiService.VoiceService.SetMute(!newState);
            }

            await _apiService.UpdateRoomStatusAsync(_roomId, null, newState, null);
        }

        private async void ToggleCam_Click(object sender, RoutedEventArgs e)
        {
            if (_roomVideoService == null) return;
            
            bool newState = CamToggle.IsChecked == true;
            
            try
            {
                if (newState)
                {
                    // Vérifier si la limite est atteinte
                    if (_roomVideoService.ActiveCameraCount >= _roomVideoService.MaxCameras)
                    {
                        CamToggle.IsChecked = false;
                        ShowAlert($"La limite de {_roomVideoService.MaxCameras} caméras est atteinte.");
                        return;
                    }
                    
                    // Ouvrir la fenêtre vidéo flottante
                    OpenVideoWindow();
                    
                    // Activer la caméra
                    await _roomVideoService.StartCameraAsync();
                    
                    // Synchroniser l'état dans la fenêtre vidéo
                    _videoWindow?.SetCameraState(true);
                    
                    // Mise à jour visuelle
                    CamIcon.Foreground = new SolidColorBrush(Colors.Green);
                    
                    // Mettre à jour immédiatement le membre local
                    var currentMemberOn = Members.FirstOrDefault(m => m.UserId == _apiService.CurrentUserId);
                    if (currentMemberOn != null) currentMemberOn.IsCamOn = true;
                }
                else
                {
                    // Désactiver la caméra
                    await _roomVideoService.StopCameraAsync();
                    
                    // Mise à jour visuelle
                    CamIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
                    
                    // Synchroniser l'état et retirer la vidéo locale de la fenêtre flottante
                    _videoWindow?.SetCameraState(false);
                    _videoWindow?.RemoveLocalVideo();
                    
                    // Mettre à jour immédiatement le membre local
                    var currentMemberOff = Members.FirstOrDefault(m => m.UserId == _apiService.CurrentUserId);
                    if (currentMemberOff != null) currentMemberOff.IsCamOn = false;
                }
                
                // Mettre à jour le status sur le serveur (ignorer les erreurs serveur si la caméra fonctionne)
                try
                {
                    await _apiService.UpdateRoomStatusAsync(_roomId, newState, null, null);
                }
                catch (Exception serverEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[Camera] Server update error (ignored): {serverEx.Message}");
                }
            }
            catch (Exception ex)
            {
                // Erreur uniquement si la caméra n'a pas pu démarrer
                if (newState && !_roomVideoService.IsCameraEnabled)
                {
                    CamToggle.IsChecked = false;
                    System.Diagnostics.Debug.WriteLine($"[Camera] Error: {ex.Message}");
                    ShowAlert("Impossible d'accéder à la caméra.");
                }
            }
        }

        private async void ToggleHand_Click(object sender, RoutedEventArgs e)
        {
            bool newState = HandToggle.IsChecked == true;
            await _apiService.UpdateRoomStatusAsync(_roomId, null, null, newState);
        }

        // Context Menu Actions
        private void AllowSpeak_Click(object sender, RoutedEventArgs e) 
        { 
            // Placeholder: Logic to grant speaking rights if restricted
        }

        private async void MuteMic_Click(object sender, RoutedEventArgs e) 
        { 
            if (sender is MenuItem item && item.DataContext is RoomMemberViewModel member)
            {
                try 
                { 
                    await _apiService.MuteUserAsync(_roomId, member.UserId, 10); // Mute 10 min
                    MessageBox.Show($"{member.DisplayName} a été rendu muet pour 10 minutes.");
                }
                catch (Exception ex) { MessageBox.Show($"Erreur: {ex.Message}"); }
            }
        }

        private void MuteCam_Click(object sender, RoutedEventArgs e) 
        { 
            // Placeholder: Logic to disable user camera remotely
        }

        /// <summary>
        /// Ouvre la fenêtre de chuchotement pour envoyer un message privé à un membre
        /// </summary>
        private async void SendWhisper_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.DataContext is RoomMemberViewModel member)
            {
                // Ne pas chuchoter à soi-même
                if (member.UserId == _apiService.CurrentUserId)
                {
                    return;
                }

                var whisperWindow = new WhisperWindow(member.UserId, member.DisplayName)
                {
                    Owner = this
                };
                whisperWindow.ShowDialog();

                if (whisperWindow.IsSent && !string.IsNullOrEmpty(whisperWindow.WhisperMessage))
                {
                    try
                    {
                        // Obtenir le displayName de l'utilisateur courant depuis les membres
                        var currentMember = Members.FirstOrDefault(m => m.UserId == _apiService.CurrentUserId);
                        string senderDisplayName = currentMember?.DisplayName ?? _apiService.CurrentUsername;
                        
                        await _apiService.SendWhisperAsync(_roomId, member.UserId, whisperWindow.WhisperMessage, senderDisplayName);
                        
                        // Afficher le chuchotement envoyé dans le chat
                        DisplaySentWhisper(member.DisplayName, whisperWindow.WhisperMessage);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RoomWindow] Error sending whisper: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Affiche un chuchotement envoyé dans le chat (visible uniquement par l'expéditeur)
        /// </summary>
        private void DisplaySentWhisper(string recipientName, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new RoomMessageViewModel
                {
                    DisplayName = "Chuchotement",
                    Content = $"[WHISPER_SENT:{recipientName}]{message}",
                    Timestamp = DateTime.Now,
                    MessageType = "WhisperSent",
                    RoleColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53935")) // Rouge
                });
                MessagesList.ScrollIntoView(Messages.Last());
                UpdateCounts();
            });
        }

        /// <summary>
        /// Affiche un chuchotement reçu dans le chat (visible uniquement par le destinataire)
        /// </summary>
        private void DisplayReceivedWhisper(string senderName, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new RoomMessageViewModel
                {
                    DisplayName = "Chuchotement",
                    Content = $"[WHISPER_RECEIVED:{senderName}]{message}",
                    Timestamp = DateTime.Now,
                    MessageType = "WhisperReceived",
                    RoleColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E88E5")) // Bleu
                });
                MessagesList.ScrollIntoView(Messages.Last());
                UpdateCounts();
            });
        }

        /// <summary>
        /// Handler pour la vue modérateur des chuchotements (rôles 1-6 peuvent voir tous les chuchotements)
        /// </summary>
        private void OnWhisperModView(int roomId, int fromUserId, string fromDisplayName, int toUserId, string message)
        {
            System.Diagnostics.Debug.WriteLine($"[RoomWindow] OnWhisperModView called: roomId={roomId}, from={fromDisplayName} to userId={toUserId}");
            
            if (roomId != _roomId) return;
            
            // Trouver le nom du destinataire
            var recipient = Members.FirstOrDefault(m => m.UserId == toUserId);
            string recipientName = recipient?.DisplayName ?? $"User#{toUserId}";
            
            DisplayModeratorWhisper(fromDisplayName, recipientName, message);
        }

        /// <summary>
        /// Affiche un chuchotement en mode modérateur (visible par les rôles système 1-6)
        /// Montre qui chuchote à qui
        /// </summary>
        private void DisplayModeratorWhisper(string senderName, string recipientName, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new RoomMessageViewModel
                {
                    DisplayName = "Modération",
                    Content = $"[WHISPER_MOD:{senderName}:{recipientName}]{message}",
                    Timestamp = DateTime.Now,
                    MessageType = "WhisperMod",
                    RoleColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C27B0")) // Violet
                });
                MessagesList.ScrollIntoView(Messages.Last());
                UpdateCounts();
            });
        }

        /// <summary>
        /// Ouvre une fenêtre pour visionner la vidéo d'un autre participant
        /// </summary>
        private void ViewPeerCamera_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is RoomMemberViewModel member)
            {
                // Ne pas ouvrir pour soi-même
                if (member.UserId == _apiService.CurrentUserId)
                {
                    // Ouvrir sa propre fenêtre vidéo
                    OpenVideoWindow();
                    return;
                }
                
                // Vérifier si une fenêtre existe déjà pour ce peer
                if (_peerVideoWindows.TryGetValue(member.UserId, out var existingWindow))
                {
                    if (existingWindow.IsVisible)
                    {
                        existingWindow.Activate();
                        return;
                    }
                    else
                    {
                        _peerVideoWindows.Remove(member.UserId);
                    }
                }
                
                // Créer une nouvelle fenêtre de visionnage
                var peerWindow = new PeerVideoWindow(member.UserId, member.DisplayName, _roomId);
                
                // Gérer la fermeture
                peerWindow.Closed += (s, args) =>
                {
                    _peerVideoWindows.Remove(member.UserId);
                };
                
                _peerVideoWindows[member.UserId] = peerWindow;
                peerWindow.Show();
            }
        }

        private async void Kick_Click(object sender, RoutedEventArgs e) 
        { 
            if (sender is MenuItem item && item.DataContext is RoomMemberViewModel member)
            {
                if (MessageBox.Show($"Voulez-vous vraiment expulser {member.DisplayName} ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try 
                    { 
                        await _apiService.KickUserAsync(_roomId, member.UserId); 
                    }
                    catch (Exception ex) { MessageBox.Show($"Erreur: {ex.Message}"); }
                }
            }
        }

        private async void Ban_Click(object sender, RoutedEventArgs e) 
        { 
            if (sender is MenuItem item && item.DataContext is RoomMemberViewModel member)
            {
                if (MessageBox.Show($"Voulez-vous vraiment bannir {member.DisplayName} ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try 
                    { 
                        await _apiService.BanUserAsync(_roomId, member.UserId); 
                    }
                    catch (Exception ex) { MessageBox.Show($"Erreur: {ex.Message}"); }
                }
            }
        }
        
        #region Rich Text Formatting
        
        private void FormatBold_Click(object sender, RoutedEventArgs e)
        {
            var selection = MessageInput.Selection;
            if (!selection.IsEmpty)
            {
                object currentWeight = selection.GetPropertyValue(TextElement.FontWeightProperty);
                if (currentWeight is FontWeight fw && fw == FontWeights.Bold)
                    selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                else
                    selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            }
            else
            {
                // Apply to caret position for future typing
                var caretPos = MessageInput.CaretPosition;
                if (caretPos.Parent is Run run)
                {
                    run.FontWeight = run.FontWeight == FontWeights.Bold ? FontWeights.Normal : FontWeights.Bold;
                }
            }
            MessageInput.Focus();
        }
        
        private void FormatItalic_Click(object sender, RoutedEventArgs e)
        {
            var selection = MessageInput.Selection;
            if (!selection.IsEmpty)
            {
                object currentStyle = selection.GetPropertyValue(TextElement.FontStyleProperty);
                if (currentStyle is FontStyle fs && fs == FontStyles.Italic)
                    selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
                else
                    selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
            }
            else
            {
                var caretPos = MessageInput.CaretPosition;
                if (caretPos.Parent is Run run)
                {
                    run.FontStyle = run.FontStyle == FontStyles.Italic ? FontStyles.Normal : FontStyles.Italic;
                }
            }
            MessageInput.Focus();
        }
        
        private void FormatUnderline_Click(object sender, RoutedEventArgs e)
        {
            var selection = MessageInput.Selection;
            if (!selection.IsEmpty)
            {
                object currentDeco = selection.GetPropertyValue(Inline.TextDecorationsProperty);
                if (currentDeco is TextDecorationCollection td && td.Count > 0)
                    selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
                else
                    selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
            }
            else
            {
                var caretPos = MessageInput.CaretPosition;
                if (caretPos.Parent is Run run)
                {
                    run.TextDecorations = (run.TextDecorations?.Count > 0) ? null : TextDecorations.Underline;
                }
            }
            MessageInput.Focus();
        }
        
        private void Color_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string colorHex)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorHex);
                    var brush = new SolidColorBrush(color);
                    
                    var selection = MessageInput.Selection;
                    if (!selection.IsEmpty)
                    {
                        selection.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
                    }
                    else
                    {
                        var caretPos = MessageInput.CaretPosition;
                        if (caretPos.Parent is Run run)
                        {
                            run.Foreground = brush;
                        }
                    }
                    
                    BtnColor.IsChecked = false;
                    MessageInput.Focus();
                }
                catch { }
            }
        }
        
        #endregion
        
        #region Smileys
        
        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_smileysLoaded)
            {
                LoadSmileys();
                _smileysLoaded = true;
            }
            SmileyPopup.IsOpen = !SmileyPopup.IsOpen;
        }
        
        private void LoadSmileys()
        {
            SmileyPanel.Children.Clear();
            string smileyPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Smiley", "pxt_01");
            
            if (!System.IO.Directory.Exists(smileyPath)) return;
            
            var files = System.IO.Directory.GetFiles(smileyPath, "*.png")
                .OrderBy(f => {
                    string name = System.IO.Path.GetFileNameWithoutExtension(f);
                    if (int.TryParse(name, out int n)) return n;
                    return 999;
                });
            
            foreach (var file in files)
            {
                var btn = new Button
                {
                    Width = 34,
                    Height = 34,
                    Margin = new Thickness(1),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Tag = $"pxt_01/{System.IO.Path.GetFileName(file)}",
                    ToolTip = System.IO.Path.GetFileNameWithoutExtension(file)
                };
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(file, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 30;
                bitmap.EndInit();
                bitmap.Freeze();
                
                var img = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    Width = 30,
                    Height = 30
                };
                
                btn.Content = img;
                btn.Click += Smiley_Click;
                SmileyPanel.Children.Add(btn);
            }
        }
        
        private void Smiley_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filename)
            {
                string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Smiley", filename);
                
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
        
        #endregion
        
        private void ShowAlert(string message, string title = "Information")
        {
            new CustomAlertWindow(message, title).ShowDialog();
        }
    }

    public class RoomMemberViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isMicOn;
        private bool _isCamOn;
        private bool _hasHandRaised;
        private string _speakingTime = "";
        private string _roleName = "Membre";
        private Brush _roleColor = Brushes.Gray;
        private bool _isInvisible = false;

        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarPath { get; set; } = string.Empty;
        
        public string RoleName 
        { 
            get => _roleName; 
            set { _roleName = value; OnPropertyChanged(nameof(RoleName)); } 
        }
        
        public Brush RoleColor 
        { 
            get => _roleColor; 
            set { _roleColor = value; OnPropertyChanged(nameof(RoleColor)); } 
        }
        
        public bool IsInvisible
        {
            get => _isInvisible;
            set { _isInvisible = value; OnPropertyChanged(nameof(IsInvisible)); OnPropertyChanged(nameof(InvisibleIndicator)); }
        }
        
        /// <summary>
        /// Affiche 👻 devant le nom si invisible (visible seulement pour les admins qui peuvent le voir)
        /// </summary>
        public string InvisibleIndicator => IsInvisible ? "👻 " : "";
        
        public DateTime LastMicOnTime { get; set; }
        public string Gender { get; set; } = "Unknown";
        
        public bool IsMicOn 
        { 
            get => _isMicOn; 
            set 
            { 
                if (!_isMicOn && value) LastMicOnTime = DateTime.Now;
                _isMicOn = value; 
                OnPropertyChanged(nameof(IsMicOn)); 
            } 
        }
        public bool IsCamOn 
        { 
            get => _isCamOn; 
            set { _isCamOn = value; OnPropertyChanged(nameof(IsCamOn)); } 
        }
        public bool HasHandRaised 
        { 
            get => _hasHandRaised; 
            set { _hasHandRaised = value; OnPropertyChanged(nameof(HasHandRaised)); } 
        }
        
        public string SpeakingTime
        {
            get => _speakingTime;
            set { _speakingTime = value; OnPropertyChanged(nameof(SpeakingTime)); }
        }

        public bool CanModerate { get; set; } = true;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public class RoomMessageViewModel
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarPath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Brush RoleColor { get; set; } = Brushes.Gray;
        public string RoleName { get; set; } = "Membre";
        public string MessageType { get; set; } = "Text";
        
        public bool IsSystem => MessageType == "System";
        public Visibility BubbleVisibility => IsSystem ? Visibility.Collapsed : Visibility.Visible;
        public Visibility SystemVisibility => IsSystem ? Visibility.Visible : Visibility.Collapsed;
    }
}

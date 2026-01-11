using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

            // Join SignalR Group
            _apiService.JoinRoomGroupAsync(_roomId);

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
            
            // Initialize Room Video Service
            InitializeRoomVideoService();
            
            this.Closed += RoomWindow_Closed;
        }

        private void InitializeRoomVideoService()
        {
            try
            {
                // Vérifier que la connexion SignalR est disponible
                if (_apiService.HubConnection == null) 
                {
                    System.Diagnostics.Debug.WriteLine("[RoomVideoService] HubConnection not available");
                    return;
                }
                
                // Déterminer si l'utilisateur a un abonnement premium (niveau > 7 ou abonnement actif)
                bool isPremium = _apiService.CurrentUserRoleLevel < 7 || _apiService.HasPremiumSubscription;
                
                _roomVideoService = new RoomVideoPeerService(
                    _apiService.HubConnection,
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
                // Ouvrir la fenêtre vidéo si pas encore ouverte
                OpenVideoWindow();
            });
        }
        
        private void OnPeerCameraStopped(int userId)
        {
            Dispatcher.Invoke(() =>
            {
                _videoWindow?.RemoveVideo(userId);
            });
        }
        
        private void OnVideoError(string error)
        {
            System.Diagnostics.Debug.WriteLine($"[RoomVideo] Error: {error}");
        }
        
        private void OpenVideoWindow()
        {
            if (_videoWindow == null || !_videoWindow.IsVisible)
            {
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
                
                _videoWindow.Closed += async (s, e) =>
                {
                    // Si la caméra était active, la désactiver
                    if (_roomVideoService?.IsCameraEnabled == true)
                    {
                        await _roomVideoService.StopCameraAsync();
                        CamToggle.IsChecked = false;
                        CamIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
                        
                        // Mettre à jour immédiatement le membre local pour que l'icône disparaisse
                        var currentMember = Members.FirstOrDefault(m => m.UserId == _apiService.CurrentUserId);
                        if (currentMember != null)
                        {
                            currentMember.IsCamOn = false;
                        }
                        
                        // Mettre à jour le statut sur le serveur pour notifier les autres
                        await _apiService.UpdateRoomStatusAsync(_roomId, false, null, null);
                    }
                    _videoWindow = null;
                };
                
                // Positionner la fenêtre à droite de RoomWindow
                _videoWindow.Left = this.Left + this.Width + 10;
                _videoWindow.Top = this.Top;
                _videoWindow.Show();
            }
            else
            {
                _videoWindow.Activate();
            }
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

            TotalCountText.Text = total.ToString();
            MenCountText.Text = men.ToString();
            WomenCountText.Text = women.ToString();
            
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

        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(MessageInput.Text))
            {
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
                        _apiService.VoiceService.ConnectToPeer(m.Username);
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
            }
            catch (Exception ex) { }
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
                AvatarPath = BuildAvatarUrl(m.AvatarPath),
                RoleName = m.RoleName,
                RoleColor = (SolidColorBrush)new BrushConverter().ConvertFrom(m.RoleColor),
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
                AvatarPath = BuildAvatarUrl(m.AvatarPath),
                Content = m.Content,
                Timestamp = m.Timestamp,
                RoleColor = (SolidColorBrush)new BrushConverter().ConvertFrom(m.RoleColor),
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
            });
        }

        // SignalR Handlers
        private void OnMessageReceived(RoomMessageDto dto)
        {
            if (dto.RoomId != _roomId) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(MapMessage(dto));
                MessagesList.ScrollIntoView(Messages.Last());
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

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageInput.Text)) return;
            
            try
            {
                await _apiService.SendRoomMessageAsync(_roomId, MessageInput.Text);
                MessageInput.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur envoi: {ex.Message}");
            }
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
                    
                    // Retirer la vidéo locale de la fenêtre flottante
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
        public string DisplayName { get; set; }
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
        public string DisplayName { get; set; }
        public string AvatarPath { get; set; } = string.Empty;
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public Brush RoleColor { get; set; }
        public string RoleName { get; set; } = "Membre";
        public string MessageType { get; set; } = "Text";
        
        public bool IsSystem => MessageType == "System";
        public Visibility BubbleVisibility => IsSystem ? Visibility.Collapsed : Visibility.Visible;
        public Visibility SystemVisibility => IsSystem ? Visibility.Visible : Visibility.Collapsed;
    }
}

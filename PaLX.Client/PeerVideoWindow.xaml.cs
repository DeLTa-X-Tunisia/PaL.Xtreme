// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// PeerVideoWindow - Fenêtre flottante pour visionner la vidéo d'un autre participant

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PaLX.Client.Services;

namespace PaLX.Client
{
    /// <summary>
    /// Fenêtre simple pour afficher la vidéo d'un autre participant du chatroom
    /// Reçoit les frames décodées via UpdateVideoFrame() appelé par RoomWindow
    /// </summary>
    public partial class PeerVideoWindow : Window
    {
        #region Fields

        private readonly int _peerId;
        private readonly string _peerUsername;
        private readonly int _roomId;
        private readonly ApiService _apiService;
        private bool _isPinned = true;
        private bool _isReceivingVideo = false;
        private DateTime _lastFrameTime = DateTime.MinValue;
        private System.Windows.Threading.DispatcherTimer? _timeoutTimer;

        #endregion

        #region Constructor

        public PeerVideoWindow(int peerId, string peerUsername, int roomId)
        {
            InitializeComponent();
            
            _peerId = peerId;
            _peerUsername = peerUsername;
            _roomId = roomId;
            _apiService = ApiService.Instance;
            
            // Setup UI
            this.Title = $"Vidéo - {peerUsername}";
            UsernameText.Text = peerUsername;
            OverlayUsername.Text = peerUsername;
            
            // Position : décalée pour éviter superposition avec autres fenêtres
            PositionWindow();
            
            // S'abonner aux événements centralisés d'ApiService
            SubscribeToEvents();
            
            // Afficher le chargement et démarrer le timeout
            ShowLoading();
            StartTimeoutTimer();
            
            this.Closed += PeerVideoWindow_Closed;
        }

        #endregion

        #region Initialization

        private void PositionWindow()
        {
            // Position aléatoire légèrement décalée pour éviter superposition
            var random = new Random();
            var workArea = SystemParameters.WorkArea;
            
            this.Left = workArea.Right - this.Width - 30 - random.Next(0, 100);
            this.Top = workArea.Bottom - this.Height - 30 - random.Next(0, 150);
        }

        private void SubscribeToEvents()
        {
            // Écouter si le peer arrête sa caméra via l'événement centralisé
            _apiService.OnRoomCameraStopped += HandleCameraStopped;
        }

        private void UnsubscribeFromEvents()
        {
            _apiService.OnRoomCameraStopped -= HandleCameraStopped;
        }
        
        private void HandleCameraStopped(int roomId, int userId)
        {
            if (roomId != _roomId || userId != _peerId) return;
            
            Dispatcher.Invoke(() =>
            {
                ShowCameraStopped();
            });
        }

        private void StartTimeoutTimer()
        {
            // Timer pour vérifier si on reçoit des frames
            _timeoutTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _timeoutTimer.Tick += (s, e) =>
            {
                // Si pas de frame reçue depuis 5 secondes et pas encore de vidéo
                if (!_isReceivingVideo)
                {
                    ShowNoVideo();
                    _timeoutTimer?.Stop();
                }
            };
            _timeoutTimer.Start();
        }

        #endregion

        #region Video Processing

        /// <summary>
        /// Met à jour l'affichage avec une nouvelle frame vidéo
        /// Appelé depuis l'extérieur par RoomWindow via OnRemoteVideoFrameReceived
        /// </summary>
        public void UpdateVideoFrame(BitmapSource? frame)
        {
            if (frame == null) return;
            
            Dispatcher.Invoke(() =>
            {
                _isReceivingVideo = true;
                _lastFrameTime = DateTime.Now;
                
                // Arrêter le timer de timeout si on reçoit une frame
                _timeoutTimer?.Stop();
                
                VideoImage.Source = frame;
                PlaceholderPanel.Visibility = Visibility.Collapsed;
                LoadingIndicator.Visibility = Visibility.Collapsed;
            });
        }

        private void ShowLoading()
        {
            LoadingIndicator.Visibility = Visibility.Visible;
            PlaceholderPanel.Visibility = Visibility.Collapsed;
            StatusText.Text = "Connexion en cours...";
        }

        private void ShowNoVideo()
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
            PlaceholderPanel.Visibility = Visibility.Visible;
            StatusText.Text = "Vidéo non disponible";
        }

        private void ShowCameraStopped()
        {
            _isReceivingVideo = false;
            VideoImage.Source = null;
            PlaceholderPanel.Visibility = Visibility.Visible;
            StatusText.Text = $"{_peerUsername} a coupé sa caméra";
        }

        #endregion

        #region Window Controls

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    // Double-clic : maximiser/restaurer
                    if (WindowState == WindowState.Maximized)
                        WindowState = WindowState.Normal;
                    else
                        WindowState = WindowState.Maximized;
                }
                else
                {
                    DragMove();
                }
            }
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            this.Topmost = _isPinned;
            PinIcon.Text = _isPinned ? "📌" : "📍";
            PinIcon.Opacity = _isPinned ? 1.0 : 0.5;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion

        #region Cleanup

        private void PeerVideoWindow_Closed(object? sender, EventArgs e)
        {
            // Se désabonner proprement des événements
            UnsubscribeFromEvents();
            
            // Arrêter le timer
            _timeoutTimer?.Stop();
            _timeoutTimer = null;
            
            _isReceivingVideo = false;
        }

        #endregion

        #region Public Properties

        /// <summary>ID du peer dont on affiche la vidéo</summary>
        public int PeerId => _peerId;
        
        /// <summary>Nom d'utilisateur du peer</summary>
        public string PeerUsername => _peerUsername;
        
        /// <summary>Indique si la fenêtre reçoit actuellement de la vidéo</summary>
        public bool IsReceivingVideo => _isReceivingVideo;

        #endregion
    }
}

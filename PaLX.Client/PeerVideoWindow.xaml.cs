// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// PeerVideoWindow - Fenêtre flottante pour visionner la vidéo d'un autre participant

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.AspNetCore.SignalR.Client;
using PaLX.Client.Services;

namespace PaLX.Client
{
    /// <summary>
    /// Fenêtre simple pour afficher la vidéo d'un autre participant du chatroom
    /// </summary>
    public partial class PeerVideoWindow : Window
    {
        #region Fields

        private readonly int _peerId;
        private readonly string _peerUsername;
        private readonly int _roomId;
        private readonly HubConnection? _hubConnection;
        private bool _isPinned = true;
        private bool _isReceivingVideo = false;

        #endregion

        #region Constructor

        public PeerVideoWindow(int peerId, string peerUsername, int roomId)
        {
            InitializeComponent();
            
            _peerId = peerId;
            _peerUsername = peerUsername;
            _roomId = roomId;
            _hubConnection = ApiService.Instance.HubConnection;
            
            // Setup UI
            this.Title = $"Vidéo - {peerUsername}";
            UsernameText.Text = peerUsername;
            OverlayUsername.Text = peerUsername;
            
            // Position : décalée pour éviter superposition avec autres fenêtres
            PositionWindow();
            
            // S'abonner aux frames vidéo de ce peer
            SubscribeToPeerVideo();
            
            // Demander le flux vidéo
            RequestVideoStream();
            
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

        private void SubscribeToPeerVideo()
        {
            if (_hubConnection == null) return;
            
            // Écouter les frames vidéo envoyées par ce peer
            _hubConnection.On<int, int, byte[]>("RoomVideoFrame", (roomId, userId, frameData) =>
            {
                if (roomId != _roomId || userId != _peerId) return;
                
                Dispatcher.Invoke(() =>
                {
                    ProcessVideoFrame(frameData);
                });
            });
            
            // Écouter si le peer arrête sa caméra
            _hubConnection.On<int, int>("RoomCameraStopped", (roomId, userId) =>
            {
                if (roomId != _roomId || userId != _peerId) return;
                
                Dispatcher.Invoke(() =>
                {
                    ShowCameraStopped();
                });
            });
        }

        private async void RequestVideoStream()
        {
            try
            {
                ShowLoading();
                
                // Demander au serveur de nous envoyer le flux de ce peer
                if (_hubConnection != null)
                {
                    await _hubConnection.SendAsync("RequestPeerVideoStream", _roomId, _peerId);
                }
                
                // Timeout après 5 secondes si pas de vidéo
                await System.Threading.Tasks.Task.Delay(5000);
                
                if (!_isReceivingVideo)
                {
                    Dispatcher.Invoke(() =>
                    {
                        ShowNoVideo();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PeerVideo] Error requesting stream: {ex.Message}");
                ShowNoVideo();
            }
        }

        #endregion

        #region Video Processing

        private void ProcessVideoFrame(byte[] frameData)
        {
            try
            {
                // Pour l'instant, on utilise une approche simplifiée
                // Dans une implémentation complète, on décoderait les frames VP8
                
                _isReceivingVideo = true;
                HideLoading();
                
                // TODO: Décoder frameData (VP8) en BitmapSource
                // Pour l'instant, afficher un placeholder "connecté"
                PlaceholderPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PeerVideo] Frame processing error: {ex.Message}");
            }
        }

        /// <summary>
        /// Met à jour l'affichage avec une nouvelle frame vidéo
        /// Appelé depuis l'extérieur par RoomWindow
        /// </summary>
        public void UpdateVideoFrame(BitmapSource? frame)
        {
            Dispatcher.Invoke(() =>
            {
                if (frame != null)
                {
                    _isReceivingVideo = true;
                    VideoImage.Source = frame;
                    PlaceholderPanel.Visibility = Visibility.Collapsed;
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void ShowLoading()
        {
            LoadingIndicator.Visibility = Visibility.Visible;
            PlaceholderPanel.Visibility = Visibility.Collapsed;
        }

        private void HideLoading()
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
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
            // Se désabonner proprement
            // Note: Les handlers SignalR seront nettoyés automatiquement
            _isReceivingVideo = false;
        }

        #endregion

        #region Public Properties

        /// <summary>ID du peer dont on affiche la vidéo</summary>
        public int PeerId => _peerId;
        
        /// <summary>Nom d'utilisateur du peer</summary>
        public string PeerUsername => _peerUsername;

        #endregion
    }
}

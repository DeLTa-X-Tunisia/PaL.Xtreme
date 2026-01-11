// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// RoomVideoWindow - Fenêtre flottante WebRTC multi-caméra pour chatrooms

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using PaLX.Client.Services;

namespace PaLX.Client
{
    /// <summary>
    /// Fenêtre flottante pour afficher les vidéos des participants d'un chatroom
    /// - Grille adaptive (1→2→4→6→8 caméras)
    /// - WebRTC direct via VideoCallService optimisé
    /// - Toujours au-dessus, redimensionnable, déplaçable
    /// </summary>
    public partial class RoomVideoWindow : System.Windows.Window
    {
        #region Events
        
        /// <summary>Événement déclenché quand l'utilisateur toggle la caméra</summary>
        public event Action<bool>? OnCameraToggled;
        
        #endregion

        #region Fields

        private readonly string _roomName;
        private readonly ApiService _apiService;
        
        // Video cells - userId → UI elements
        private readonly Dictionary<int, VideoCellInfo> _videoCells = new();
        
        // Local video
        private bool _isCameraEnabled = false;
        private bool _isMicEnabled = true;
        private int _localUserId;
        
        // State
        private bool _isPinned = true;
        
        // Available cameras
        private List<CameraInfo> _availableCameras = new();

        #endregion
        
        #region Inner Classes
        
        private class CameraInfo
        {
            public int Index { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        #endregion

        #region Properties
        
        /// <summary>
        /// État actuel de la caméra locale
        /// </summary>
        public bool IsCameraEnabled => _isCameraEnabled;

        #endregion

        #region Constructor

        public RoomVideoWindow(string roomName)
        {
            InitializeComponent();
            
            _roomName = roomName;
            _apiService = ApiService.Instance;
            _localUserId = _apiService.CurrentUserId;
            
            this.Title = $"Vidéos - {roomName}";
            
            // Position par défaut : coin inférieur droit
            PositionWindow();
            
            this.Closed += RoomVideoWindow_Closed;
            this.Loaded += RoomVideoWindow_Loaded;
        }

        #endregion

        #region Initialization

        private void PositionWindow()
        {
            // Positionner en bas à droite de l'écran principal
            var workArea = SystemParameters.WorkArea;
            this.Left = workArea.Right - this.Width - 20;
            this.Top = workArea.Bottom - this.Height - 20;
        }

        private void RoomVideoWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Fenêtre prête
        }

        #endregion

        #region Video Grid Management

        /// <summary>
        /// Met à jour la vidéo locale avec une nouvelle frame
        /// </summary>
        public void UpdateLocalVideo(BitmapSource? frame, string username)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_videoCells.TryGetValue(_localUserId, out var cell))
                {
                    // Créer la cellule locale si elle n'existe pas
                    cell = CreateVideoCell(_localUserId, username, true);
                    _videoCells[_localUserId] = cell;
                    VideoGrid.Children.Add(cell.Container);
                    
                    UpdateGridLayout();
                    UpdateVisibility();
                }
                
                // Mettre à jour la frame
                if (frame != null)
                {
                    cell.VideoImage.Source = frame;
                    cell.Placeholder.Visibility = Visibility.Collapsed;
                }
                
                UpdateCameraCount();
            });
        }

        /// <summary>
        /// Retire la vidéo locale de la grille
        /// </summary>
        public void RemoveLocalVideo()
        {
            RemoveVideo(_localUserId);
        }

        /// <summary>
        /// Ajoute ou met à jour une vidéo dans la grille
        /// </summary>
        public void AddOrUpdateVideo(int userId, string username, BitmapSource? frame, bool isLocal = false)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_videoCells.TryGetValue(userId, out var cell))
                {
                    // Créer nouvelle cellule
                    cell = CreateVideoCell(userId, username, isLocal);
                    _videoCells[userId] = cell;
                    VideoGrid.Children.Add(cell.Container);
                    
                    UpdateGridLayout();
                    UpdateVisibility();
                }
                
                // Mettre à jour la frame
                if (frame != null)
                {
                    cell.VideoImage.Source = frame;
                    cell.Placeholder.Visibility = Visibility.Collapsed;
                }
                
                UpdateCameraCount();
            });
        }

        /// <summary>
        /// Retire une vidéo de la grille
        /// </summary>
        public void RemoveVideo(int userId)
        {
            Dispatcher.Invoke(() =>
            {
                if (_videoCells.TryGetValue(userId, out var cell))
                {
                    VideoGrid.Children.Remove(cell.Container);
                    _videoCells.Remove(userId);
                    
                    UpdateGridLayout();
                    UpdateVisibility();
                    UpdateCameraCount();
                }
            });
        }

        private VideoCellInfo CreateVideoCell(int userId, string username, bool isLocal)
        {
            // Container principal
            var container = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 26)),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(4),
                ClipToBounds = true
            };

            var grid = new Grid();

            // Image vidéo
            var videoImage = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(videoImage);

            // Placeholder
            var placeholder = new TextBlock
            {
                Text = "📷",
                FontSize = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.3
            };
            grid.Children.Add(placeholder);

            // Label utilisateur
            var labelBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(8)
            };

            var labelStack = new StackPanel { Orientation = Orientation.Horizontal };
            
            if (isLocal)
            {
                labelStack.Children.Add(new TextBlock
                {
                    Text = "📹 ",
                    FontSize = 11,
                    Foreground = Brushes.White
                });
            }

            labelStack.Children.Add(new TextBlock
            {
                Text = isLocal ? "Vous" : username,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });

            labelBorder.Child = labelStack;
            grid.Children.Add(labelBorder);

            // Badge "VOUS" pour vidéo locale
            if (isLocal)
            {
                var localBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(224, 62, 47)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(8)
                };
                localBadge.Child = new TextBlock
                {
                    Text = "VOUS",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                grid.Children.Add(localBadge);
            }

            container.Child = grid;

            // Double-clic pour agrandir (future feature)
            container.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    // TODO: Ouvrir en grand
                }
            };

            return new VideoCellInfo
            {
                UserId = userId,
                Username = username,
                IsLocal = isLocal,
                Container = container,
                VideoImage = videoImage,
                Placeholder = placeholder
            };
        }

        private void UpdateGridLayout()
        {
            int count = _videoCells.Count;

            // Layout adaptatif
            int rows, cols;
            if (count <= 1) { rows = 1; cols = 1; }
            else if (count == 2) { rows = 1; cols = 2; }
            else if (count <= 4) { rows = 2; cols = 2; }
            else if (count <= 6) { rows = 2; cols = 3; }
            else { rows = 2; cols = 4; }

            VideoGrid.Rows = rows;
            VideoGrid.Columns = cols;
        }

        private void UpdateVisibility()
        {
            bool hasVideos = _videoCells.Count > 0;
            EmptyState.Visibility = hasVideos ? Visibility.Collapsed : Visibility.Visible;
            VideoGrid.Visibility = hasVideos ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateCameraCount()
        {
            CameraCountText.Text = _videoCells.Count.ToString();
        }

        #endregion

        #region Camera & Mic Control

        /// <summary>
        /// Synchronise l'état visuel de la caméra avec l'état réel
        /// Appelé par RoomWindow quand la caméra est activée/désactivée depuis le bouton principal
        /// </summary>
        public void SetCameraState(bool isEnabled)
        {
            Dispatcher.Invoke(() =>
            {
                _isCameraEnabled = isEnabled;
                UpdateCameraButtonVisual();
            });
        }

        private void UpdateCameraButtonVisual()
        {
            if (_isCameraEnabled)
            {
                CameraIcon.Text = "📷";
                CameraButton.Style = (Style)FindResource("VideoControlButtonActive");
                // Cacher le bouton "Activer ma caméra" si visible
                if (StartCameraBtn != null)
                {
                    StartCameraBtn.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                CameraIcon.Text = "📷";
                CameraButton.Style = (Style)FindResource("VideoControlButton");
            }
        }

        private void ToggleCamera_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isCameraEnabled = !_isCameraEnabled;
                UpdateCameraButtonVisual();
                
                // Notifier le RoomWindow pour démarrer/arrêter la caméra via le service
                OnCameraToggled?.Invoke(_isCameraEnabled);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur caméra: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                _isCameraEnabled = false;
                UpdateCameraButtonVisual();
            }
        }

        private void ToggleMic_Click(object sender, RoutedEventArgs e)
        {
            _isMicEnabled = !_isMicEnabled;

            if (_isMicEnabled)
            {
                MicIcon.Text = "🎤";
                MicButton.Style = (Style)FindResource("VideoControlButton");
            }
            else
            {
                MicIcon.Text = "🔇";
                MicButton.Style = (Style)FindResource("VideoControlButtonActive");
            }

            // TODO: Muter/démuter le micro via VoiceService
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // Ouvrir les paramètres vidéo/audio
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
        
        /// <summary>
        /// Affiche le menu de sélection de caméra
        /// </summary>
        private void SwitchCamera_Click(object sender, RoutedEventArgs e)
        {
            // Détecter les caméras disponibles
            DetectAvailableCameras();
            
            // Construire le menu
            CameraMenu.Items.Clear();
            
            int currentIndex = SettingsService.SelectedCameraIndex;
            
            foreach (var camera in _availableCameras)
            {
                var menuItem = new MenuItem
                {
                    Header = camera.Name,
                    Tag = camera.Index,
                    IsChecked = camera.Index == currentIndex,
                    Icon = camera.Index == currentIndex ? new TextBlock { Text = "✓" } : null
                };
                menuItem.Click += CameraMenuItem_Click;
                CameraMenu.Items.Add(menuItem);
            }
            
            if (_availableCameras.Count == 0)
            {
                CameraMenu.Items.Add(new MenuItem 
                { 
                    Header = "Aucune caméra détectée", 
                    IsEnabled = false 
                });
            }
            
            // Afficher le menu
            CameraMenu.IsOpen = true;
        }
        
        /// <summary>
        /// Détecte les caméras disponibles sur le système
        /// </summary>
        private void DetectAvailableCameras()
        {
            _availableCameras.Clear();
            
            // Tester jusqu'à 10 indices de caméra
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    using var testCapture = new VideoCapture(i);
                    if (testCapture.IsOpened())
                    {
                        _availableCameras.Add(new CameraInfo
                        {
                            Index = i,
                            Name = $"📷 Caméra {i + 1}"
                        });
                        testCapture.Release();
                    }
                    else
                    {
                        break;
                    }
                }
                catch
                {
                    break;
                }
            }
        }
        
        /// <summary>
        /// Gère le clic sur une caméra du menu
        /// </summary>
        private void CameraMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is int cameraIndex)
            {
                // Sauvegarder le choix
                SettingsService.SelectedCameraIndex = cameraIndex;
                
                // Si la caméra est active, la redémarrer avec la nouvelle caméra
                if (_isCameraEnabled)
                {
                    // Déclencher un toggle off puis on pour redémarrer avec la nouvelle caméra
                    OnCameraToggled?.Invoke(false);
                    
                    // Petit délai pour laisser le temps de fermer
                    System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            OnCameraToggled?.Invoke(true);
                        });
                    });
                }
            }
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

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Note: La désactivation de la caméra sera gérée par le handler Closed dans RoomWindow
            // On met juste à jour l'état local pour éviter les appels multiples
            _isCameraEnabled = false;
            
            this.Close();
        }

        #endregion

        #region Cleanup

        private void RoomVideoWindow_Closed(object? sender, EventArgs e)
        {
            // Arrêter tous les flux vidéo
            if (_isCameraEnabled)
            {
                _isCameraEnabled = false;
                OnCameraToggled?.Invoke(false);
            }
            
            _videoCells.Clear();
        }

        #endregion

        #region Nested Types

        private class VideoCellInfo
        {
            public int UserId { get; set; }
            public string Username { get; set; } = string.Empty;
            public bool IsLocal { get; set; }
            public Border Container { get; set; } = null!;
            public Image VideoImage { get; set; } = null!;
            public TextBlock Placeholder { get; set; } = null!;
        }

        #endregion
    }
}

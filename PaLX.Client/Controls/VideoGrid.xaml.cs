// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// VideoGrid Control - Dynamic video grid for room video display

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PaLX.Client.Controls
{
    /// <summary>
    /// Modes de taille pour la zone vidéo
    /// </summary>
    public enum VideoSizeMode
    {
        Mini,       // Très petit - une ligne de vignettes
        Compact,    // Compact - hauteur réduite
        Normal,     // Normal - taille standard
        Large       // Grand - plus d'espace
    }
    
    /// <summary>
    /// Contrôle de grille vidéo dynamique pour les chatrooms
    /// S'adapte automatiquement au nombre de flux vidéo actifs
    /// </summary>
    public partial class VideoGrid : UserControl
    {
        #region Fields

        private readonly Dictionary<int, VideoCell> _videoCells = new();
        private bool _isCollapsed = false;
        private bool _isVisible = false;
        private bool _isFullscreen = false;
        private int _localUserId = 0;
        private VideoSizeMode _currentSizeMode = VideoSizeMode.Normal;
        
        // Dimensions par mode
        private static readonly Dictionary<VideoSizeMode, (double MinHeight, double CellHeight)> SizeModeSettings = new()
        {
            { VideoSizeMode.Mini, (60, 60) },
            { VideoSizeMode.Compact, (100, 90) },
            { VideoSizeMode.Normal, (150, 130) },
            { VideoSizeMode.Large, (220, 180) }
        };

        #endregion

        #region Events

        public event Action? OnCollapseToggled;
        public event Action<int>? OnVideoClicked;
        public event Action<bool>? OnFullscreenToggled;
        public event Action<VideoSizeMode>? OnSizeModeChanged;

        #endregion

        #region Properties

        public int VideoCount => _videoCells.Count;
        public bool IsCollapsed => _isCollapsed;
        public bool IsFullscreen => _isFullscreen;
        public VideoSizeMode CurrentSizeMode => _currentSizeMode;
        
        /// <summary>
        /// Nombre maximum de vidéos autorisées
        /// </summary>
        public int MaxVideos { get; set; } = 6;

        #endregion

        #region Constructor

        public VideoGrid()
        {
            InitializeComponent();
            ApplySizeMode(VideoSizeMode.Normal);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Configure l'ID de l'utilisateur local
        /// </summary>
        public void SetLocalUserId(int userId)
        {
            _localUserId = userId;
        }
        
        /// <summary>
        /// Change le mode de taille
        /// </summary>
        public void SetSizeMode(VideoSizeMode mode)
        {
            if (_currentSizeMode == mode) return;
            _currentSizeMode = mode;
            ApplySizeMode(mode);
            OnSizeModeChanged?.Invoke(mode);
        }
        
        /// <summary>
        /// Active/désactive le mode plein écran
        /// </summary>
        public void SetFullscreen(bool fullscreen)
        {
            _isFullscreen = fullscreen;
            OnFullscreenToggled?.Invoke(fullscreen);
            
            // Mise à jour de l'icône
            FullscreenIcon.Text = fullscreen ? "⛶" : "⛶";
            FullscreenButton.ToolTip = fullscreen ? "Quitter plein écran" : "Plein écran";
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
                    // Créer une nouvelle cellule
                    cell = CreateVideoCell(userId, username, isLocal);
                    _videoCells[userId] = cell;
                    VideoUniformGrid.Children.Add(cell.Container);
                    
                    UpdateGridLayout();
                    UpdateVisibility();
                }
                
                // Mettre à jour la frame
                if (frame != null)
                {
                    cell.VideoImage.Source = frame;
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
                    VideoUniformGrid.Children.Remove(cell.Container);
                    _videoCells.Remove(userId);
                    
                    UpdateGridLayout();
                    UpdateVisibility();
                    UpdateCameraCount();
                }
            });
        }

        /// <summary>
        /// Efface toutes les vidéos
        /// </summary>
        public void ClearAll()
        {
            Dispatcher.Invoke(() =>
            {
                VideoUniformGrid.Children.Clear();
                _videoCells.Clear();
                UpdateVisibility();
                UpdateCameraCount();
            });
        }

        /// <summary>
        /// Affiche ou masque la grille avec animation
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_isVisible == visible) return;
            _isVisible = visible;
            
            Dispatcher.Invoke(() =>
            {
                if (visible)
                {
                    this.Visibility = Visibility.Visible;
                    AnimateHeight(0, GetExpandedHeight(), 250);
                }
                else
                {
                    AnimateHeight(this.ActualHeight, 0, 200, () =>
                    {
                        this.Visibility = Visibility.Collapsed;
                    });
                }
            });
        }
        
        /// <summary>
        /// Met à jour la vidéo locale
        /// </summary>
        public void UpdateLocalVideo(BitmapSource? frame, string username)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_videoCells.TryGetValue(_localUserId, out var cell))
                {
                    // Créer la cellule pour la vidéo locale
                    cell = CreateVideoCell(_localUserId, username, true);
                    _videoCells[_localUserId] = cell;
                    VideoUniformGrid.Children.Add(cell.Container);
                    
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
        /// Retire la vidéo locale
        /// </summary>
        public void RemoveLocalVideo()
        {
            RemoveVideo(_localUserId);
        }
        
        /// <summary>
        /// Affiche la zone vidéo (expand)
        /// </summary>
        public void Expand()
        {
            Dispatcher.Invoke(() =>
            {
                // Rendre visible
                this.Visibility = Visibility.Visible;
                
                if (_isCollapsed)
                {
                    CollapseButton_Click(null, null);
                }
                
                _isVisible = true;
            });
        }
        
        /// <summary>
        /// Cache la zone vidéo (collapse)
        /// </summary>
        public void Collapse()
        {
            Dispatcher.Invoke(() =>
            {
                if (!_isCollapsed)
                {
                    CollapseButton_Click(null, null);
                }
            });
        }
        
        /// <summary>
        /// Cache complètement la zone vidéo
        /// </summary>
        public void Hide()
        {
            Dispatcher.Invoke(() =>
            {
                this.Visibility = Visibility.Collapsed;
                _isVisible = false;
            });
        }

        #endregion

        #region Private Methods

        private VideoCell CreateVideoCell(int userId, string username, bool isLocal)
        {
            var container = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 46)),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(4),
                MinHeight = 120,
                ClipToBounds = true,
                Cursor = System.Windows.Input.Cursors.Hand
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
            
            // Placeholder quand pas de vidéo
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
            
            // Indicateur "Local" pour sa propre vidéo
            if (isLocal)
            {
                var localIndicator = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(224, 62, 47)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(8)
                };
                localIndicator.Child = new TextBlock
                {
                    Text = "VOUS",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                };
                grid.Children.Add(localIndicator);
            }
            
            container.Child = grid;
            
            // Click handler
            container.MouseLeftButtonUp += (s, e) => OnVideoClicked?.Invoke(userId);
            
            return new VideoCell
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
            var settings = SizeModeSettings[_currentSizeMode];
            
            // Déterminer le layout optimal selon le mode et le nombre de vidéos
            int rows, cols;
            
            if (_currentSizeMode == VideoSizeMode.Mini)
            {
                // Mode Mini: toujours une seule ligne
                rows = 1;
                cols = Math.Max(1, count);
            }
            else if (count <= 1)
            {
                rows = 1; cols = 1;
            }
            else if (count == 2)
            {
                rows = 1; cols = 2;
            }
            else if (count <= 4)
            {
                rows = _currentSizeMode == VideoSizeMode.Compact ? 1 : 2;
                cols = _currentSizeMode == VideoSizeMode.Compact ? count : 2;
            }
            else if (count <= 6)
            {
                rows = 2; cols = 3;
            }
            else // 7-8
            {
                rows = 2; cols = 4;
            }
            
            VideoUniformGrid.Rows = rows;
            VideoUniformGrid.Columns = cols;
            
            // Ajuster la hauteur selon le mode
            double height = settings.CellHeight * rows;
            VideoContainer.MinHeight = height;
            VideoContainer.MaxHeight = _currentSizeMode == VideoSizeMode.Large ? double.PositiveInfinity : height + 20;
        }
        
        private void ApplySizeMode(VideoSizeMode mode)
        {
            var settings = SizeModeSettings[mode];
            
            // Mettre à jour le bouton radio correspondant
            switch (mode)
            {
                case VideoSizeMode.Mini:
                    SizeMini.IsChecked = true;
                    this.MaxHeight = 80;
                    break;
                case VideoSizeMode.Compact:
                    SizeCompact.IsChecked = true;
                    this.MaxHeight = 130;
                    break;
                case VideoSizeMode.Normal:
                    SizeNormal.IsChecked = true;
                    this.MaxHeight = 200;
                    break;
                case VideoSizeMode.Large:
                    SizeLarge.IsChecked = true;
                    this.MaxHeight = 320;
                    break;
            }
            
            // Recalculer le layout
            UpdateGridLayout();
            
            // Animation de transition
            var animation = new DoubleAnimation
            {
                To = settings.MinHeight,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            
            VideoContainer.BeginAnimation(MinHeightProperty, animation);
        }

        private void UpdateVisibility()
        {
            bool hasVideos = _videoCells.Count > 0;
            
            EmptyState.Visibility = hasVideos ? Visibility.Collapsed : Visibility.Visible;
            VideoUniformGrid.Visibility = hasVideos ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateCameraCount()
        {
            CameraCountText.Text = _videoCells.Count.ToString();
        }

        private double GetExpandedHeight()
        {
            var settings = SizeModeSettings[_currentSizeMode];
            int count = Math.Max(1, _videoCells.Count);
            int rows = _currentSizeMode == VideoSizeMode.Mini ? 1 : (count <= 2 ? 1 : 2);
            return rows * settings.CellHeight + 50; // +50 pour le header
        }

        private void AnimateHeight(double from, double to, int durationMs, Action? onComplete = null)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            
            if (onComplete != null)
            {
                animation.Completed += (s, e) => onComplete();
            }
            
            this.BeginAnimation(HeightProperty, animation);
        }

        #endregion

        #region Event Handlers

        private void CollapseButton_Click(object? sender, RoutedEventArgs? e)
        {
            _isCollapsed = !_isCollapsed;
            
            if (_isCollapsed)
            {
                VideoContainer.Visibility = Visibility.Collapsed;
                EmptyState.Visibility = Visibility.Collapsed;
                CollapseIcon.Text = "▼";
                CollapseButton.ToolTip = "Agrandir";
            }
            else
            {
                UpdateVisibility();
                CollapseIcon.Text = "▲";
                CollapseButton.ToolTip = "Réduire";
            }
            
            OnCollapseToggled?.Invoke();
        }
        
        private void SizeMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb)
            {
                VideoSizeMode newMode = VideoSizeMode.Normal;
                
                if (rb == SizeMini) newMode = VideoSizeMode.Mini;
                else if (rb == SizeCompact) newMode = VideoSizeMode.Compact;
                else if (rb == SizeNormal) newMode = VideoSizeMode.Normal;
                else if (rb == SizeLarge) newMode = VideoSizeMode.Large;
                
                SetSizeMode(newMode);
            }
        }
        
        private void FullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            SetFullscreen(!_isFullscreen);
        }

        #endregion

        #region Nested Types

        private class VideoCell
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

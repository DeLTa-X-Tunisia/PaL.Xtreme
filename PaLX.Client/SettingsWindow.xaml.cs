// Copyright (c) 2025 Azizi Mounir. All rights reserved.
// This software is proprietary and confidential.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NAudio.Wave;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using PaLX.Client.Services;

namespace PaLX.Client
{
    /// <summary>
    /// Représente une caméra disponible
    /// </summary>
    public class CameraDevice
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
    }

    /// <summary>
    /// Settings window for application preferences
    /// </summary>
    public partial class SettingsWindow : System.Windows.Window
    {
        private VideoCapture? _videoCapture;
        private CancellationTokenSource? _cameraCts;
        private bool _isCameraTesting = false;
        private int _selectedCameraIndex = 0;
        private ObservableCollection<CameraDevice> _availableCameras = new();
        
        private WaveInEvent? _waveIn;
        private bool _isMicTesting = false;
        private DispatcherTimer? _micLevelTimer;
        private float _currentMicLevel = 0;

        public SettingsWindow()
        {
            InitializeComponent();
            
            // Charger les paramètres sauvegardés
            LoadSavedSettings();
            
            // Subscribe to theme changes (for live preview)
            ThemeService.ThemeChanged += OnThemeChanged;
            
            // Détecter les caméras disponibles
            DetectAvailableCameras();
        }

        /// <summary>
        /// Charge les paramètres sauvegardés
        /// </summary>
        private void LoadSavedSettings()
        {
            // Mode sombre
            DarkModeToggle.IsChecked = SettingsService.DarkMode;
            
            // Notifications
            SoundNotificationsToggle.IsChecked = SettingsService.SoundNotifications;
            StartupSoundToggle.IsChecked = SettingsService.StartupSound;
            
            // L'index de la caméra sera appliqué après DetectAvailableCameras()
            _selectedCameraIndex = SettingsService.SelectedCameraIndex;
            
            // Qualité vidéo
            VideoQualitySelector.SelectedIndex = SettingsService.VideoQuality;
        }

        /// <summary>
        /// Détecte toutes les caméras disponibles sur le système
        /// </summary>
        private void DetectAvailableCameras()
        {
            _availableCameras.Clear();
            
            // Tester les indices de 0 à 9 pour trouver les caméras
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    using var testCapture = new VideoCapture(i);
                    if (testCapture.IsOpened())
                    {
                        _availableCameras.Add(new CameraDevice
                        {
                            Index = i,
                            Name = $"Caméra {i + 1}"
                        });
                        testCapture.Release();
                    }
                    else
                    {
                        break; // Pas plus de caméras
                    }
                }
                catch
                {
                    break;
                }
            }
            
            // Si aucune caméra trouvée, ajouter une entrée par défaut
            if (_availableCameras.Count == 0)
            {
                _availableCameras.Add(new CameraDevice
                {
                    Index = 0,
                    Name = "Aucune caméra détectée"
                });
            }
            
            CameraSelector.ItemsSource = _availableCameras;
            
            // Restaurer la caméra sauvegardée si elle existe encore
            int savedIndex = SettingsService.SelectedCameraIndex;
            if (savedIndex < _availableCameras.Count)
            {
                CameraSelector.SelectedIndex = savedIndex;
                _selectedCameraIndex = _availableCameras[savedIndex].Index;
            }
            else
            {
                CameraSelector.SelectedIndex = 0;
                _selectedCameraIndex = 0;
            }
        }

        /// <summary>
        /// Gère le changement de caméra sélectionnée
        /// </summary>
        private async void CameraSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CameraSelector.SelectedItem is CameraDevice selectedCamera)
            {
                _selectedCameraIndex = selectedCamera.Index;
                
                // Sauvegarder le choix automatiquement
                SettingsService.SelectedCameraIndex = CameraSelector.SelectedIndex;
                
                // Si la caméra est en cours de test, redémarrer avec la nouvelle
                if (_isCameraTesting)
                {
                    StopCameraTest();
                    await Task.Delay(200); // Petit délai pour libérer la ressource
                    await StartCameraTestAsync();
                }
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            StopAllTests();
            ThemeService.ThemeChanged -= OnThemeChanged;
            Close();
        }

        private void DarkModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            bool isDarkMode = DarkModeToggle.IsChecked == true;
            ThemeService.SetTheme(isDarkMode);
            SettingsService.DarkMode = isDarkMode;
        }

        private void SoundNotificationsToggle_Changed(object sender, RoutedEventArgs e)
        {
            SettingsService.SoundNotifications = SoundNotificationsToggle.IsChecked == true;
        }

        private void StartupSoundToggle_Changed(object sender, RoutedEventArgs e)
        {
            SettingsService.StartupSound = StartupSoundToggle.IsChecked == true;
        }

        /// <summary>
        /// Gère le changement de qualité vidéo
        /// </summary>
        private void VideoQualitySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VideoQualitySelector.SelectedIndex >= 0)
            {
                SettingsService.VideoQuality = VideoQualitySelector.SelectedIndex;
                
                // Afficher la config actuelle dans le statut de la caméra
                var config = SettingsService.CurrentVideoQuality;
                CameraStatusText.Text = $"{config.Width}×{config.Height} @ {config.Bitrate}kbps";
            }
        }

        private void OnThemeChanged()
        {
            // Update toggle if theme changed externally
            if (DarkModeToggle.IsChecked != ThemeService.IsDarkMode)
            {
                DarkModeToggle.IsChecked = ThemeService.IsDarkMode;
            }
        }

        #region Camera Test

        private async void TestCamera_Click(object sender, RoutedEventArgs e)
        {
            if (_isCameraTesting)
            {
                StopCameraTest();
            }
            else
            {
                await StartCameraTestAsync();
            }
        }

        private async Task StartCameraTestAsync()
        {
            try
            {
                _isCameraTesting = true;
                TestCameraButton.Content = "Arrêter";
                CameraStatusText.Text = "Initialisation...";
                CameraStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA726"));

                _cameraCts = new CancellationTokenSource();
                
                await Task.Run(() =>
                {
                    _videoCapture = new VideoCapture(_selectedCameraIndex);
                    if (!_videoCapture.IsOpened())
                    {
                        throw new Exception("Impossible d'accéder à la caméra");
                    }
                });

                Dispatcher.Invoke(() =>
                {
                    CameraPlaceholder.Visibility = Visibility.Collapsed;
                    CameraPreviewImage.Visibility = Visibility.Visible;
                    CameraStatusText.Text = "✓ Fonctionne";
                    CameraStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                });

                // Start camera loop
                _ = Task.Run(async () =>
                {
                    var frame = new Mat();
                    while (!_cameraCts.Token.IsCancellationRequested && _videoCapture != null && _videoCapture.IsOpened())
                    {
                        if (_videoCapture.Read(frame) && !frame.Empty())
                        {
                            var bitmap = frame.ToBitmapSource();
                            bitmap.Freeze();
                            
                            Dispatcher.Invoke(() =>
                            {
                                CameraPreviewImage.Source = bitmap;
                            });
                        }
                        await Task.Delay(33); // ~30 fps
                    }
                    frame.Dispose();
                }, _cameraCts.Token);
            }
            catch (Exception ex)
            {
                _isCameraTesting = false;
                TestCameraButton.Content = "Tester";
                CameraStatusText.Text = "✗ Erreur";
                CameraStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                ToastService.Error($"Erreur caméra: {ex.Message}");
                
                _videoCapture?.Dispose();
                _videoCapture = null;
            }
        }

        private void StopCameraTest()
        {
            _isCameraTesting = false;
            _cameraCts?.Cancel();
            
            _videoCapture?.Dispose();
            _videoCapture = null;
            
            TestCameraButton.Content = "Tester";
            CameraPlaceholder.Visibility = Visibility.Visible;
            CameraPreviewImage.Visibility = Visibility.Collapsed;
            CameraPreviewImage.Source = null;
        }

        #endregion

        #region Microphone Test

        private void TestMicrophone_Click(object sender, RoutedEventArgs e)
        {
            if (_isMicTesting)
            {
                StopMicrophoneTest();
            }
            else
            {
                StartMicrophoneTest();
            }
        }

        private void StartMicrophoneTest()
        {
            try
            {
                _isMicTesting = true;
                TestMicButton.Content = "Arrêter";
                MicStatusText.Text = "Écoute en cours...";
                MicStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA726"));

                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(44100, 1),
                    BufferMilliseconds = 50
                };

                _waveIn.DataAvailable += WaveIn_DataAvailable;
                _waveIn.RecordingStopped += WaveIn_RecordingStopped;
                
                _waveIn.StartRecording();

                // Timer for UI updates
                _micLevelTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(50)
                };
                _micLevelTimer.Tick += (s, e) => UpdateMicLevelUI();
                _micLevelTimer.Start();

                MicStatusText.Text = "✓ Fonctionne";
                MicStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
            }
            catch (Exception ex)
            {
                _isMicTesting = false;
                TestMicButton.Content = "Tester";
                MicStatusText.Text = "✗ Erreur";
                MicStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                MicLevelText.Text = "Niveau: --";
                ToastService.Error($"Erreur micro: {ex.Message}");
            }
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            float max = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = (short)(e.Buffer[i + 1] << 8 | e.Buffer[i]);
                float sample32 = sample / 32768f;
                if (sample32 < 0) sample32 = -sample32;
                if (sample32 > max) max = sample32;
            }
            _currentMicLevel = max;
        }

        private void WaveIn_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            _waveIn?.Dispose();
            _waveIn = null;
        }

        private void UpdateMicLevelUI()
        {
            // Map level to percentage (with some amplification for visibility)
            double levelPercent = Math.Min(_currentMicLevel * 3, 1.0) * 100;
            double maxWidth = MicLevelBar.Parent is Grid grid ? grid.ActualWidth : 300;
            
            MicLevelBar.Width = maxWidth * (levelPercent / 100);
            MicLevelText.Text = $"Niveau: {levelPercent:F0}%";
        }

        private void StopMicrophoneTest()
        {
            _isMicTesting = false;
            _micLevelTimer?.Stop();
            _micLevelTimer = null;
            
            _waveIn?.StopRecording();
            
            TestMicButton.Content = "Tester";
            MicLevelBar.Width = 0;
            MicLevelText.Text = "Niveau: --";
            _currentMicLevel = 0;
        }

        #endregion

        private void StopAllTests()
        {
            if (_isCameraTesting) StopCameraTest();
            if (_isMicTesting) StopMicrophoneTest();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopAllTests();
            ThemeService.ThemeChanged -= OnThemeChanged;
            base.OnClosed(e);
        }
    }
}

// Copyright (c) 2025 Azizi Mounir. All rights reserved.
// This software is proprietary and confidential.

using System;
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
    /// Settings window for application preferences
    /// </summary>
    public partial class SettingsWindow : System.Windows.Window
    {
        private VideoCapture? _videoCapture;
        private CancellationTokenSource? _cameraCts;
        private bool _isCameraTesting = false;
        
        private WaveInEvent? _waveIn;
        private bool _isMicTesting = false;
        private DispatcherTimer? _micLevelTimer;
        private float _currentMicLevel = 0;

        public SettingsWindow()
        {
            InitializeComponent();
            
            // Initialize toggle state from ThemeService
            DarkModeToggle.IsChecked = ThemeService.IsDarkMode;
            
            // Subscribe to theme changes (for live preview)
            ThemeService.ThemeChanged += OnThemeChanged;
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
                    _videoCapture = new VideoCapture(0);
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

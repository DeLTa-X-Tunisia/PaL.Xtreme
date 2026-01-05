// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// Video Call Window - Modern video calling UI

using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PaLX.Client.Services;

namespace PaLX.Client
{
    /// <summary>
    /// Video call window with modern UI
    /// </summary>
    public partial class VideoCallWindow : Window
    {
        private readonly VideoCallService _videoCallService;
        private readonly string _partnerUsername;
        private readonly string? _partnerAvatar;
        private readonly bool _isIncoming;
        private readonly string _callId;
        
        private DispatcherTimer? _timer;
        private int _callDuration = 0;
        private bool _isAudioMuted = false;
        private bool _isVideoDisabled = false;

        /// <summary>
        /// Create video call window for outgoing call
        /// </summary>
        public VideoCallWindow(VideoCallService videoCallService, string partnerUsername, string? partnerAvatar)
        {
            InitializeComponent();
            _videoCallService = videoCallService;
            _partnerUsername = partnerUsername;
            _partnerAvatar = partnerAvatar;
            _isIncoming = false;
            _callId = string.Empty;
            
            SetupWindow();
            SetupEvents();
            
            // Start outgoing call
            StartOutgoingCall();
        }

        /// <summary>
        /// Create video call window for incoming call
        /// </summary>
        public VideoCallWindow(VideoCallService videoCallService, string callerUsername, string callId, string? callerAvatar)
        {
            InitializeComponent();
            _videoCallService = videoCallService;
            _partnerUsername = callerUsername;
            _partnerAvatar = callerAvatar;
            _isIncoming = true;
            _callId = callId;
            
            SetupWindow();
            SetupEvents();
            
            // Show incoming call UI
            ShowIncomingCallUI();
        }

        private void SetupWindow()
        {
            // Set partner info
            PartnerNameText.Text = _partnerUsername;
            
            // Load partner avatar
            if (!string.IsNullOrEmpty(_partnerAvatar) && File.Exists(_partnerAvatar))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(_partnerAvatar);
                    bitmap.EndInit();
                    PartnerAvatarImage.Source = bitmap;
                }
                catch
                {
                    LoadDefaultAvatar();
                }
            }
            else
            {
                LoadDefaultAvatar();
            }
        }

        private void LoadDefaultAvatar()
        {
            try
            {
                var defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "default_avatar.png");
                if (File.Exists(defaultPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(defaultPath);
                    bitmap.EndInit();
                    PartnerAvatarImage.Source = bitmap;
                }
            }
            catch { }
        }

        private void SetupEvents()
        {
            _videoCallService.OnStatusChanged += status =>
            {
                Dispatcher.Invoke(() => CallStatusText.Text = status);
            };

            _videoCallService.OnLocalVideoFrame += frame =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (frame != null)
                    {
                        LocalVideoImage.Source = frame;
                        LocalVideoDisabled.Visibility = Visibility.Collapsed;
                    }
                });
            };

            _videoCallService.OnRemoteVideoFrame += frame =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (frame != null)
                    {
                        RemoteVideoImage.Source = frame;
                        RemoteVideoPlaceholder.Visibility = Visibility.Collapsed;
                    }
                });
            };

            _videoCallService.OnVideoCallAccepted += (callee, callId) =>
            {
                Dispatcher.Invoke(() =>
                {
                    CallStatusText.Text = "Connecté";
                    StartTimer();
                });
            };

            _videoCallService.OnVideoCallDeclined += (callee, callId) =>
            {
                Dispatcher.Invoke(() =>
                {
                    ToastService.Warning($"{_partnerUsername} a refusé l'appel vidéo");
                    Close();
                });
            };

            _videoCallService.OnVideoCallEnded += (partner, callId) =>
            {
                Dispatcher.Invoke(() =>
                {
                    ToastService.Info($"Appel terminé avec {_partnerUsername}");
                    Close();
                });
            };

            _videoCallService.OnPartnerVideoToggled += isEnabled =>
            {
                Dispatcher.Invoke(() =>
                {
                    PartnerVideoDisabledOverlay.Visibility = isEnabled ? Visibility.Collapsed : Visibility.Visible;
                    if (!isEnabled)
                    {
                        RemoteVideoPlaceholder.Visibility = Visibility.Visible;
                    }
                });
            };

            _videoCallService.OnPartnerAudioToggled += isEnabled =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Could show audio indicator here
                });
            };

            _videoCallService.OnError += error =>
            {
                Dispatcher.Invoke(() =>
                {
                    ToastService.Error(error);
                });
            };
        }

        private async void StartOutgoingCall()
        {
            CallStatusText.Text = $"Appel de {_partnerUsername}...";
            IncomingCallControls.Visibility = Visibility.Collapsed;
            ActiveCallControls.Visibility = Visibility.Visible;
            
            await _videoCallService.RequestVideoCall(_partnerUsername);
        }

        private void ShowIncomingCallUI()
        {
            CallStatusText.Text = "Appel vidéo entrant...";
            IncomingCallControls.Visibility = Visibility.Visible;
            ActiveCallControls.Visibility = Visibility.Collapsed;
            RecordingDot.Visibility = Visibility.Collapsed;
            
            // Play ringtone sound
            PlayRingtone();
        }

        private void PlayRingtone()
        {
            try
            {
                var ringtonePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ringtone.wav");
                if (File.Exists(ringtonePath))
                {
                    var player = new System.Media.SoundPlayer(ringtonePath);
                    player.PlayLooping();
                }
            }
            catch { }
        }

        private void StartTimer()
        {
            RecordingDot.Visibility = Visibility.Visible;
            _callDuration = 0;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _callDuration++;
            var minutes = _callDuration / 60;
            var seconds = _callDuration % 60;
            TimerText.Text = $"{minutes:D2}:{seconds:D2}";
        }

        private async void AcceptCallButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop ringtone
            System.Media.SoundPlayer player = new();
            player.Stop();
            
            IncomingCallControls.Visibility = Visibility.Collapsed;
            ActiveCallControls.Visibility = Visibility.Visible;
            CallStatusText.Text = "Connexion...";
            
            await _videoCallService.AcceptVideoCall(_partnerUsername, _callId);
            StartTimer();
        }

        private async void DeclineCallButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop ringtone
            System.Media.SoundPlayer player = new();
            player.Stop();
            
            await _videoCallService.DeclineVideoCall(_partnerUsername, _callId);
            Close();
        }

        private async void EndCallButton_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            await _videoCallService.EndVideoCall();
            Close();
        }

        private async void MuteAudioButton_Click(object sender, RoutedEventArgs e)
        {
            await _videoCallService.ToggleAudio();
            _isAudioMuted = !_isAudioMuted;
            
            if (_isAudioMuted)
            {
                MuteAudioButton.Style = (Style)FindResource("MutedToggleStyle");
                MuteAudioIcon.Text = "🔇";
                MuteAudioLabel.Text = "Muet";
            }
            else
            {
                MuteAudioButton.Style = (Style)FindResource("ActiveToggleStyle");
                MuteAudioIcon.Text = "🎤";
                MuteAudioLabel.Text = "Micro";
            }
        }

        private async void ToggleVideoButton_Click(object sender, RoutedEventArgs e)
        {
            await _videoCallService.ToggleVideo();
            _isVideoDisabled = !_isVideoDisabled;
            
            if (_isVideoDisabled)
            {
                ToggleVideoButton.Style = (Style)FindResource("MutedToggleStyle");
                ToggleVideoIcon.Text = "📷";
                ToggleVideoLabel.Text = "Off";
                LocalVideoDisabled.Visibility = Visibility.Visible;
            }
            else
            {
                ToggleVideoButton.Style = (Style)FindResource("ActiveToggleStyle");
                ToggleVideoIcon.Text = "📹";
                ToggleVideoLabel.Text = "Caméra";
                LocalVideoDisabled.Visibility = Visibility.Collapsed;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_videoCallService.IsCallActive)
            {
                // Show confirmation
                var result = MessageBox.Show(
                    "Voulez-vous terminer l'appel vidéo ?",
                    "Terminer l'appel",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    EndCallButton_Click(sender, e);
                }
            }
            else
            {
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void LocalVideo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Could implement drag to reposition PiP
            e.Handled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            base.OnClosed(e);
        }
    }
}

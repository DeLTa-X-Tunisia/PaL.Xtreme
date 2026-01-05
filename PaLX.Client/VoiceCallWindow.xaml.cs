using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PaLX.Client.Services;
using System.Windows.Shapes;

using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;

namespace PaLX.Client
{
    public class CallParticipant
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarPath { get; set; } = string.Empty;
    }

    public partial class VoiceCallWindow : Window
    {
        private VoiceCallService _voiceService;
        private DispatcherTimer _timer;
        private DateTime _startTime;
        private bool _isMuted = false;
        private string _remoteUser;
        private System.Windows.Media.MediaPlayer _ringtonePlayer = new System.Windows.Media.MediaPlayer();
        public ObservableCollection<CallParticipant> Participants { get; set; } = new ObservableCollection<CallParticipant>();

        public VoiceCallWindow(VoiceCallService voiceService, string remoteUser, bool isIncoming)
        {
            InitializeComponent();
            _voiceService = voiceService;
            _remoteUser = remoteUser;
            
            ParticipantsList.ItemsSource = Participants;
            
            StatusText.Text = isIncoming ? "Appel entrant..." : "Appel sortant...";
            
            _voiceService.OnCallEnded += VoiceService_OnCallEnded;
            _voiceService.OnStatusChanged += VoiceService_OnStatusChanged;
            _voiceService.OnCallAccepted += VoiceService_OnCallAccepted;
            _voiceService.OnCallCancelled += VoiceService_OnCallCancelled;

            LoadUserProfile();

            // Ringtone Logic
            try 
            {
                if (isIncoming)
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string path = System.IO.Path.Combine(baseDir, "Assets", "Sounds", "ringtone.mp3");
                    
                    if (System.IO.File.Exists(path))
                    {
                        _ringtonePlayer.Open(new Uri(path));
                        _ringtonePlayer.MediaEnded += (s, e) => 
                        {
                            _ringtonePlayer.Position = TimeSpan.Zero;
                            _ringtonePlayer.Play();
                        };
                        _ringtonePlayer.Play();
                    }
                }
            }
            catch { }

            if (isIncoming)
            {
                IncomingControls.Visibility = Visibility.Visible;
                ActiveControls.Visibility = Visibility.Collapsed;
            }
            else
            {
                IncomingControls.Visibility = Visibility.Collapsed;
                ActiveControls.Visibility = Visibility.Visible;
                MuteBtn.IsEnabled = false; // Disable until connected
            }

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            this.Closing += VoiceCallWindow_Closing;
        }

        private void VoiceCallWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try { _ringtonePlayer.Stop(); _ringtonePlayer.Close(); } catch { }
            _voiceService.EndCall();
        }

        private async void LoadUserProfile()
        {
            try
            {
                var profile = await ApiService.Instance.GetUserProfileAsync(_remoteUser);
                if (profile != null)
                {
                    // Format: LastName FirstName (to match Invite Window / SQL logic)
                    // User requested "Admin A" (which corresponds to Last First in DB if First="A", Last="Admin")
                    string first = profile.FirstName ?? "";
                    string last = profile.LastName ?? "";
                    string fullName = $"{last} {first}".Trim();
                    
                    if (string.IsNullOrEmpty(fullName))
                    {
                        fullName = _remoteUser;
                    }

                    // Capitalize
                    fullName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(fullName.ToLower());

                    Participants.Add(new CallParticipant 
                    { 
                        Username = _remoteUser,
                        DisplayName = fullName,
                        AvatarPath = profile.AvatarPath ?? "/Assets/default_avatar.png"
                    });
                }
                else
                {
                    // Fallback to username with capitalization
                    string displayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(_remoteUser.ToLower());
                    
                    Participants.Add(new CallParticipant 
                    { 
                        Username = _remoteUser,
                        DisplayName = displayName,
                        AvatarPath = "/Assets/default_avatar.png"
                    });
                }
            }
            catch { }
        }

        private void InviteBtn_Click(object sender, RoutedEventArgs e)
        {
            // Create list of excluded users (current participants)
            var excludedUsers = Participants.Select(p => p.Username).ToList();
            
            // Also exclude self if needed, but GetFriends usually doesn't return self.
            // However, if the user is somehow in the list, we should exclude them.
            // We don't have easy access to "Self" username here unless we store it.
            // But GetFriendsAsync returns friends, so self shouldn't be there.
            // The issue was likely that the remote user was in the list.
            
            var inviteWindow = new InviteFriendWindow(excludedUsers);
            inviteWindow.Owner = this;
            if (inviteWindow.ShowDialog() == true && !string.IsNullOrEmpty(inviteWindow.SelectedFriend))
            {
                _voiceService.RequestCall(inviteWindow.SelectedFriend);
                // Add to UI immediately as pending? Or wait for accept?
                // Let's wait for accept or just show as invited.
                // For now, we just send the invite.
                StatusText.Text = $"Invitation envoyée à {inviteWindow.SelectedFriend}...";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _voiceService.OnCallEnded -= VoiceService_OnCallEnded;
            _voiceService.OnStatusChanged -= VoiceService_OnStatusChanged;
            _voiceService.OnCallAccepted -= VoiceService_OnCallAccepted;
            _voiceService.OnCallCancelled -= VoiceService_OnCallCancelled;
            
            try 
            {
                _ringtonePlayer.Stop();
                _ringtonePlayer.Close();
            } catch {}
            
            _timer.Stop();
        }

        private void VoiceService_OnCallAccepted(string sender)
        {
            Dispatcher.Invoke(async () => 
            {
                _ringtonePlayer.Stop();
                _ringtonePlayer.Close();
                
                // Format status with display name if possible
                var p = Participants.FirstOrDefault(x => x.Username == sender);
                string displayName = p?.DisplayName ?? sender;
                StatusText.Text = $"Connecté avec {displayName}";
                
                IncomingControls.Visibility = Visibility.Collapsed;
                ActiveControls.Visibility = Visibility.Visible;
                MuteBtn.IsEnabled = true;
                StartTimer();

                // Add new participant if not exists
                var existing = Participants.FirstOrDefault(p => p.Username == sender);
                if (existing == null)
                {
                    try
                    {
                        var profile = await ApiService.Instance.GetUserProfileAsync(sender);
                        if (profile != null)
                        {
                            string first = profile.FirstName ?? "";
                            string last = profile.LastName ?? "";
                            string fullName = $"{last} {first}".Trim();
                            fullName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(fullName.ToLower());
                            
                            if (string.IsNullOrEmpty(fullName)) fullName = sender;

                            Participants.Add(new CallParticipant 
                            { 
                                Username = sender,
                                DisplayName = fullName,
                                AvatarPath = profile.AvatarPath ?? "/Assets/default_avatar.png"
                            });
                        }
                        else
                        {
                            string formattedName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(sender.ToLower());
                            Participants.Add(new CallParticipant 
                            { 
                                Username = sender,
                                DisplayName = formattedName,
                                AvatarPath = "/Assets/default_avatar.png"
                            });
                        }
                    }
                    catch
                    {
                        Participants.Add(new CallParticipant 
                        { 
                            Username = sender,
                            DisplayName = sender,
                            AvatarPath = "/Assets/default_avatar.png"
                        });
                    }
                }
            });
        }

        private void StartTimer()
        {
            _startTime = DateTime.Now;
            _timer.Start();
        }

        private void VoiceService_OnStatusChanged(string status)
        {
            Dispatcher.Invoke(() => 
            {
                string displayStatus = status;
                // Replace known usernames with display names
                foreach (var p in Participants)
                {
                    if (!string.IsNullOrEmpty(p.Username) && !string.IsNullOrEmpty(p.DisplayName) && p.Username != p.DisplayName)
                    {
                        displayStatus = displayStatus.Replace($"({p.Username})", $"({p.DisplayName})");
                        displayStatus = displayStatus.Replace($" {p.Username}", $" {p.DisplayName}");
                    }
                }
                
                StatusText.Text = displayStatus;
                if (status == "Connecté" && !_timer.IsEnabled)
                {
                    StartTimer();
                    MuteBtn.IsEnabled = true;
                }
            });
        }

        private void VoiceService_OnCallEnded(string reason)
        {
            Dispatcher.Invoke(() => 
            {
                _ringtonePlayer.Stop();
                _ringtonePlayer.Close();
                _timer.Stop();
                StatusText.Text = reason;
                var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                closeTimer.Tick += (s, e) => { closeTimer.Stop(); Close(); };
                closeTimer.Start();
            });
        }

        private void VoiceService_OnCallCancelled(string caller)
        {
            Dispatcher.Invoke(() => 
            {
                // Caller hung up before we answered
                _ringtonePlayer.Stop();
                _ringtonePlayer.Close();
                _timer.Stop();
                
                var p = Participants.FirstOrDefault(x => x.Username == caller);
                string displayName = p?.DisplayName ?? caller;
                StatusText.Text = $"{displayName} a annulé l'appel";
                
                var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                closeTimer.Tick += (s, e) => { closeTimer.Stop(); Close(); };
                closeTimer.Start();
            });
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            var duration = DateTime.Now - _startTime;
            TimerText.Text = duration.ToString(@"mm\:ss");
        }

        private async void AcceptBtn_Click(object sender, RoutedEventArgs e)
        {
            IncomingControls.Visibility = Visibility.Collapsed;
            ActiveControls.Visibility = Visibility.Visible;
            StatusText.Text = "Connexion...";
            await _voiceService.AcceptCall(_remoteUser);
        }

        private async void DeclineBtn_Click(object sender, RoutedEventArgs e)
        {
            await _voiceService.DeclineCall(_remoteUser);
            Close();
        }

        private void MuteBtn_Click(object sender, RoutedEventArgs e)
        {
            _isMuted = !_isMuted;
            _voiceService.ToggleMute(_isMuted);
            MuteBtn.Content = _isMuted ? "🔇" : "🎙️";
            MuteBtn.Background = _isMuted ? System.Windows.Media.Brushes.Red : (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#444");
        }

        private void EndCallBtn_Click(object sender, RoutedEventArgs e)
        {
            _voiceService.EndCall();
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
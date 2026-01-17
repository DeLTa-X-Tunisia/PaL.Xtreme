using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class RoomInvitationPopup : Window
    {
        private readonly int _roomId;
        private readonly string _inviterUsername;
        private readonly DispatcherTimer _autoCloseTimer;
        
        // Event to notify when user accepts the invitation
        public event Action<int, string>? InvitationAccepted;
        
        // Static position tracking for stacking
        private static int _activePopupCount = 0;
        private int _popupIndex;
        
        public RoomInvitationPopup(string inviterUsername, string inviterDisplayName, string? inviterAvatarPath, 
                                   int roomId, string roomName, string roomCategory)
        {
            InitializeComponent();
            
            _roomId = roomId;
            _inviterUsername = inviterUsername;
            _popupIndex = _activePopupCount++;
            
            Console.WriteLine($"[RoomInvitationPopup] Creating popup - Inviter={inviterDisplayName}, Room={roomName}, Avatar={inviterAvatarPath ?? "null"}");
            
            // Setup UI
            InviterNameText.Text = string.IsNullOrEmpty(inviterDisplayName) ? inviterUsername : inviterDisplayName;
            RoomNameText.Text = roomName;
            RoomCategoryText.Text = roomCategory;
            
            // Set avatar - build absolute URL if relative
            if (!string.IsNullOrEmpty(inviterAvatarPath))
            {
                try
                {
                    string absoluteAvatarUrl = inviterAvatarPath;
                    if (!inviterAvatarPath.StartsWith("http"))
                    {
                        var baseUrl = ApiService.Instance.GetBaseUrl().TrimEnd('/');
                        absoluteAvatarUrl = inviterAvatarPath.StartsWith("/") 
                            ? $"{baseUrl}{inviterAvatarPath}" 
                            : $"{baseUrl}/{inviterAvatarPath}";
                    }
                    
                    Console.WriteLine($"[RoomInvitationPopup] Loading avatar from: {absoluteAvatarUrl}");
                    InviterAvatar.Source = new BitmapImage(new Uri(absoluteAvatarUrl));
                    InviterAvatarFallback.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RoomInvitationPopup] Avatar load error: {ex.Message}");
                    InviterAvatarFallback.Visibility = Visibility.Visible;
                }
            }
            else
            {
                InviterAvatarFallback.Visibility = Visibility.Visible;
            }
            
            // Auto-close timer (30 seconds)
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _autoCloseTimer.Tick += (s, e) => CloseWithAnimation();
            
            Loaded += RoomInvitationPopup_Loaded;
            Closed += RoomInvitationPopup_Closed;
        }
        
        private void RoomInvitationPopup_Loaded(object sender, RoutedEventArgs e)
        {
            // Position in bottom-right corner with stacking
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 20;
            Top = workArea.Bottom - Height - 20 - (_popupIndex * 10);
            
            // Play slide-in animation
            var slideIn = (Storyboard)FindResource("SlideIn");
            slideIn.Begin(this);
            
            // Start auto-close timer
            _autoCloseTimer.Start();
        }
        
        private void RoomInvitationPopup_Closed(object sender, EventArgs e)
        {
            _activePopupCount = Math.Max(0, _activePopupCount - 1);
            _autoCloseTimer.Stop();
        }
        
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            CloseWithAnimation();
        }
        
        private void Refuse_Click(object sender, RoutedEventArgs e)
        {
            CloseWithAnimation();
        }
        
        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer.Stop();
            
            // Notify that invitation was accepted
            InvitationAccepted?.Invoke(_roomId, _inviterUsername);
            
            CloseWithAnimation();
        }
        
        private void CloseWithAnimation()
        {
            _autoCloseTimer.Stop();
            
            var slideOut = (Storyboard)FindResource("SlideOut");
            slideOut.Completed += (s, e) =>
            {
                try
                {
                    Close();
                }
                catch { }
            };
            slideOut.Begin(this);
        }
        
        public void Show(Window? owner = null)
        {
            if (owner != null)
            {
                Owner = null; // Don't set owner so it appears on top independently
            }
            
            base.Show();
        }
    }
}

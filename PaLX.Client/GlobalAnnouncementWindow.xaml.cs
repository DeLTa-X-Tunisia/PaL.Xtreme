using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class GlobalAnnouncementWindow : Window
    {
        private readonly DispatcherTimer _autoCloseTimer;
        
        public GlobalAnnouncementWindow(GlobalAnnouncementDto announcement)
        {
            InitializeComponent();
            
            // Configurer le style selon le type d'annonce
            ConfigureStyle(announcement.Type);
            
            // Remplir les données
            TitleText.Text = announcement.Title;
            MessageText.Text = announcement.Message;
            SenderText.Text = $"De: {announcement.SentBy}";
            TimestampText.Text = GetRelativeTime(announcement.Timestamp);
            
            // Animation d'entrée
            Loaded += (s, e) =>
            {
                var storyboard = (Storyboard)FindResource("SlideIn");
                storyboard.Begin(this);
            };
            
            // Timer pour fermeture automatique après 15 secondes
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(15)
            };
            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer.Stop();
                CloseWithAnimation();
            };
            _autoCloseTimer.Start();
        }

        private void ConfigureStyle(string type)
        {
            Color primaryColor;
            Color borderColor;
            string icon;

            switch (type.ToLower())
            {
                case "success":
                    primaryColor = (Color)ColorConverter.ConvertFromString("#22C55E")!;
                    borderColor = (Color)ColorConverter.ConvertFromString("#166534")!;
                    icon = "✅";
                    break;
                case "warning":
                    primaryColor = (Color)ColorConverter.ConvertFromString("#F59E0B")!;
                    borderColor = (Color)ColorConverter.ConvertFromString("#B45309")!;
                    icon = "⚠️";
                    break;
                case "alert":
                    primaryColor = (Color)ColorConverter.ConvertFromString("#EF4444")!;
                    borderColor = (Color)ColorConverter.ConvertFromString("#B91C1C")!;
                    icon = "🚨";
                    break;
                case "info":
                default:
                    primaryColor = (Color)ColorConverter.ConvertFromString("#3B82F6")!;
                    borderColor = (Color)ColorConverter.ConvertFromString("#1D4ED8")!;
                    icon = "ℹ️";
                    break;
            }

            // Appliquer les couleurs
            MainBorder.BorderBrush = new SolidColorBrush(borderColor);
            
            // Gradient pour le header
            var headerGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            headerGradient.GradientStops.Add(new GradientStop(Color.FromArgb(40, primaryColor.R, primaryColor.G, primaryColor.B), 0));
            headerGradient.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            HeaderBorder.Background = headerGradient;
            
            // Icône
            IconText.Text = icon;
            IconBorder.Background = new SolidColorBrush(Color.FromArgb(50, primaryColor.R, primaryColor.G, primaryColor.B));
            
            // Couleur du titre
            TitleText.Foreground = new SolidColorBrush(primaryColor);
        }

        private string GetRelativeTime(DateTime timestamp)
        {
            var diff = DateTime.UtcNow - timestamp;
            
            if (diff.TotalSeconds < 10)
                return "À l'instant";
            if (diff.TotalSeconds < 60)
                return $"Il y a {(int)diff.TotalSeconds} secondes";
            if (diff.TotalMinutes < 60)
                return $"Il y a {(int)diff.TotalMinutes} minute{((int)diff.TotalMinutes > 1 ? "s" : "")}";
            if (diff.TotalHours < 24)
                return $"Il y a {(int)diff.TotalHours} heure{((int)diff.TotalHours > 1 ? "s" : "")}";
            
            return timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer.Stop();
            CloseWithAnimation();
        }

        private void CloseWithAnimation()
        {
            var storyboard = (Storyboard)FindResource("SlideOut");
            storyboard.Begin(this);
        }

        private void SlideOut_Completed(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Affiche une annonce globale de manière statique
        /// </summary>
        public static void Show(GlobalAnnouncementDto announcement)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var window = new GlobalAnnouncementWindow(announcement);
                window.Show();
            });
        }
    }
}

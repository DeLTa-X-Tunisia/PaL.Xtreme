using System;
using System.Windows;

namespace PaLX.Client
{
    /// <summary>
    /// Fenêtre de notification élégante affichée lorsqu'un utilisateur est banni d'un salon
    /// Design moderne avec animation d'entrée et icône pulsante
    /// </summary>
    public partial class BanNotificationWindow : Window
    {
        public BanNotificationWindow(string roomName, string reason, string banType, DateTime? expiresAt)
        {
            InitializeComponent();
            
            RoomNameText.Text = roomName;
            ReasonText.Text = string.IsNullOrWhiteSpace(reason) ? "Aucune raison spécifiée" : reason;
            
            // Afficher la durée
            if (banType == "Permanent")
            {
                DurationText.Text = "Bannissement permanent";
                DurationBorder.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEE2E2"));
                DurationText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC2626"));
            }
            else if (expiresAt.HasValue)
            {
                var duration = expiresAt.Value - DateTime.UtcNow;
                string durationText;
                
                if (duration.TotalDays >= 1)
                    durationText = $"Durée : {(int)duration.TotalDays} jour{((int)duration.TotalDays > 1 ? "s" : "")}";
                else if (duration.TotalHours >= 1)
                    durationText = $"Durée : {(int)duration.TotalHours} heure{((int)duration.TotalHours > 1 ? "s" : "")}";
                else
                    durationText = $"Durée : {(int)duration.TotalMinutes} minute{((int)duration.TotalMinutes > 1 ? "s" : "")}";
                
                DurationText.Text = durationText;
            }
            else
            {
                DurationBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

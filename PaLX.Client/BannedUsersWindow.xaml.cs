using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PaLX.Client.Services;

namespace PaLX.Client
{
    /// <summary>
    /// Fenêtre affichant la liste des utilisateurs bannis d'un salon
    /// </summary>
    public partial class BannedUsersWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly int _roomId;
        private readonly ObservableCollection<BannedUserViewModel> _bans = new();

        public BannedUsersWindow(ApiService apiService, int roomId)
        {
            InitializeComponent();
            _apiService = apiService;
            _roomId = roomId;

            BansList.ItemsSource = _bans;

            // Charger les bans au démarrage
            Loaded += async (s, e) => await LoadBansAsync();
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
            Close();
        }

        private async System.Threading.Tasks.Task LoadBansAsync()
        {
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                EmptyPanel.Visibility = Visibility.Collapsed;
                BansScrollViewer.Visibility = Visibility.Collapsed;

                var bans = await _apiService.GetRoomBansAsync(_roomId);
                
                _bans.Clear();
                foreach (var ban in bans)
                {
                    _bans.Add(new BannedUserViewModel
                    {
                        Id = ban.Id,
                        UserId = ban.UserId,
                        Username = ban.Username,
                        DisplayName = ban.DisplayName,
                        AvatarUrl = BuildAvatarUrl(ban.AvatarUrl),
                        BannedByUsername = ban.BannedByUsername,
                        Reason = ban.Reason,
                        BanType = ban.BanType,
                        TimeRemaining = ban.TimeRemaining,
                        ExpiresAt = ban.ExpiresAt,
                        CreatedAt = ban.CreatedAt
                    });
                }

                LoadingPanel.Visibility = Visibility.Collapsed;
                
                if (_bans.Count == 0)
                {
                    EmptyPanel.Visibility = Visibility.Visible;
                    BansScrollViewer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    EmptyPanel.Visibility = Visibility.Collapsed;
                    BansScrollViewer.Visibility = Visibility.Visible;
                }

                CountBadge.Text = $"({_bans.Count})";
            }
            catch (Exception ex)
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                var alert = new CustomAlertWindow("Erreur", $"Impossible de charger les bans: {ex.Message}");
                alert.Owner = this;
                alert.ShowDialog();
            }
        }

        private void EditBan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is BannedUserViewModel ban)
            {
                var editWindow = new EditBanWindow(ban);
                
                // S'abonner à l'événement de confirmation
                editWindow.OnBanUpdated += async (updatedBan, newBanType, newDurationMinutes) =>
                {
                    try
                    {
                        var success = await _apiService.UpdateBanAsync(_roomId, updatedBan.UserId, newBanType, newDurationMinutes);
                        if (success)
                        {
                            // Refresh the list to show updated ban
                            await LoadBansAsync();
                            
                            var typeText = newBanType == "Permanent" ? "permanent" : "temporaire";
                            var alert = new CustomAlertWindow($"Le bannissement de {updatedBan.DisplayName} a été modifié en {typeText}.", "Bannissement modifié");
                            alert.Show();
                        }
                        else
                        {
                            var alert = new CustomAlertWindow("Impossible de modifier le bannissement.", "Erreur");
                            alert.Show();
                        }
                    }
                    catch (Exception ex)
                    {
                        var alert = new CustomAlertWindow($"Erreur: {ex.Message}", "Erreur");
                        alert.Show();
                    }
                };
                
                // Ouvrir en mode non-modal
                editWindow.Show();
            }
        }

        private async void Unban_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is BannedUserViewModel ban)
            {
                // Confirmation
                var confirm = new CustomConfirmWindow(
                    $"Voulez-vous vraiment débannir {ban.DisplayName} ?\n\nIl pourra à nouveau rejoindre le salon.",
                    "Confirmer le déban"
                );
                confirm.Owner = this;
                
                if (confirm.ShowDialog() == true)
                {
                    try
                    {
                        var success = await _apiService.UnbanUserAsync(_roomId, ban.UserId);
                        if (success)
                        {
                            // Retirer de la liste
                            _bans.Remove(ban);
                            CountBadge.Text = $"({_bans.Count})";

                            if (_bans.Count == 0)
                            {
                                EmptyPanel.Visibility = Visibility.Visible;
                                BansScrollViewer.Visibility = Visibility.Collapsed;
                            }

                            var alert = new CustomAlertWindow("Succès", $"{ban.DisplayName} a été débanni.");
                            alert.Owner = this;
                            alert.ShowDialog();
                        }
                        else
                        {
                            var alert = new CustomAlertWindow("Erreur", "Impossible de débannir l'utilisateur.");
                            alert.Owner = this;
                            alert.ShowDialog();
                        }
                    }
                    catch (Exception ex)
                    {
                        var alert = new CustomAlertWindow("Erreur", $"Erreur: {ex.Message}");
                        alert.Owner = this;
                        alert.ShowDialog();
                    }
                }
            }
        }

        /// <summary>
        /// Construit l'URL complète de l'avatar à partir du chemin relatif
        /// </summary>
        private string BuildAvatarUrl(string? avatarPath)
        {
            // URL par défaut si pas d'avatar
            if (string.IsNullOrEmpty(avatarPath))
                return $"{ApiService.BaseUrl}/avatars/default_avatar.png";

            // Si c'est déjà une URL complète, la retourner telle quelle
            if (avatarPath.StartsWith("http://") || avatarPath.StartsWith("https://"))
                return avatarPath;

            // Si c'est un chemin local, le retourner tel quel
            if ((avatarPath.Contains(":\\") || avatarPath.StartsWith("\\\\")) && System.IO.File.Exists(avatarPath))
                return avatarPath;

            // Construire l'URL complète avec le BaseUrl
            return $"{ApiService.BaseUrl}/{avatarPath.TrimStart('/', '\\')}";
        }
    }

    /// <summary>
    /// ViewModel pour afficher un utilisateur banni
    /// </summary>
    public class BannedUserViewModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string BannedByUsername { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string BanType { get; set; } = "Permanent";
        public string? TimeRemaining { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }

        // Computed properties for UI bindings
        public bool HasReason => !string.IsNullOrEmpty(Reason);
        public bool HasTimeRemaining => !string.IsNullOrEmpty(TimeRemaining) && BanType == "Temporary";
        public bool IsPermanent => BanType == "Permanent";
    }
}

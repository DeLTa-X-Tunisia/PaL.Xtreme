using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PaLX.Client.Services;

namespace PaLX.Client
{
    /// <summary>
    /// Fenêtre de notification pour les demandes de rôle dans un salon
    /// </summary>
    public partial class RoleRequestWindow : Window
    {
        private readonly RoleRequestReceivedDto _request;
        private readonly ApiService _apiService;

        public RoleRequestWindow(RoleRequestReceivedDto request)
        {
            InitializeComponent();
            _request = request;
            _apiService = ApiService.Instance;
            
            ConfigureUI();
        }

        private void ConfigureUI()
        {
            // Configurer l'affichage selon le rôle
            RoomNameLabel.Text = _request.RoomName;
            RoomNameRun.Text = _request.RoomName;

            switch (_request.Role)
            {
                case "SuperAdmin":
                    RoleIcon.Text = "👑";
                    RoleText.Text = "👑 SuperAdmin";
                    RoleBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9B59B6"));
                    break;
                case "Admin":
                    RoleIcon.Text = "⭐";
                    RoleText.Text = "⭐ Admin";
                    RoleBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12"));
                    break;
                case "Moderator":
                    RoleIcon.Text = "🔧";
                    RoleText.Text = "🔧 Modérateur";
                    RoleBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
                    break;
                default:
                    RoleIcon.Text = "🎭";
                    RoleText.Text = _request.Role;
                    RoleBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D"));
                    break;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            // Cette fenêtre n'est plus utilisée avec le nouveau système simplifié
            // Les rôles sont attribués directement sans demande d'acceptation
            string roleName = _request.Role switch
            {
                "SuperAdmin" => "SuperAdmin 👑",
                "Admin" => "Admin ⭐",
                "Moderator" => "Modérateur 🔧",
                _ => _request.Role
            };
            ToastService.Success($"Vous êtes maintenant {roleName} dans {_request.RoomName} !", "Rôle attribué");
            Close();
        }

        private void Decline_Click(object sender, RoutedEventArgs e)
        {
            // Cette fenêtre n'est plus utilisée avec le nouveau système simplifié
            ToastService.Info("Les rôles sont maintenant attribués directement.", "Information");
            Close();
        }
    }
}

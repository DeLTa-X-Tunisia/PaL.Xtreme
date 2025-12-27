using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using PaLX.Admin.Services;
using System.Threading.Tasks;

namespace PaLX.Admin
{
    public partial class PublicProfileWindow : Window
    {
        private string _username;

        public PublicProfileWindow(string username)
        {
            InitializeComponent();
            _username = username;
            LoadProfile();
        }

        private async void LoadProfile()
        {
            var profile = await ApiService.Instance.GetUserProfileAsync(_username);
            if (profile != null)
            {
                DisplayNameText.Text = $"{profile.LastName} {profile.FirstName}";
                UsernameText.Text = $"@{_username}";
                GenderText.Text = string.IsNullOrEmpty(profile.Gender) ? "Non spécifié" : profile.Gender;
                CountryText.Text = string.IsNullOrEmpty(profile.Country) ? "Non spécifié" : profile.Country;

                if (profile.DateOfBirth.HasValue)
                {
                    var today = DateTime.Today;
                    var age = today.Year - profile.DateOfBirth.Value.Year;
                    if (profile.DateOfBirth.Value.Date > today.AddYears(-age)) age--;
                    AgeText.Text = $"{age} ans";
                }
                else
                {
                    AgeText.Text = "Non spécifié";
                }

                if (!string.IsNullOrEmpty(profile.AvatarPath) && File.Exists(profile.AvatarPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(profile.AvatarPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    ProfileAvatar.Fill = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                    AvatarPlaceholder.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                DisplayNameText.Text = _username;
                UsernameText.Text = $"@{_username}";
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using PaLX.Client.Services;

namespace PaLX.Client
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
                string fullName = $"{profile.LastName} {profile.FirstName}".Trim();
                DisplayNameText.Text = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(fullName.ToLower());
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

                if (!string.IsNullOrEmpty(profile.AvatarPath))
                {
                    try
                    {
                        string avatarUrl = BuildAvatarUrl(profile.AvatarPath);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(avatarUrl, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        ProfileAvatar.Fill = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                        AvatarPlaceholder.Visibility = Visibility.Collapsed;
                    }
                    catch { /* Ignore avatar load errors */ }
                }
            }
            else
            {
                DisplayNameText.Text = _username;
                UsernameText.Text = $"@{_username}";
            }
        }

        private string BuildAvatarUrl(string? avatarPath)
        {
            if (string.IsNullOrEmpty(avatarPath))
                return ApiService.BaseUrl + "/Assets/default_avatar.png";
            
            if (avatarPath.StartsWith("http://") || avatarPath.StartsWith("https://"))
                return avatarPath;
            
            if ((avatarPath.Contains(":\\") || avatarPath.StartsWith("/") || avatarPath.StartsWith("\\")) && File.Exists(avatarPath))
                return avatarPath;
            
            return $"{ApiService.BaseUrl}/{avatarPath.TrimStart('/', '\\')}";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
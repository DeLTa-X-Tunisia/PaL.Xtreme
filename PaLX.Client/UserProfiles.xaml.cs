using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.IO;
using System;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class UserProfiles : Window
    {
        public event Action? ProfileSaved;

        private string _username;
        private string _roleName;
        private string? _avatarPath;
        private bool _isEditMode;
        
        private List<string> _countries = new List<string>
        {
            "Afghanistan", "Afrique du Sud", "Albanie", "Algérie", "Allemagne", "Andorre", "Angola", "Arabie saoudite", "Argentine", "Arménie", "Australie", "Autriche", "Azerbaïdjan",
            "Bahamas", "Bahreïn", "Bangladesh", "Barbade", "Belgique", "Belize", "Bénin", "Bhoutan", "Biélorussie", "Birmanie", "Bolivie", "Bosnie-Herzégovine", "Botswana", "Brésil", "Brunei", "Bulgarie", "Burkina Faso", "Burundi",
            "Cambodge", "Cameroun", "Canada", "Cap-Vert", "Centrafrique", "Chili", "Chine", "Chypre", "Colombie", "Comores", "Congo", "Corée du Nord", "Corée du Sud", "Costa Rica", "Côte d'Ivoire", "Croatie", "Cuba",
            "Danemark", "Djibouti", "Dominique",
            "Égypte", "Émirats arabes unis", "Équateur", "Érythrée", "Espagne", "Estonie", "États-Unis", "Éthiopie",
            "Fidji", "Finlande", "France",
            "Gabon", "Gambie", "Géorgie", "Ghana", "Grèce", "Grenade", "Guatemala", "Guinée", "Guyana",
            "Haïti", "Honduras", "Hongrie",
            "Inde", "Indonésie", "Irak", "Iran", "Irlande", "Islande", "Israël", "Italie",
            "Jamaïque", "Japon", "Jordanie",
            "Kazakhstan", "Kenya", "Kirghizistan", "Kiribati", "Koweït",
            "Laos", "Lesotho", "Lettonie", "Liban", "Libéria", "Libye", "Liechtenstein", "Lituanie", "Luxembourg",
            "Macédoine", "Madagascar", "Malaisie", "Malawi", "Maldives", "Mali", "Malte", "Maroc", "Marshall", "Maurice", "Mauritanie", "Mexique", "Micronésie", "Moldavie", "Monaco", "Mongolie", "Monténégro", "Mozambique",
            "Namibie", "Nauru", "Népal", "Nicaragua", "Niger", "Nigéria", "Norvège", "Nouvelle-Zélande",
            "Oman", "Ouganda", "Ouzbékistan",
            "Pakistan", "Palaos", "Palestine", "Panama", "Papouasie-Nouvelle-Guinée", "Paraguay", "Pays-Bas", "Pérou", "Philippines", "Pologne", "Portugal",
            "Qatar",
            "Roumanie", "Royaume-Uni", "Russie", "Rwanda",
            "Saint-Christophe-et-Niévès", "Sainte-Lucie", "Saint-Marin", "Saint-Vincent-et-les-Grenadines", "Salomon", "Salvador", "Samoa", "São Tomé-et-Príncipe", "Sénégal", "Serbie", "Seychelles", "Sierra Leone", "Singapour", "Slovaquie", "Slovénie", "Somalie", "Soudan", "Sri Lanka", "Suède", "Suisse", "Suriname", "Swaziland", "Syrie",
            "Tadjikistan", "Tanzanie", "Tchad", "République Tchèque", "Thaïlande", "Timor oriental", "Togo", "Tonga", "Trinité-et-Tobago", "Tunisie", "Turkménistan", "Turquie", "Tuvalu",
            "Ukraine", "Uruguay",
            "Vanuatu", "Vatican", "Venezuela", "Vietnam",
            "Yémen",
            "Zambie", "Zimbabwe"
        };

        public UserProfiles(string username, string roleName, bool isEditMode = false)
        {
            InitializeComponent();
            _username = username;
            _roleName = roleName;
            _isEditMode = isEditMode;
            CountryCombo.ItemsSource = _countries;
            LoadUserData();
        }

        private async void LoadUserData()
        {
            var profile = await ApiService.Instance.GetUserProfileAsync(_username);
            
            if (profile != null)
            {
                FirstNameBox.Text = profile.FirstName;
                LastNameBox.Text = profile.LastName;
                EmailBox.Text = profile.Email;
                PhoneBox.Text = profile.PhoneNumber;
                CountryCombo.Text = profile.Country;
                DobPicker.SelectedDate = profile.DateOfBirth;
                
                // Set Gender
                foreach (ComboBoxItem item in GenderCombo.Items)
                {
                    if (item.Content.ToString() == profile.Gender)
                    {
                        GenderCombo.SelectedItem = item;
                        break;
                    }
                }

                // Load Avatar
                if (!string.IsNullOrEmpty(profile.AvatarPath) && File.Exists(profile.AvatarPath))
                {
                    _avatarPath = profile.AvatarPath;
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_avatarPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    AvatarEllipse.Fill = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                    AvatarPlaceholder.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void UploadAvatar_Click(object sender, RoutedEventArgs e)
        {
            TriggerAvatarUpload();
        }

        private void Avatar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TriggerAvatarUpload();
        }

        private void TriggerAvatarUpload()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png)|*.jpg;*.jpeg;*.png";
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string sourcePath = openFileDialog.FileName;
                    string appDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatars");
                    if (!Directory.Exists(appDataPath))
                    {
                        Directory.CreateDirectory(appDataPath);
                    }

                    string fileName = $"{_username}_{DateTime.Now.Ticks}{Path.GetExtension(sourcePath)}";
                    string destPath = Path.Combine(appDataPath, fileName);

                    File.Copy(sourcePath, destPath, true);
                    _avatarPath = destPath;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(destPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    AvatarEllipse.Fill = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                    AvatarPlaceholder.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    ToastService.Error($"Erreur lors du chargement de l'image : {ex.Message}");
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditMode)
            {
                this.Close();
            }
            else
            {
                // Return to Login
                var loginWindow = new MainWindow();
                loginWindow.Show();
                this.Close();
            }
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateForm())
            {
                SaveProfile();
                
                if (_isEditMode)
                {
                    ProfileSaved?.Invoke();
                    this.Close();
                }
                else
                {
                    // Proceed to MainView
                    var mainView = new MainView(_username, _roleName);
                    Application.Current.MainWindow = mainView; // Définir comme fenêtre principale
                    mainView.Show();
                    this.Close();
                }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void ClosePopup_Click(object sender, RoutedEventArgs e)
        {
            ValidationPopup.Visibility = Visibility.Collapsed;
        }

        private bool ValidateForm()
        {
            bool isValid = true;
            string errorMessage = "Veuillez remplir les champs obligatoires :\n";

            if (string.IsNullOrWhiteSpace(LastNameBox.Text))
            {
                isValid = false;
                errorMessage += "- Nom\n";
            }
            if (string.IsNullOrWhiteSpace(FirstNameBox.Text))
            {
                isValid = false;
                errorMessage += "- Prénom\n";
            }
            
            // Email Validation
            if (string.IsNullOrWhiteSpace(EmailBox.Text))
            {
                isValid = false;
                errorMessage += "- Email\n";
            }
            else if (!IsValidEmail(EmailBox.Text))
            {
                isValid = false;
                errorMessage += "- Format Email invalide (ex: nom@domaine.com)\n";
            }

            // Country Validation
            if (string.IsNullOrWhiteSpace(CountryCombo.Text))
            {
                isValid = false;
                errorMessage += "- Pays\n";
            }

            // Date of Birth Validation
            if (DobPicker.SelectedDate == null)
            {
                isValid = false;
                errorMessage += "- Date de naissance\n";
            }

            if (!isValid)
            {
                ValidationMessage.Text = errorMessage;
                ValidationPopup.Visibility = Visibility.Visible;
            }

            return isValid;
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                return Regex.IsMatch(email,
                    @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                    RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        private async void SaveProfile()
        {
            string gender = (GenderCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Autre";
            string country = CountryCombo.Text;
            
            try
            {
                var profile = new UserProfileDto
                {
                    FirstName = FirstNameBox.Text,
                    LastName = LastNameBox.Text,
                    Email = EmailBox.Text,
                    Gender = gender,
                    Country = country,
                    PhoneNumber = PhoneBox.Text,
                    AvatarPath = _avatarPath,
                    DateOfBirth = DobPicker.SelectedDate
                };

                await ApiService.Instance.UpdateUserProfileAsync(profile);
                ToastService.Success("Profil sauvegardé avec succès !");
            }
            catch (System.Exception ex)
            {
                ToastService.Error($"Erreur lors de la sauvegarde : {ex.Message}");
            }
        }
    }
}
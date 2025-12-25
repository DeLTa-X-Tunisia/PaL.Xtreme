using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace PaLX.Client
{
    public partial class UserProfiles : Window
    {
        private string _username;
        private string _roleName;
        
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

        public UserProfiles(string username, string roleName)
        {
            InitializeComponent();
            _username = username;
            _roleName = roleName;
            CountryCombo.ItemsSource = _countries;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Return to Login
            var loginWindow = new MainWindow();
            loginWindow.Show();
            this.Close();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateForm())
            {
                SaveProfile();
                
                // Proceed to MainView
                var mainView = new MainView(_username, _roleName);
                mainView.Show();
                this.Close();
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

        private void SaveProfile()
        {
            var dbService = new DatabaseService();
            string gender = (GenderCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Autre";
            string country = CountryCombo.Text;
            
            try
            {
                dbService.SaveProfile(
                    _username,
                    FirstNameBox.Text,
                    LastNameBox.Text,
                    EmailBox.Text,
                    gender,
                    country,
                    PhoneBox.Text,
                    null // Avatar path placeholder
                );
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Erreur lors de la sauvegarde : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
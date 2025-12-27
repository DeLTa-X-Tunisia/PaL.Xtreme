using System.Windows;
using System.Windows.Controls;
using PaLX.Admin.Services;

namespace PaLX.Admin
{
    public partial class RegisterView : UserControl
    {
        public event RoutedEventHandler? SwitchToLogin;
        public string? AutoFillUsername { get; private set; }
        public string? AutoFillPassword { get; private set; }

        public RegisterView()
        {
            InitializeComponent();
            PasswordBox.PasswordChanged += (s, e) => UpdatePasswordPlaceholder();
            PasswordTxtBox.TextChanged += (s, e) => UpdatePasswordPlaceholder();
            ConfirmPasswordBox.PasswordChanged += (s, e) => 
            {
                ConfirmPasswordPlaceholder.Visibility = string.IsNullOrEmpty(ConfirmPasswordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
            };
        }

        private void UpdatePasswordPlaceholder()
        {
            bool isEmpty = ShowPasswordToggle.IsChecked == true 
                ? string.IsNullOrEmpty(PasswordTxtBox.Text) 
                : string.IsNullOrEmpty(PasswordBox.Password);
            
            PasswordPlaceholder.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowPasswordToggle_Click(object sender, RoutedEventArgs e)
        {
            if (ShowPasswordToggle.IsChecked == true)
            {
                PasswordTxtBox.Text = PasswordBox.Password;
                PasswordTxtBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                PasswordBox.Password = PasswordTxtBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTxtBox.Visibility = Visibility.Collapsed;
            }
            UpdatePasswordPlaceholder();
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string password = ShowPasswordToggle.IsChecked == true ? PasswordTxtBox.Text : PasswordBox.Password;

            if (string.IsNullOrEmpty(UsernameBox.Text) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(ConfirmPasswordBox.Password))
            {
                MessageBox.Show("Veuillez remplir tous les champs.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (password != ConfirmPasswordBox.Password)
            {
                MessageBox.Show("Les mots de passe ne correspondent pas.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RegisterButton.IsEnabled = false;
            bool success = await ApiService.Instance.RegisterAsync(UsernameBox.Text, password, ConfirmPasswordBox.Password);
            RegisterButton.IsEnabled = true;

            if (success)
            {
                MessageBox.Show("Inscription réussie ! Vous pouvez maintenant vous connecter.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                AutoFillUsername = UsernameBox.Text;
                AutoFillPassword = password;
                SwitchToLogin?.Invoke(this, e);
            }
            else
            {
                MessageBox.Show("Erreur lors de l'inscription. Le nom d'utilisateur est peut-être déjà pris.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SwitchToLogin_Click(object sender, RoutedEventArgs e)
        {
            AutoFillUsername = null;
            AutoFillPassword = null;
            SwitchToLogin?.Invoke(this, e);
        }
    }
}
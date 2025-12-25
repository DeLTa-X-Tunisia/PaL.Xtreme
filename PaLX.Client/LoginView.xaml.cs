using System.Windows;
using System.Windows.Controls;

namespace PaLX.Client
{
    public partial class LoginView : UserControl
    {
        public event RoutedEventHandler? SwitchToRegister;

        public LoginView()
        {
            InitializeComponent();
            PasswordBox.PasswordChanged += (s, e) => 
            {
                PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
            };
        }

        public void SetCredentials(string username, string password)
        {
            UsernameBox.Text = username;
            PasswordBox.Password = password;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(UsernameBox.Text) || string.IsNullOrEmpty(PasswordBox.Password))
            {
                MessageBox.Show("Veuillez remplir tous les champs.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dbService = new DatabaseService();
            var (isValid, roleLevel, roleName) = dbService.ValidateUser(UsernameBox.Text, PasswordBox.Password);
            
            if (isValid)
            {
                // Client App: Allows all roles (1-7)
                var mainView = new MainView(UsernameBox.Text, roleName ?? "User");
                mainView.Show();
                Window.GetWindow(this)?.Close();
            }
            else
            {
                MessageBox.Show("Nom d'utilisateur ou mot de passe incorrect.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SwitchToRegister_Click(object sender, RoutedEventArgs e)
        {
            SwitchToRegister?.Invoke(this, e);
        }
    }
}
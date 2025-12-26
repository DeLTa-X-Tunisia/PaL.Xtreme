using System.Windows;
using System.Windows.Controls;

namespace PaLX.Admin
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
                // Admin App: Allows roles 1-6
                if (roleLevel >= 1 && roleLevel <= 6)
                {
                    // Create Session
                    try
                    {
                        string hostName = System.Net.Dns.GetHostName();
                        string ip = System.Net.Dns.GetHostEntry(hostName).AddressList
                            .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString() ?? "127.0.0.1";
                        string deviceName = System.Environment.MachineName;
                        string deviceNumber = "ADM-" + new Random().Next(1000, 9999);

                        dbService.CreateSession(UsernameBox.Text, ip, deviceName, deviceNumber);
                    }
                    catch { /* Ignore session creation errors */ }

                    // Check if profile is complete
                    if (dbService.IsProfileComplete(UsernameBox.Text))
                    {
                        var mainView = new MainView(UsernameBox.Text, roleName ?? "Admin");
                        mainView.Show();
                    }
                    else
                    {
                        var userProfiles = new UserProfiles(UsernameBox.Text, roleName ?? "Admin");
                        userProfiles.Show();
                    }
                    Window.GetWindow(this)?.Close();
                }
                else
                {
                    MessageBox.Show("Accès refusé. Vous n'avez pas les droits d'administration.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
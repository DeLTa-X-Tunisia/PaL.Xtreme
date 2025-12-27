using System.Windows;
using System.Windows.Controls;
using PaLX.Admin.Services;

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

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(UsernameBox.Text) || string.IsNullOrEmpty(PasswordBox.Password))
            {
                MessageBox.Show("Veuillez remplir tous les champs.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoginButton.IsEnabled = false;

            try
            {
                string hostName = System.Net.Dns.GetHostName();
                string ip = System.Net.Dns.GetHostEntry(hostName).AddressList
                    .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString() ?? "127.0.0.1";
                string deviceName = System.Environment.MachineName;
                string deviceNumber = "ADM-" + new Random().Next(1000, 9999);

                var (result, isConnectionError) = await ApiService.Instance.LoginAsync(UsernameBox.Text, PasswordBox.Password, ip, deviceName, deviceNumber);
                
                if (result != null)
                {
                    // Admin App: Allows roles 1-6
                    if (result.RoleLevel >= 1 && result.RoleLevel <= 6)
                    {
                        // Connect SignalR
                        await ApiService.Instance.ConnectSignalRAsync();

                        if (result.IsProfileComplete)
                        {
                            var mainView = new MainView(UsernameBox.Text, result.Role);
                            mainView.Show();
                        }
                        else
                        {
                            var userProfiles = new UserProfiles(UsernameBox.Text, result.Role);
                            userProfiles.Show();
                        }
                        Window.GetWindow(this)?.Close();
                    }
                    else
                    {
                        MessageBox.Show("Accès refusé. Vous n'avez pas les droits d'administration.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else if (!isConnectionError)
                {
                    new LoginFailedWindow().ShowDialog();
                }
            }
            finally
            {
                LoginButton.IsEnabled = true;
            }
        }

        private void SwitchToRegister_Click(object sender, RoutedEventArgs e)
        {
            SwitchToRegister?.Invoke(this, e);
        }
    }
}
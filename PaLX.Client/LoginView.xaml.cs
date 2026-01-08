using System.Windows;
using System.Windows.Controls;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class LoginView : UserControl
    {
        public event RoutedEventHandler? SwitchToRegister;

        public LoginView()
        {
            InitializeComponent();
        }

        // Password placeholder visibility handler
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
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

            // Show loading state
            LoginButton.IsEnabled = false;
            LoginButton.Content = "Connexion...";
            LoadingPanel.Visibility = Visibility.Visible;

            try
            {
                string hostName = System.Net.Dns.GetHostName();
                string ip = System.Net.Dns.GetHostEntry(hostName).AddressList
                    .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString() ?? "127.0.0.1";
                string deviceName = System.Environment.MachineName;
                string deviceNumber = "PC-" + new Random().Next(1000, 9999);

                var (authResult, isConnectionError) = await ApiService.Instance.LoginAsync(UsernameBox.Text, PasswordBox.Password, ip, deviceName, deviceNumber);
                
                if (authResult != null)
                {
                    LoadingText.Text = "Initialisation...";
                    
                    // Connect SignalR
                    await ApiService.Instance.ConnectSignalRAsync();

                    if (authResult.IsProfileComplete)
                    {
                        var mainView = new MainView(UsernameBox.Text, authResult.Role);
                        Application.Current.MainWindow = mainView; // Définir comme fenêtre principale
                        mainView.Show();
                    }
                    else
                    {
                        var userProfiles = new UserProfiles(UsernameBox.Text, authResult.Role);
                        Application.Current.MainWindow = userProfiles; // Définir comme fenêtre principale
                        userProfiles.Show();
                    }
                    Window.GetWindow(this)?.Close();
                }
                else if (!isConnectionError)
                {
                    new LoginFailedWindow().ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur de connexion: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoginButton.IsEnabled = true;
                LoginButton.Content = "Se connecter";
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void SwitchToRegister_Click(object sender, RoutedEventArgs e)
        {
            SwitchToRegister?.Invoke(this, e);
        }
    }
}
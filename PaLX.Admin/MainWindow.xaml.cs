using System.Windows;

namespace PaLX.Admin;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoginView.SwitchToRegister += (s, e) => 
        {
            LoginView.Visibility = Visibility.Collapsed;
            RegisterView.Visibility = Visibility.Visible;
        };

        RegisterView.SwitchToLogin += (s, e) => 
        {
            if (!string.IsNullOrEmpty(RegisterView.AutoFillUsername))
            {
                LoginView.SetCredentials(RegisterView.AutoFillUsername, RegisterView.AutoFillPassword ?? string.Empty);
            }
            RegisterView.Visibility = Visibility.Collapsed;
            LoginView.Visibility = Visibility.Visible;
        };
    }
}
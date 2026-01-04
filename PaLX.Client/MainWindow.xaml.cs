using System.Windows;
using System.Windows.Input;

namespace PaLX.Client;

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

    // Window drag support
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    // Window control buttons
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
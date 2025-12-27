using System.Windows;

namespace PaLX.Admin
{
    public partial class LoginFailedWindow : Window
    {
        public LoginFailedWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
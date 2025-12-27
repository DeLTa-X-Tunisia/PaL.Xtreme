using System.Windows;

namespace PaLX.Admin
{
    public partial class CustomAlertWindow : Window
    {
        public CustomAlertWindow(string message, string title = "Action Non Autorisée")
        {
            InitializeComponent();
            MessageText.Text = message;
            TitleText.Text = title;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
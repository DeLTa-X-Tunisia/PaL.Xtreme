using System.Windows;

namespace PaLX.Client
{
    public partial class CustomAlertWindow : Window
    {
        public CustomAlertWindow(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
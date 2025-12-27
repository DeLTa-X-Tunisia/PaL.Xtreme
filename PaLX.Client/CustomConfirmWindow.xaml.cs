using System.Windows;

namespace PaLX.Client
{
    public partial class CustomConfirmWindow : Window
    {
        public CustomConfirmWindow(string message, string title = "Confirmation")
        {
            InitializeComponent();
            MessageText.Text = message;
            TitleText.Text = title;
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }
    }
}
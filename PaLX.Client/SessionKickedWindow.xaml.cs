using System.Windows;

namespace PaLX.Client
{
    public partial class SessionKickedWindow : Window
    {
        public SessionKickedWindow(string? reason = null)
        {
            InitializeComponent();
            
            if (!string.IsNullOrEmpty(reason))
            {
                MessageText.Text = reason;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}

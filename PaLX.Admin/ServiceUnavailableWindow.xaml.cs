using System.Windows;

namespace PaLX.Admin
{
    public partial class ServiceUnavailableWindow : Window
    {
        public ServiceUnavailableWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
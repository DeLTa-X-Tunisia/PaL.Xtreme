using System.Windows;

namespace PaLX.Client
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
using System.Windows;

namespace PaLX.Admin
{
    public partial class DisconnectionWindow : Window
    {
        public DisconnectionWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
using System.Windows;

namespace PaLX.Client
{
    public partial class MaintenanceWindow : Window
    {
        public MaintenanceWindow()
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
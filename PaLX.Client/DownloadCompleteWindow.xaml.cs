using System.Windows;

namespace PaLX.Client
{
    public partial class DownloadCompleteWindow : Window
    {
        public bool ShouldOpen { get; private set; } = false;

        public DownloadCompleteWindow()
        {
            InitializeComponent();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            ShouldOpen = true;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
        {
            ShouldOpen = false;
            this.DialogResult = false;
            this.Close();
        }
    }
}
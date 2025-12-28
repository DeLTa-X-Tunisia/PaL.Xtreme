using System.Windows;

namespace PaLX.Client
{
    public partial class ClearHistoryWindow : Window
    {
        public bool IsConfirmed { get; private set; } = false;

        public ClearHistoryWindow()
        {
            InitializeComponent();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }
    }
}
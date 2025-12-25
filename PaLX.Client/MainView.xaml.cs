using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PaLX.Client
{
    public partial class MainView : Window
    {
        public MainView(string username, string role)
        {
            InitializeComponent();
            UsernameText.Text = username;
            
            // Initialize Statuses
            var statuses = new List<StatusItem>
            {
                new StatusItem { Name = "En ligne", ColorBrush = Brushes.Green },
                new StatusItem { Name = "Occupé", ColorBrush = Brushes.Red },
                new StatusItem { Name = "Absent", ColorBrush = Brushes.Orange },
                new StatusItem { Name = "En appel", ColorBrush = Brushes.DarkRed },
                new StatusItem { Name = "Ne pas déranger", ColorBrush = Brushes.Purple },
                new StatusItem { Name = "Hors ligne", ColorBrush = Brushes.Gray }
            };
            StatusCombo.ItemsSource = statuses;
            StatusCombo.SelectedIndex = 0;

            // Initialize Dummy Friends
            var friends = new List<Friend>
            {
                new Friend { Name = "Alice", StatusText = "En ligne", StatusColor = Brushes.Green },
                new Friend { Name = "Bob", StatusText = "Occupé", StatusColor = Brushes.Red },
                new Friend { Name = "Charlie", StatusText = "Absent", StatusColor = Brushes.Orange },
                new Friend { Name = "David", StatusText = "Hors ligne", StatusColor = Brushes.Gray },
                new Friend { Name = "Eve", StatusText = "En ligne", StatusColor = Brushes.Green }
            };
            FriendsList.ItemsSource = friends;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
    }

    public class StatusItem
    {
        public string Name { get; set; } = "";
        public SolidColorBrush ColorBrush { get; set; } = Brushes.Gray;
    }

    public class Friend
    {
        public string Name { get; set; } = "";
        public string StatusText { get; set; } = "";
        public SolidColorBrush StatusColor { get; set; } = Brushes.Gray;
    }
}

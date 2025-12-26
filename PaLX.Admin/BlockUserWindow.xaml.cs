using System;
using System.Windows;
using System.Windows.Controls;

namespace PaLX.Admin
{
    public partial class BlockUserWindow : Window
    {
        private string _blockerUsername;
        private string _blockedUsername;
        private DatabaseService _dbService;

        public event Action? UserBlocked;

        public BlockUserWindow(string blockerUsername, string blockedUsername)
        {
            InitializeComponent();
            _blockerUsername = blockerUsername;
            _blockedUsername = blockedUsername;
            _dbService = new DatabaseService();
            TargetUserText.Text = blockedUsername;
        }

        private void BlockTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BlockTypeCombo.SelectedItem is ComboBoxItem item && item.Tag.ToString() == "2")
            {
                DateRangePanel.Visibility = Visibility.Visible;
            }
            else
            {
                DateRangePanel.Visibility = Visibility.Collapsed;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            int blockType = int.Parse(((ComboBoxItem)BlockTypeCombo.SelectedItem).Tag.ToString() ?? "0");
            DateTime? endDate = null;

            if (blockType == 1)
            {
                endDate = DateTime.Now.AddDays(7);
            }
            else if (blockType == 2)
            {
                if (EndDatePicker.SelectedDate == null)
                {
                    MessageBox.Show("Veuillez sélectionner une date de fin.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                endDate = EndDatePicker.SelectedDate;
            }

            _dbService.BlockUser(_blockerUsername, _blockedUsername, blockType, endDate, ReasonBox.Text);
            UserBlocked?.Invoke();
            DialogResult = true;
            this.Close();
        }
    }
}
using System;
using System.Windows;
using System.Windows.Controls;
using PaLX.Admin.Services;

namespace PaLX.Admin
{
    public partial class BlockUserWindow : Window
    {
        private string _blockerUsername;
        private string _blockedUsername;

        public event Action? UserBlocked;

        public BlockUserWindow(string blockerUsername, string blockedUsername)
        {
            InitializeComponent();
            _blockerUsername = blockerUsername;
            _blockedUsername = blockedUsername;
            TargetUserText.Text = blockedUsername;
        }

        public BlockUserWindow(string blockerUsername, BlockedUserDto info) : this(blockerUsername, info.Username)
        {
            // Pre-fill data
            BlockTypeCombo.SelectedIndex = info.BlockType;
            
            if (info.BlockType == 2 && info.EndDate.HasValue)
            {
                EndDatePicker.SelectedDate = info.EndDate;
            }

            ReasonBox.Text = info.Reason;
        }

        private void BlockTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateRangePanel == null) return;

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

        private async void Confirm_Click(object sender, RoutedEventArgs e)
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

            var result = await ApiService.Instance.BlockUserAsync(_blockedUsername, blockType, endDate, ReasonBox.Text);
            if (result.Success)
            {
                UserBlocked?.Invoke();
                DialogResult = true;
                this.Close();
            }
            else
            {
                new CustomAlertWindow(result.Message).ShowDialog();
            }
        }
    }
}
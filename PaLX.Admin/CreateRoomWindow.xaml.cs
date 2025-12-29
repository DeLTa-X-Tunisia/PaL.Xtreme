using System;
using System.Windows;
using System.Windows.Controls;
using PaLX.Admin.Services;
using PaLX.Admin.Controls;
using System.Linq;

namespace PaLX.Admin
{
    public partial class CreateRoomWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly RoomViewModel? _roomToEdit;

        public CreateRoomWindow(RoomViewModel? roomToEdit = null)
        {
            InitializeComponent();
            _apiService = ApiService.Instance;
            _roomToEdit = roomToEdit;
            LoadCategories();
            
            if (_roomToEdit != null)
            {
                Title = "Modifier le salon";
                CreateButton.Content = "Modifier";
                RoomNameBox.Text = _roomToEdit.Name;
                DescriptionBox.Text = _roomToEdit.Description;
                PrivateCheck.IsChecked = _roomToEdit.IsPrivate;
                AdultCheck.IsChecked = _roomToEdit.Is18Plus;
                LevelCombo.SelectedIndex = _roomToEdit.SubscriptionLevel;
            }
            else
            {
                LevelCombo.SelectedIndex = 0;
            }
        }

        private async void LoadCategories()
        {
            try
            {
                var categories = await _apiService.GetRoomCategoriesAsync();
                CategoryCombo.ItemsSource = categories;
                
                if (_roomToEdit != null)
                {
                    var cat = categories.FirstOrDefault(c => c.Id == _roomToEdit.CategoryId);
                    if (cat != null) CategoryCombo.SelectedItem = cat;
                }
                else if (categories.Count > 0) 
                {
                    CategoryCombo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur chargement catégories: {ex.Message}");
            }
        }

        private void PrivateCheck_Changed(object sender, RoutedEventArgs e)
        {
            PasswordBox.Visibility = PrivateCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LevelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Logic to update max users hint or validation could go here
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void Create_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RoomNameBox.Text))
            {
                MessageBox.Show("Le nom du salon est requis.");
                return;
            }

            if (CategoryCombo.SelectedItem == null)
            {
                MessageBox.Show("Veuillez sélectionner une catégorie.");
                return;
            }

            var levelItem = (ComboBoxItem)LevelCombo.SelectedItem;
            int level = int.Parse(levelItem.Tag.ToString());
            int maxUsers = level switch
            {
                0 => 20,
                1 => 50,
                2 => 100,
                3 => 200,
                4 => 500,
                _ => 20
            };

            var dto = new CreateRoomDto
            {
                Name = RoomNameBox.Text,
                Description = DescriptionBox.Text,
                CategoryId = ((RoomCategoryDto)CategoryCombo.SelectedItem).Id,
                SubscriptionLevel = level,
                MaxUsers = maxUsers,
                IsPrivate = PrivateCheck.IsChecked == true,
                Password = PrivateCheck.IsChecked == true ? PasswordBox.Password : null,
                Is18Plus = AdultCheck.IsChecked == true
            };

            try
            {
                if (_roomToEdit != null)
                {
                    await _apiService.UpdateRoomAsync(_roomToEdit.Id, dto);
                    MessageBox.Show("Salon modifié avec succès !");
                }
                else
                {
                    await _apiService.CreateRoomAsync(dto);
                    MessageBox.Show("Salon créé avec succès !");
                }
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur: {ex.Message}");
            }
        }
    }
}
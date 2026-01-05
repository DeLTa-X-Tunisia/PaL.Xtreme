using System;
using System.Windows;
using System.Windows.Controls;
using PaLX.Client.Services;
using PaLX.Client.Controls;

namespace PaLX.Client
{
    public partial class CreateRoomWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly int? _editingRoomId;

        public CreateRoomWindow()
        {
            InitializeComponent();
            _apiService = ApiService.Instance;
            LoadCategories();
            LevelCombo.SelectedIndex = 0;
        }

        public CreateRoomWindow(RoomViewModel room) : this()
        {
            _editingRoomId = room.Id;
            Title = "Modifier le Salon";
            RoomNameBox.Text = room.Name;
            DescriptionBox.Text = room.Description;
            PrivateCheck.IsChecked = room.IsPrivate;
            AdultCheck.IsChecked = room.Is18Plus;
            
            // Note: Category and Level selection requires waiting for LoadCategories or manual setting
            // For simplicity, we might not pre-select them perfectly here without async handling
            // But we can try to set them after loading.
        }

        private async void LoadCategories()
        {
            try
            {
                var categories = await _apiService.GetRoomCategoriesAsync();
                CategoryCombo.ItemsSource = categories;
                if (categories.Count > 0) CategoryCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ToastService.Error($"Erreur chargement catégories: {ex.Message}");
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
                ToastService.Warning("Le nom du salon est requis.");
                return;
            }

            if (CategoryCombo.SelectedItem == null)
            {
                ToastService.Warning("Veuillez sélectionner une catégorie.");
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
                if (_editingRoomId.HasValue)
                {
                    await _apiService.UpdateRoomAsync(_editingRoomId.Value, dto);
                    ToastService.Success("Salon modifié avec succès !");
                }
                else
                {
                    await _apiService.CreateRoomAsync(dto);
                    ToastService.Success("Salon créé avec succès !");
                }
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ToastService.Error($"Erreur: {ex.Message}");
            }
        }
    }
}

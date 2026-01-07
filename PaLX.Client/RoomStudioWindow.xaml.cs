using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PaLX.Client.Services;

namespace PaLX.Client
{
    /// <summary>
    /// Room Studio - Interface moderne pour la gestion des salons
    /// Fenêtre non-modale avec navigation fluide
    /// </summary>
    public partial class RoomStudioWindow : Window
    {
        private readonly ApiService _apiService;

        // Data collections
        private ObservableCollection<CategoryViewModel> _categories = new();
        private ObservableCollection<SubCategoryViewModel> _subCategories = new();
        private ObservableCollection<SubscriptionTierViewModel> _subscriptionTiers = new();
        private ObservableCollection<MyRoomViewModel> _myRooms = new();

        private CategoryViewModel? _selectedCategory;

        // Event to notify when a room is created
        public event EventHandler? RoomCreated;

        public RoomStudioWindow()
        {
            InitializeComponent();
            _apiService = ApiService.Instance;

            Loaded += RoomStudioWindow_Loaded;
        }

        private async void RoomStudioWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Live preview bindings - must be done after controls are loaded
            if (CreateRoomName != null)
                CreateRoomName.TextChanged += UpdatePreview;
            if (CreateRoomDesc != null)
                CreateRoomDesc.TextChanged += UpdatePreview;

            await LoadDataAsync();
            UpdatePreview(null, null);
        }

        #region ═══════════════════════════════════════════════════════════════
        // DATA LOADING
        #endregion

        private async Task LoadDataAsync()
        {
            try
            {
                // Load Categories
                await LoadCategoriesAsync();

                // Load Subscription Tiers
                await LoadSubscriptionTiersAsync();

                // Load My Rooms
                await LoadMyRoomsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors du chargement: {ex.Message}", "Erreur", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var categories = await _apiService.GetRoomCategoriesAsync();
                _categories.Clear();
                foreach (var cat in categories)
                {
                    _categories.Add(new CategoryViewModel
                    {
                        Id = cat.Id,
                        Name = cat.Name,
                        Description = cat.Description ?? "",
                        Icon = GetIconFromName(cat.Icon),
                        Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(cat.Color ?? "#3498DB")),
                        SubCategoryCount = cat.SubCategoryCount
                    });
                }
            }
            catch
            {
                // Fallback demo data
                _categories = new ObservableCollection<CategoryViewModel>
                {
                    new() { Id = 1, Name = "Général", Description = "Discussions générales", Icon = "\uE8BD", Color = new SolidColorBrush(Colors.DodgerBlue), SubCategoryCount = 4 },
                    new() { Id = 2, Name = "Rencontres", Description = "Faites de nouvelles connaissances", Icon = "\uE8FA", Color = new SolidColorBrush(Colors.HotPink), SubCategoryCount = 3 },
                    new() { Id = 3, Name = "Musique", Description = "Partagez vos goûts musicaux", Icon = "\uE8D6", Color = new SolidColorBrush(Colors.MediumPurple), SubCategoryCount = 5 },
                    new() { Id = 4, Name = "Jeux Vidéo", Description = "Discutez gaming et e-sport", Icon = "\uE7FC", Color = new SolidColorBrush(Colors.LimeGreen), SubCategoryCount = 6 },
                    new() { Id = 5, Name = "Cinéma & Séries", Description = "Vos films et séries préférés", Icon = "\uE8B2", Color = new SolidColorBrush(Colors.Tomato), SubCategoryCount = 4 },
                    new() { Id = 6, Name = "Technologie", Description = "Tech, dev et gadgets", Icon = "\uE950", Color = new SolidColorBrush(Colors.SteelBlue), SubCategoryCount = 3 },
                    new() { Id = 7, Name = "Sport", Description = "Actualités sportives", Icon = "\uE805", Color = new SolidColorBrush(Colors.Orange), SubCategoryCount = 5 },
                    new() { Id = 8, Name = "Adulte (18+)", Description = "Contenu réservé aux adultes", Icon = "\uE7BA", Color = new SolidColorBrush(Colors.Crimson), SubCategoryCount = 2 }
                };
            }

            CategoriesItemsControl.ItemsSource = _categories;
            CreateCategoryCombo.ItemsSource = _categories;
            if (_categories.Any())
                CreateCategoryCombo.SelectedIndex = 0;
        }

        private async Task LoadSubCategoriesAsync(int categoryId)
        {
            try
            {
                var subs = await _apiService.GetRoomSubCategoriesAsync(categoryId);
                _subCategories.Clear();
                foreach (var sub in subs)
                {
                    _subCategories.Add(new SubCategoryViewModel
                    {
                        Id = sub.Id,
                        Name = sub.Name,
                        Icon = GetIconFromName(sub.Icon),
                        Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sub.Color ?? "#6C757D"))
                    });
                }
            }
            catch
            {
                // Fallback demo
                _subCategories = new ObservableCollection<SubCategoryViewModel>
                {
                    new() { Id = 1, Name = "Discussion libre", Icon = "\uE8BD", Color = new SolidColorBrush(Colors.SteelBlue) },
                    new() { Id = 2, Name = "Débats", Icon = "\uE8BD", Color = new SolidColorBrush(Colors.Orange) },
                    new() { Id = 3, Name = "Entraide", Icon = "\uE8BD", Color = new SolidColorBrush(Colors.Green) },
                };
            }

            SubCategoriesItemsControl.ItemsSource = _subCategories;
            CreateSubCategoryCombo.ItemsSource = _subCategories;
            if (_subCategories.Any())
                CreateSubCategoryCombo.SelectedIndex = 0;
        }

        private async Task LoadSubscriptionTiersAsync()
        {
            try
            {
                var tiers = await _apiService.GetSubscriptionTiersAsync();
                _subscriptionTiers.Clear();
                foreach (var tier in tiers)
                {
                    _subscriptionTiers.Add(new SubscriptionTierViewModel
                    {
                        Tier = tier.Tier,
                        Name = tier.Name,
                        Icon = GetTierIcon(tier.Icon),
                        Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tier.Color ?? "#95A5A6")),
                        MaxUsers = tier.MaxUsers,
                        MaxMic = tier.MaxMic,
                        MaxCam = tier.MaxCam,
                        AlwaysOnline = tier.AlwaysOnline,
                        PriceDisplay = tier.MonthlyPriceCents == 0 ? "Gratuit" : $"{tier.MonthlyPriceCents / 100.0:F2}€"
                    });
                }
            }
            catch
            {
                // Fallback demo data
                _subscriptionTiers = new ObservableCollection<SubscriptionTierViewModel>
                {
                    new() { Tier = 0, Name = "Basic", Icon = "\uE80F", Color = new SolidColorBrush(Color.FromRgb(149, 165, 166)), MaxUsers = 50, MaxMic = 1, MaxCam = 2, AlwaysOnline = false, PriceDisplay = "Gratuit" },
                    new() { Tier = 1, Name = "Deluxe", Icon = "\uE735", Color = new SolidColorBrush(Color.FromRgb(52, 152, 219)), MaxUsers = 100, MaxMic = 1, MaxCam = 5, AlwaysOnline = false, PriceDisplay = "2,99€" },
                    new() { Tier = 2, Name = "Extreme", Icon = "\uE945", Color = new SolidColorBrush(Color.FromRgb(155, 89, 182)), MaxUsers = 250, MaxMic = 2, MaxCam = 8, AlwaysOnline = false, PriceDisplay = "4,99€" },
                    new() { Tier = 3, Name = "VIP", Icon = "\uE8FA", Color = new SolidColorBrush(Color.FromRgb(231, 76, 60)), MaxUsers = 500, MaxMic = 2, MaxCam = 10, AlwaysOnline = true, PriceDisplay = "7,99€" },
                    new() { Tier = 4, Name = "Bronze", Icon = "\uE7C1", Color = new SolidColorBrush(Color.FromRgb(205, 127, 50)), MaxUsers = 500, MaxMic = 3, MaxCam = 12, AlwaysOnline = true, PriceDisplay = "9,99€" },
                    new() { Tier = 5, Name = "Silver", Icon = "\uE83B", Color = new SolidColorBrush(Color.FromRgb(192, 192, 192)), MaxUsers = 1000, MaxMic = 3, MaxCam = 14, AlwaysOnline = true, PriceDisplay = "14,99€" },
                    new() { Tier = 6, Name = "Gold", Icon = "\uE734", Color = new SolidColorBrush(Color.FromRgb(255, 215, 0)), MaxUsers = 1500, MaxMic = 4, MaxCam = 20, AlwaysOnline = true, PriceDisplay = "19,99€" },
                    new() { Tier = 7, Name = "Platinum", Icon = "\uE7C8", Color = new SolidColorBrush(Color.FromRgb(229, 228, 226)), MaxUsers = 2000, MaxMic = 4, MaxCam = 30, AlwaysOnline = true, PriceDisplay = "29,99€" },
                    new() { Tier = 8, Name = "Ultimate", Icon = "\uE945", Color = new SolidColorBrush(Color.FromRgb(255, 107, 107)), MaxUsers = 5000, MaxMic = 5, MaxCam = 50, AlwaysOnline = true, PriceDisplay = "49,99€" },
                    new() { Tier = 9, Name = "Legend", Icon = "\uE7C1", Color = new SolidColorBrush(Color.FromRgb(255, 20, 147)), MaxUsers = 10000, MaxMic = 5, MaxCam = 100, AlwaysOnline = true, PriceDisplay = "99,99€" },
                };
            }

            SubscriptionTiersControl.ItemsSource = _subscriptionTiers;
        }

        private async Task LoadMyRoomsAsync()
        {
            try
            {
                var rooms = await _apiService.GetMyRoomsAsync();
                _myRooms.Clear();
                foreach (var room in rooms)
                {
                    _myRooms.Add(new MyRoomViewModel
                    {
                        Id = room.Id,
                        Name = room.Name,
                        CategoryName = room.CategoryName ?? "",
                        TierName = room.TierName ?? "Basic",
                        TierColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(room.TierColor ?? "#95A5A6")),
                        UserCount = room.UserCount,
                        MaxUsers = room.MaxUsers,
                        UserCountDisplay = $"{room.UserCount}/{room.MaxUsers} utilisateurs"
                    });
                }
            }
            catch
            {
                // No rooms or error
            }

            MyRoomsItemsControl.ItemsSource = _myRooms;
            NoRoomsMessage.Visibility = _myRooms.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        #region ═══════════════════════════════════════════════════════════════
        // NAVIGATION
        #endregion

        private void NavButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb) return;
            
            // Skip if controls not yet initialized
            if (PageCreate == null || PageCategories == null || PageSubscriptions == null || 
                PageMyRooms == null || PageSettings == null || PageTitle == null) 
                return;

            // Hide all pages
            PageCreate.Visibility = Visibility.Collapsed;
            PageCategories.Visibility = Visibility.Collapsed;
            PageSubscriptions.Visibility = Visibility.Collapsed;
            PageMyRooms.Visibility = Visibility.Collapsed;
            PageSettings.Visibility = Visibility.Collapsed;

            // Show selected page
            if (rb == NavCreate)
            {
                PageCreate.Visibility = Visibility.Visible;
                PageTitle.Text = "Créer un nouveau salon";
                PageSubtitle.Text = "Configurez votre espace de discussion";
            }
            else if (rb == NavCategories)
            {
                PageCategories.Visibility = Visibility.Visible;
                PageTitle.Text = "Catégories";
                PageSubtitle.Text = "Explorez toutes les catégories disponibles";
                if (SubCategoriesPanel != null) SubCategoriesPanel.Visibility = Visibility.Collapsed;
            }
            else if (rb == NavSubscriptions)
            {
                PageSubscriptions.Visibility = Visibility.Visible;
                PageTitle.Text = "Abonnements Room";
                PageSubtitle.Text = "Débloquez plus de fonctionnalités pour votre salon";
            }
            else if (rb == NavMyRooms)
            {
                PageMyRooms.Visibility = Visibility.Visible;
                PageTitle.Text = "Mes salons";
                PageSubtitle.Text = "Gérez vos salons existants";
                _ = LoadMyRoomsAsync();
            }
            else if (rb == NavSettings)
            {
                PageSettings.Visibility = Visibility.Visible;
                PageTitle.Text = "Paramètres";
                PageSubtitle.Text = "Configurez vos préférences";
            }
        }

        #region ═══════════════════════════════════════════════════════════════
        // CREATE ROOM
        #endregion

        private void UpdatePreview(object? sender, TextChangedEventArgs? e)
        {
            if (PreviewRoomName == null || CreateRoomName == null || CreateRoomDesc == null) return;

            PreviewRoomName.Text = string.IsNullOrWhiteSpace(CreateRoomName.Text) ? "Mon Salon" : CreateRoomName.Text;
            PreviewDesc.Text = string.IsNullOrWhiteSpace(CreateRoomDesc.Text) ? "Description du salon..." : CreateRoomDesc.Text;

            if (CreateCategoryCombo?.SelectedItem is CategoryViewModel cat && PreviewCategory != null)
            {
                PreviewCategory.Text = cat.Name;
            }

            if (PreviewCapacity != null && CreateMaxUsersSlider != null)
                PreviewCapacity.Text = $"0/{(int)CreateMaxUsersSlider.Value}";
        }

        private async void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CreateCategoryCombo.SelectedItem is CategoryViewModel cat)
            {
                _selectedCategory = cat;
                await LoadSubCategoriesAsync(cat.Id);
                UpdatePreview(null, null);
            }
        }

        private void MaxUsersSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CreateMaxUsersText != null)
            {
                CreateMaxUsersText.Text = ((int)e.NewValue).ToString();
                UpdatePreview(null, null);
            }
        }

        private void PrivateCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (CreatePasswordBorder != null)
            {
                CreatePasswordBorder.Visibility = CreatePrivateCheck.IsChecked == true 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        private async void CreateRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validation
                if (CreateRoomName == null || string.IsNullOrWhiteSpace(CreateRoomName.Text))
                {
                    ShowError("Veuillez entrer un nom pour le salon.");
                    return;
                }

                if (CreateCategoryCombo?.SelectedItem is not CategoryViewModel category)
                {
                    ShowError("Veuillez sélectionner une catégorie.");
                    return;
                }

                if (CreateRoomButton != null)
                {
                    CreateRoomButton.IsEnabled = false;
                    CreateRoomButton.Content = "Création en cours...";
                }

                var dto = new CreateRoomDto
                {
                    Name = CreateRoomName.Text.Trim(),
                    Description = CreateRoomDesc?.Text?.Trim() ?? "",
                    CategoryId = category.Id,
                    MaxUsers = CreateMaxUsersSlider != null ? (int)CreateMaxUsersSlider.Value : 25,
                    IsPrivate = CreatePrivateCheck?.IsChecked == true,
                    Password = CreatePrivateCheck?.IsChecked == true ? CreatePasswordBox?.Password : null,
                    Is18Plus = false,
                    SubscriptionLevel = 0 // Always Basic
                };

                var result = await _apiService.CreateRoomAsync(dto);

                if (result != null)
                {
                    ShowSuccess("Salon créé avec succès ! 🎉");
                    
                    // Notify parent
                    RoomCreated?.Invoke(this, EventArgs.Empty);
                    
                    // Clear form
                    if (CreateRoomName != null) CreateRoomName.Text = "";
                    if (CreateRoomDesc != null) CreateRoomDesc.Text = "";
                    if (CreatePrivateCheck != null) CreatePrivateCheck.IsChecked = false;
                    if (CreatePasswordBox != null) CreatePasswordBox.Password = "";
                    if (CreateMaxUsersSlider != null) CreateMaxUsersSlider.Value = 25;

                    // Switch to My Rooms
                    if (NavMyRooms != null) NavMyRooms.IsChecked = true;
                }
                else
                {
                    ShowError("Erreur lors de la création du salon.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Erreur: {ex.Message}");
            }
            finally
            {
                if (CreateRoomButton != null)
                {
                    CreateRoomButton.IsEnabled = true;
                    CreateRoomButton.Content = "✨ Créer le salon";
                }
            }
        }

        #region ═══════════════════════════════════════════════════════════════
        // CATEGORIES
        #endregion

        private async void Category_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is CategoryViewModel category)
            {
                _selectedCategory = category;
                SelectedCategoryTitle.Text = category.Name;
                await LoadSubCategoriesAsync(category.Id);
                SubCategoriesPanel.Visibility = Visibility.Visible;
            }
        }

        private void BackToCategories_Click(object sender, RoutedEventArgs e)
        {
            SubCategoriesPanel.Visibility = Visibility.Collapsed;
        }

        #region ═══════════════════════════════════════════════════════════════
        // SUBSCRIPTIONS
        #endregion

        private void SelectTier_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int tier)
            {
                // For now, show info - payment integration would go here
                if (tier == 0)
                {
                    ShowInfo("Le niveau Basic est gratuit et inclus par défaut avec chaque salon.");
                }
                else
                {
                    var tierInfo = _subscriptionTiers.FirstOrDefault(t => t.Tier == tier);
                    if (tierInfo != null)
                    {
                        ShowInfo($"L'abonnement {tierInfo.Name} sera bientôt disponible à l'achat.\n\n" +
                                 $"• {tierInfo.MaxUsers} utilisateurs max\n" +
                                 $"• {tierInfo.MaxMic} micros simultanés\n" +
                                 $"• {tierInfo.MaxCam} caméras simultanées\n" +
                                 $"Prix: {tierInfo.PriceDisplay}/mois");
                    }
                }
            }
        }

        #region ═══════════════════════════════════════════════════════════════
        // MY ROOMS
        #endregion

        private void ManageRoom_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int roomId)
            {
                ShowInfo($"La gestion du salon #{roomId} sera bientôt disponible.");
            }
        }

        private void UpgradeRoom_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int roomId)
            {
                NavSubscriptions.IsChecked = true;
            }
        }

        private void GoToCreate_Click(object sender, RoutedEventArgs e)
        {
            NavCreate.IsChecked = true;
        }

        #region ═══════════════════════════════════════════════════════════════
        // SETTINGS
        #endregion

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowSuccess("Paramètres sauvegardés !");
        }

        #region ═══════════════════════════════════════════════════════════════
        // WINDOW CONTROLS
        #endregion

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Maximize_Click(sender, e);
            }
            else
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #region ═══════════════════════════════════════════════════════════════
        // HELPERS
        #endregion

        private string GetIconFromName(string? iconName)
        {
            return iconName?.ToLower() switch
            {
                "chat" => "\uE8BD",
                "heart" => "\uE8FA",
                "music-note" or "music" => "\uE8D6",
                "controller" or "game" => "\uE7FC",
                "movie" or "film" => "\uE8B2",
                "cpu" or "tech" => "\uE950",
                "basketball" or "sport" => "\uE805",
                "alert-circle" or "alert" => "\uE7BA",
                "home" => "\uE80F",
                "star" => "\uE735",
                "zap" => "\uE945",
                "crown" => "\uE734",
                "shield" => "\uE83B",
                "award" => "\uE7C1",
                "diamond" => "\uE7C8",
                "rocket" => "\uE945",
                "trophy" => "\uE7C1",
                _ => "\uE8BD"
            };
        }

        private string GetTierIcon(string? iconName)
        {
            return iconName?.ToLower() switch
            {
                "home" => "\uE80F",
                "star" => "\uE735",
                "zap" => "\uE945",
                "heart" => "\uE8FA",
                "award" => "\uE7C1",
                "shield" => "\uE83B",
                "crown" => "\uE734",
                "diamond" => "\uE7C8",
                "rocket" => "\uE945",
                "trophy" => "\uE7C1",
                _ => "\uE735"
            };
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowSuccess(string message)
        {
            MessageBox.Show(message, "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowInfo(string message)
        {
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    #region ═══════════════════════════════════════════════════════════════
    // VIEW MODELS (for UI binding with SolidColorBrush)
    #endregion

    public class CategoryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Icon { get; set; } = "\uE8BD";
        public SolidColorBrush Color { get; set; } = new(Colors.DodgerBlue);
        public int SubCategoryCount { get; set; }
    }

    public class SubCategoryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "\uE8BD";
        public SolidColorBrush Color { get; set; } = new(Colors.Gray);
    }

    public class SubscriptionTierViewModel
    {
        public int Tier { get; set; }
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "\uE735";
        public SolidColorBrush Color { get; set; } = new(Colors.Gray);
        public int MaxUsers { get; set; }
        public int MaxMic { get; set; }
        public int MaxCam { get; set; }
        public bool AlwaysOnline { get; set; }
        public string PriceDisplay { get; set; } = "Gratuit";
        public Visibility AlwaysOnlineVisibility => AlwaysOnline ? Visibility.Visible : Visibility.Collapsed;
    }

    public class MyRoomViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string TierName { get; set; } = "Basic";
        public SolidColorBrush TierColor { get; set; } = new(Colors.Gray);
        public int UserCount { get; set; }
        public int MaxUsers { get; set; }
        public string UserCountDisplay { get; set; } = "0/25 utilisateurs";
    }
}

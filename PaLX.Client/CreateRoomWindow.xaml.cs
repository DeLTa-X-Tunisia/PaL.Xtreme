using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PaLX.Client.Services;
using PaLX.Client.Controls;

namespace PaLX.Client
{
    public partial class CreateRoomWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly int? _editingRoomId;
        private readonly RoomViewModel? _editingRoom; // Stocker les données du salon à éditer
        private int _userRoomSubscriptionLevel = 0; // Basic par défaut
        private string? _selectedAvatarPath; // Chemin de l'avatar sélectionné
        private RoomModerationWindow? _moderationWindow; // Référence à la fenêtre de modération
        
        // Propriétés de rôle pour la gestion des permissions
        private readonly bool _isOwner = false;
        private readonly string? _userRole = null; // SuperAdmin, Admin, Moderator ou null
        
        /// <summary>
        /// Indique si l'utilisateur a un accès complet au salon (Owner OU Admin Système)
        /// </summary>
        private bool HasFullAccess => _isOwner || ApiService.Instance.IsSystemAdmin;

        // Limites par niveau d'abonnement Room
        private static readonly Dictionary<int, (int MaxMic, int MaxCam, int MaxUsers, string Name)> SubscriptionLimits = new()
        {
            { 0, (1, 2, 50, "Basic") },
            { 1, (1, 5, 100, "Deluxe") },
            { 2, (2, 8, 250, "Extreme") },
            { 3, (2, 10, 500, "VIP") },
            { 4, (3, 12, 500, "Bronze") },
            { 5, (3, 14, 1000, "Silver") },
            { 6, (4, 20, 1500, "Gold") },
            { 7, (4, 30, 2000, "Platinum") },
            { 8, (5, 50, 5000, "Ultimate") },
            { 9, (5, 100, 10000, "Legend") }
        };

        public CreateRoomWindow()
        {
            InitializeComponent();
            _apiService = ApiService.Instance;
            Loaded += Window_Loaded;
            
            // S'abonner à l'événement de suppression de rôle
            _apiService.OnRoleRemoved += OnRoleRemoved;
            Closed += (s, e) => _apiService.OnRoleRemoved -= OnRoleRemoved;
        }

        private void OnRoleRemoved(int roomId, string roomName)
        {
            // Si cette fenêtre édite le salon dont le rôle a été retiré
            if (_editingRoomId.HasValue && _editingRoomId.Value == roomId)
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        Console.WriteLine($"[CreateRoomWindow] Role removed for room {roomId}, closing window");
                        
                        // Fermer d'abord la fenêtre de modération si ouverte
                        if (_moderationWindow != null)
                        {
                            try { _moderationWindow.Close(); } catch { }
                            _moderationWindow = null;
                        }
                        
                        // Fermer cette fenêtre
                        Close();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CreateRoomWindow] Error handling role removal: {ex.Message}");
                    }
                });
            }
        }

        public CreateRoomWindow(RoomViewModel room) : this()
        {
            _editingRoomId = room.Id;
            _editingRoom = room;
            _isOwner = room.IsOwner;
            _userRole = room.UserRole;
            // Les admins système ont un accès complet
            Title = HasFullAccess ? "Modifier le Salon" : "Gestion du Salon";
            Console.WriteLine($"[CreateRoomWindow] Editing room: Id={room.Id}, Name={room.Name}, IsOwner={_isOwner}, IsSystemAdmin={ApiService.Instance.IsSystemAdmin}, HasFullAccess={HasFullAccess}, UserRole={_userRole}");
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Charger l'abonnement Room de l'utilisateur
                await LoadUserRoomSubscription();
                
                // Configurer les sliders selon l'abonnement
                ConfigureSlidersForSubscription();
                
                // Charger les catégories
                await LoadCategories();
                
                // Si on édite un salon existant, charger ses valeurs et afficher l'admin
                if (_editingRoomId.HasValue && _editingRoom != null)
                {
                    // ═══════════════════════════════════════════════════════════════
                    // ADAPTER L'INTERFACE POUR LE MODE ÉDITION
                    // ═══════════════════════════════════════════════════════════════
                    HeaderTitle.Text = "Modifier le Salon";
                    HeaderIcon.Text = "✏️";
                    SubmitButton.Content = "✓ Mettre à jour";
                    
                    // Remplir les champs avec les données du salon
                    RoomNameBox.Text = _editingRoom.Name;
                    DescriptionBox.Text = _editingRoom.Description ?? "";
                    
                    // Cocher les options
                    PrivateCheck.IsChecked = _editingRoom.IsPrivate;
                    AdultCheck.IsChecked = _editingRoom.Is18Plus;
                    
                    // Configurer les sliders
                    UsersSlider.Value = _editingRoom.MaxUsers;
                    
                    // Sélectionner la catégorie correspondante
                    if (CategoryCombo.ItemsSource is List<RoomCategoryDto> categories)
                    {
                        var matchingCategory = categories.FirstOrDefault(c => c.Id == _editingRoom.CategoryId);
                        if (matchingCategory != null)
                        {
                            CategoryCombo.SelectedItem = matchingCategory;
                        }
                    }
                    
                    // ═══════════════════════════════════════════════════════════════
                    // GESTION DES PERMISSIONS SELON LE RÔLE
                    // ═══════════════════════════════════════════════════════════════
                    ConfigurePermissionsForRole();
                }
            }
            catch (Exception ex)
            {
                ToastService.Error($"Erreur d'initialisation: {ex.Message}");
            }
        }

        /// <summary>
        /// Configure les permissions et la visibilité des éléments selon le rôle de l'utilisateur
        /// </summary>
        private void ConfigurePermissionsForRole()
        {
            Console.WriteLine($"[CreateRoomWindow] ConfigurePermissionsForRole - IsOwner={_isOwner}, IsSystemAdmin={ApiService.Instance.IsSystemAdmin}, HasFullAccess={HasFullAccess}, UserRole={_userRole}");
            
            if (HasFullAccess)
            {
                // ═══════════════════════════════════════════════════════════════
                // OWNER ou ADMIN SYSTÈME : Accès complet - peut tout modifier et gérer tous les rôles
                // ═══════════════════════════════════════════════════════════════
                AdminSection.Visibility = Visibility.Visible;
                ModerationButton.Visibility = Visibility.Visible;
                
                // Tous les champs sont modifiables
                RoomNameBox.IsEnabled = true;
                DescriptionBox.IsEnabled = true;
                CategoryCombo.IsEnabled = true;
                SubCategoryCombo.IsEnabled = true;
                AdultCheck.IsEnabled = true;
            }
            else if (_userRole == "SuperAdmin" || _userRole == "Admin")
            {
                // ═══════════════════════════════════════════════════════════════
                // SUPERADMIN / ADMIN : Peut gérer le salon mais pas modifier les infos de base
                // ═══════════════════════════════════════════════════════════════
                AdminSection.Visibility = Visibility.Visible;
                ModerationButton.Visibility = Visibility.Visible;
                
                // Champs réservés au Owner (lecture seule)
                RoomNameBox.IsEnabled = false;
                DescriptionBox.IsEnabled = false;
                CategoryCombo.IsEnabled = false;
                SubCategoryCombo.IsEnabled = false;
                AdultCheck.IsEnabled = false;
                
                // Style lecture seule
                RoomNameBox.Opacity = 0.6;
                DescriptionBox.Opacity = 0.6;
                CategoryCombo.Opacity = 0.6;
                SubCategoryCombo.Opacity = 0.6;
                AdultCheck.Opacity = 0.6;
            }
            else if (_userRole == "Moderator")
            {
                // ═══════════════════════════════════════════════════════════════
                // MODERATOR : Peut voir le salon mais pas accéder à la modération
                // ═══════════════════════════════════════════════════════════════
                AdminSection.Visibility = Visibility.Collapsed; // Pas d'accès à la modération
                
                // Champs réservés au Owner (lecture seule)
                RoomNameBox.IsEnabled = false;
                DescriptionBox.IsEnabled = false;
                CategoryCombo.IsEnabled = false;
                SubCategoryCombo.IsEnabled = false;
                AdultCheck.IsEnabled = false;
                
                // Style lecture seule
                RoomNameBox.Opacity = 0.6;
                DescriptionBox.Opacity = 0.6;
                CategoryCombo.Opacity = 0.6;
                SubCategoryCombo.Opacity = 0.6;
                AdultCheck.Opacity = 0.6;
            }
        }

        private async System.Threading.Tasks.Task LoadUserRoomSubscription()
        {
            try
            {
                // TODO: Appeler l'API pour récupérer le niveau d'abonnement Room de l'utilisateur
                // Pour l'instant, on utilise Basic (0) par défaut
                // var userProfile = await _apiService.GetUserProfileAsync();
                // _userRoomSubscriptionLevel = userProfile.RoomSubscriptionLevel;
                
                _userRoomSubscriptionLevel = 0; // Basic par défaut
            }
            catch
            {
                _userRoomSubscriptionLevel = 0;
            }
        }

        private void ConfigureSlidersForSubscription()
        {
            if (!SubscriptionLimits.TryGetValue(_userRoomSubscriptionLevel, out var limits))
            {
                limits = SubscriptionLimits[0]; // Fallback to Basic
            }

            // Mettre à jour le label d'abonnement
            SubscriptionLabel.Text = limits.Name;

            // Configurer le slider Micros
            MicSlider.Maximum = limits.MaxMic;
            MicSlider.Value = limits.MaxMic;
            MicMaxLabel.Text = $"Max: {limits.MaxMic}";
            MicValueText.Text = limits.MaxMic.ToString();

            // Configurer le slider Caméras
            CamSlider.Maximum = limits.MaxCam;
            CamSlider.Value = limits.MaxCam;
            CamMaxLabel.Text = $"Max: {limits.MaxCam}";
            CamValueText.Text = limits.MaxCam.ToString();

            // Configurer le slider Utilisateurs
            UsersSlider.Maximum = limits.MaxUsers;
            UsersSlider.Value = Math.Min(50, limits.MaxUsers); // Valeur par défaut raisonnable
            UsersSlider.TickFrequency = limits.MaxUsers > 100 ? 50 : 10;
            UsersMaxLabel.Text = $"Max: {limits.MaxUsers}";
            UsersValueText.Text = ((int)UsersSlider.Value).ToString();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Protection contre les appels pendant l'initialisation
            if (MicValueText == null || CamValueText == null || UsersValueText == null) return;

            MicValueText.Text = ((int)MicSlider.Value).ToString();
            CamValueText.Text = ((int)CamSlider.Value).ToString();
            UsersValueText.Text = ((int)UsersSlider.Value).ToString();
        }

        private async System.Threading.Tasks.Task LoadCategories()
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

        private async void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Protection contre les appels pendant l'initialisation
            if (SubCategoryCombo == null) return;

            if (CategoryCombo.SelectedItem is RoomCategoryDto category)
            {
                try
                {
                    var subCategories = await _apiService.GetRoomSubCategoriesAsync(category.Id);
                    SubCategoryCombo.ItemsSource = subCategories;
                    if (subCategories.Count > 0) SubCategoryCombo.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    ToastService.Error($"Erreur chargement sous-catégories: {ex.Message}");
                }
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void PrivateCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (PasswordPanel != null)
                PasswordPanel.Visibility = PrivateCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ChooseAvatar_Click(object sender, object e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Choisir un avatar pour le salon",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Tous les fichiers|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _selectedAvatarPath = openFileDialog.FileName;
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_selectedAvatarPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    
                    AvatarImage.Source = bitmap;
                    AvatarImage.Visibility = Visibility.Visible;
                    AvatarPlaceholder.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    ToastService.Error($"Erreur lors du chargement de l'image: {ex.Message}");
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Ne pas utiliser DialogResult car la fenêtre peut être ouverte avec Show()
            Close();
        }

        private void OpenModeration_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine($"[CreateRoomWindow] OpenModeration_Click - _editingRoomId={_editingRoomId}, HasFullAccess={HasFullAccess}, UserRole={_userRole}");
            
            // La modération n'est disponible que pour les salons existants
            if (!_editingRoomId.HasValue || _editingRoomId.Value == 0)
            {
                ToastService.Info("Créez d'abord le salon pour pouvoir gérer les rôles.", "Salon requis");
                return;
            }
            
            // Fermer l'ancienne fenêtre de modération si elle existe
            if (_moderationWindow != null)
            {
                try { _moderationWindow.Close(); } catch { }
                _moderationWindow = null;
            }
            
            string roomName = string.IsNullOrEmpty(RoomNameBox.Text) ? "Mon Salon" : RoomNameBox.Text;
            
            Console.WriteLine($"[CreateRoomWindow] Creating RoomModerationWindow with roomId={_editingRoomId.Value}, roomName={roomName}, hasFullAccess={HasFullAccess}, userRole={_userRole}");
            
            // Passer HasFullAccess (Owner OU Admin Système) pour donner les permissions complètes
            _moderationWindow = new RoomModerationWindow(_editingRoomId.Value, roomName, HasFullAccess, _userRole);
            _moderationWindow.Owner = this;
            _moderationWindow.Closed += (s, args) => _moderationWindow = null;
            _moderationWindow.Show(); // Non-bloquant pour ne pas figer l'interface
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

            var dto = new CreateRoomDto
            {
                Name = RoomNameBox.Text,
                Description = DescriptionBox.Text,
                CategoryId = ((RoomCategoryDto)CategoryCombo.SelectedItem).Id,
                SubscriptionLevel = _userRoomSubscriptionLevel,
                MaxUsers = (int)UsersSlider.Value,
                MaxMics = (int)MicSlider.Value,
                MaxCams = (int)CamSlider.Value,
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
                // Ne pas utiliser DialogResult car la fenêtre peut être ouverte avec Show() (non-modal)
                Close();
            }
            catch (Exception ex)
            {
                ToastService.Error($"Erreur: {ex.Message}");
            }
        }
    }
}

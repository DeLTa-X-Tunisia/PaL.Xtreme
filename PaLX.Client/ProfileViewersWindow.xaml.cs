using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PaLX.Client.Services;

namespace PaLX.Client
{
    public partial class ProfileViewersWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly string _baseUrl;

        public ProfileViewersWindow(ApiService apiService)
        {
            InitializeComponent();
            _apiService = apiService;
            _baseUrl = ApiService.BaseUrl.TrimEnd('/');
            
            Loaded += async (s, e) => await LoadViewersAsync();
        }

        private async System.Threading.Tasks.Task LoadViewersAsync()
        {
            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                EmptyPanel.Visibility = Visibility.Collapsed;
                ViewersScrollViewer.Visibility = Visibility.Collapsed;

                var viewers = await _apiService.GetProfileViewersAsync(50);

                LoadingPanel.Visibility = Visibility.Collapsed;

                if (viewers == null || viewers.Count == 0)
                {
                    EmptyPanel.Visibility = Visibility.Visible;
                    ViewersCountText.Text = "0 visite";
                    return;
                }

                ViewersScrollViewer.Visibility = Visibility.Visible;
                ViewersContainer.Children.Clear();

                foreach (var viewer in viewers)
                {
                    ViewersContainer.Children.Add(CreateViewerCard(viewer));
                }

                ViewersCountText.Text = viewers.Count == 1 ? "1 visite" : $"{viewers.Count} visites";
            }
            catch (Exception ex)
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                EmptyPanel.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine($"[ProfileViewersWindow] Error loading viewers: {ex.Message}");
            }
        }

        private Border CreateViewerCard(ProfileViewerDto viewer)
        {
            // Main Card Border
            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10),
                Tag = viewer.ViewerId, // Store viewerId for deletion
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.08,
                    Color = Colors.Black
                }
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Delete button column

            // Avatar
            var avatarBorder = new Border
            {
                Width = 50,
                Height = 50,
                CornerRadius = new CornerRadius(25),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1")),
                BorderThickness = new Thickness(2)
            };

            if (!string.IsNullOrEmpty(viewer.AvatarPath))
            {
                try
                {
                    var avatarUrl = viewer.AvatarPath.StartsWith("http") 
                        ? viewer.AvatarPath 
                        : $"{_baseUrl}/{viewer.AvatarPath.TrimStart('/')}";
                    
                    var ellipse = new Ellipse
                    {
                        Width = 46,
                        Height = 46,
                        Fill = new ImageBrush
                        {
                            ImageSource = new BitmapImage(new Uri(avatarUrl, UriKind.Absolute)),
                            Stretch = Stretch.UniformToFill
                        }
                    };
                    avatarBorder.Child = ellipse;
                }
                catch
                {
                    avatarBorder.Child = CreateDefaultAvatarIcon();
                }
            }
            else
            {
                avatarBorder.Child = CreateDefaultAvatarIcon();
            }

            Grid.SetColumn(avatarBorder, 0);
            grid.Children.Add(avatarBorder);

            // Info Stack (Name + Context)
            var infoStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };

            var displayName = !string.IsNullOrWhiteSpace(viewer.DisplayName) ? viewer.DisplayName : viewer.Username;
            var nameText = new TextBlock
            {
                Text = displayName,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"))
            };
            infoStack.Children.Add(nameText);

            var contextLabel = viewer.Context switch
            {
                "room" => "Depuis un salon",
                "friends" => "Depuis la liste d'amis",
                "search" => "Depuis la recherche",
                _ => "Visite de profil"
            };
            var contextText = new TextBlock
            {
                Text = contextLabel,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                Margin = new Thickness(0, 2, 0, 0)
            };
            infoStack.Children.Add(contextText);

            Grid.SetColumn(infoStack, 1);
            grid.Children.Add(infoStack);

            // Time Stack (Date + Hour)
            var timeStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var dateText = new TextBlock
            {
                Text = FormatDate(viewer.ViewedAt),
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            timeStack.Children.Add(dateText);

            var timeText = new TextBlock
            {
                Text = viewer.ViewedAt.ToLocalTime().ToString("HH:mm"),
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 0, 0)
            };
            timeStack.Children.Add(timeText);

            Grid.SetColumn(timeStack, 2);
            grid.Children.Add(timeStack);

            // Delete Button (Trash Icon)
            var deleteButton = new Button
            {
                Width = 34,
                Height = 34,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Supprimer de la liste",
                Tag = viewer.ViewerId,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Create trash icon SVG
            var trashCanvas = new Canvas { Width = 24, Height = 24 };
            var trashPath = new Path
            {
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                Data = Geometry.Parse("M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z")
            };
            trashCanvas.Children.Add(trashPath);

            var trashViewbox = new Viewbox
            {
                Width = 18,
                Height = 18,
                Child = trashCanvas
            };

            deleteButton.Content = trashViewbox;

            // Hover effects for delete button
            deleteButton.MouseEnter += (s, e) =>
            {
                trashPath.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                deleteButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
            };
            deleteButton.MouseLeave += (s, e) =>
            {
                trashPath.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                deleteButton.Background = Brushes.Transparent;
            };

            // Click handler for delete
            deleteButton.Click += async (s, e) =>
            {
                e.Handled = true;
                var viewerId = (int)((Button)s).Tag;
                await DeleteViewerAsync(viewerId, card);
            };

            // Round corners for button
            var buttonBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Child = deleteButton
            };

            Grid.SetColumn(buttonBorder, 3);
            grid.Children.Add(buttonBorder);

            card.Child = grid;

            // Hover effect for card
            card.MouseEnter += (s, e) => card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            card.MouseLeave += (s, e) => card.Background = Brushes.White;

            return card;
        }

        private async System.Threading.Tasks.Task DeleteViewerAsync(int viewerId, Border card)
        {
            try
            {
                var success = await _apiService.DeleteProfileViewerAsync(viewerId);
                if (success)
                {
                    // Remove the card from the UI with animation effect
                    card.Opacity = 0.5;
                    await System.Threading.Tasks.Task.Delay(150);
                    
                    ViewersContainer.Children.Remove(card);
                    
                    // Update counter
                    var count = ViewersContainer.Children.Count;
                    ViewersCountText.Text = count == 0 ? "0 visite" : count == 1 ? "1 visite" : $"{count} visites";
                    
                    // Show empty state if no more viewers
                    if (count == 0)
                    {
                        ViewersScrollViewer.Visibility = Visibility.Collapsed;
                        EmptyPanel.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileViewersWindow] Error deleting viewer: {ex.Message}");
            }
        }

        private Viewbox CreateDefaultAvatarIcon()
        {
            var canvas = new Canvas { Width = 24, Height = 24 };
            var path = new Path
            {
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")),
                Data = Geometry.Parse("M12,4A4,4 0 0,1 16,8A4,4 0 0,1 12,12A4,4 0 0,1 8,8A4,4 0 0,1 12,4M12,14C16.42,14 20,15.79 20,18V20H4V18C4,15.79 7.58,14 12,14Z")
            };
            canvas.Children.Add(path);
            
            return new Viewbox
            {
                Width = 28,
                Height = 28,
                Child = canvas
            };
        }

        private string FormatDate(DateTime utcDate)
        {
            var localDate = utcDate.ToLocalTime();
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            if (localDate.Date == today)
                return "Aujourd'hui";
            if (localDate.Date == yesterday)
                return "Hier";
            if (localDate.Date > today.AddDays(-7))
                return localDate.ToString("dddd", new System.Globalization.CultureInfo("fr-FR"));
            
            return localDate.ToString("dd/MM/yyyy");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

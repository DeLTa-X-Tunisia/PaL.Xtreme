// ============================================================================
// PaL.Xtreme Admin Launcher - MainWindow
// Copyright © 2026 Azizi Mounir. All Rights Reserved.
// 
// Ce launcher permet de démarrer et gérer le Panel d'Administration React
// de manière simple et élégante pour les administrateurs système.
// ============================================================================

using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PaL.X.Admin.Launcher
{
    public partial class MainWindow : Window
    {
        // ═══════════════════════════════════════════════════════════════════
        // FIELDS
        // ═══════════════════════════════════════════════════════════════════
        
        private Process? _nodeProcess;
        private readonly HttpClient _httpClient;
        private readonly DispatcherTimer _statusTimer;
        private bool _isRunning;
        private string _reactPanelPath = "";
        private readonly string _settingsPath;

        // Status colors
        private readonly SolidColorBrush _greenBrush = new(Color.FromRgb(16, 185, 129));
        private readonly SolidColorBrush _orangeBrush = new(Color.FromRgb(245, 158, 11));
        private readonly SolidColorBrush _redBrush = new(Color.FromRgb(239, 68, 68));
        private readonly SolidColorBrush _grayBrush = new(Color.FromRgb(102, 102, 102));

        // ═══════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════════

        public MainWindow()
        {
            InitializeComponent();

            // Configurer HttpClient sans cache pour détecter les changements d'état
            var handler = new HttpClientHandler
            {
                UseDefaultCredentials = false,
                PreAuthenticate = false
            };
            _httpClient = new HttpClient(handler) 
            { 
                Timeout = TimeSpan.FromSeconds(3),
                DefaultRequestVersion = new Version(1, 1)
            };
            // Désactiver le keep-alive pour forcer une nouvelle connexion à chaque vérification
            _httpClient.DefaultRequestHeaders.ConnectionClose = true;
            
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PaL.Xtreme", "AdminLauncher", "settings.json");

            // Timer pour vérifier le statut périodiquement (toutes les 3 secondes)
            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _statusTimer.Tick += async (s, e) => await CheckServicesStatusAsync();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        // ═══════════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════════

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Trouver le chemin du projet React
            FindReactPanelPath();

            // Charger les paramètres sauvegardés
            LoadSettings();

            // Vérifier le statut initial des services
            await CheckServicesStatusAsync();

            // Démarrer la vérification périodique
            _statusTimer.Start();
        }

        private void FindReactPanelPath()
        {
            // Chercher le dossier PaL.X.Admin.React relative au launcher
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            
            // Remonter jusqu'au dossier du projet
            var projectRoot = Directory.GetParent(basePath);
            while (projectRoot != null && !File.Exists(Path.Combine(projectRoot.FullName, "PaL.Xtreme.sln")))
            {
                projectRoot = projectRoot.Parent;
            }

            if (projectRoot != null)
            {
                _reactPanelPath = Path.Combine(projectRoot.FullName, "PaL.X.Admin.React");
            }
            else
            {
                // Fallback: chemin relatif depuis le launcher
                _reactPanelPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "PaL.X.Admin.React"));
            }

            // Vérifier si le chemin existe
            if (!Directory.Exists(_reactPanelPath))
            {
                ShowNotification("⚠️ Dossier PaL.X.Admin.React introuvable - Cliquez sur ⚙️ pour configurer", NotificationType.Warning);
            }
        }

        private async Task<bool> CheckNpmAvailableAsync()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c npm --version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private bool CheckReactPanelPath()
        {
            if (string.IsNullOrEmpty(_reactPanelPath) || !Directory.Exists(_reactPanelPath))
            {
                var result = MessageBox.Show(
                    $"Le dossier PaL.X.Admin.React est introuvable.\n\nChemin recherché:\n{_reactPanelPath}\n\nVoulez-vous sélectionner le dossier manuellement?",
                    "PaL.Xtreme Admin Launcher",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    return BrowseForReactFolder();
                }
                return false;
            }
            return true;
        }

        private bool BrowseForReactFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Sélectionner le dossier PaL.X.Admin.React",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (dialog.ShowDialog() == true)
            {
                var selectedPath = dialog.FolderName;
                
                // Vérifier que c'est bien un projet React (contient package.json)
                if (File.Exists(Path.Combine(selectedPath, "package.json")))
                {
                    _reactPanelPath = selectedPath;
                    ShowNotification($"✅ Dossier configuré: {Path.GetFileName(selectedPath)}", NotificationType.Success);
                    return true;
                }
                else
                {
                    ShowNotification("❌ Ce dossier ne contient pas de package.json", NotificationType.Error);
                    return false;
                }
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════════════
        // STATUS CHECKING
        // ═══════════════════════════════════════════════════════════════════

        private async Task CheckServicesStatusAsync()
        {
            // Vérifier l'API
            await CheckApiStatusAsync();

            // Vérifier le Panel React
            await CheckReactStatusAsync();
        }

        private async void RefreshApiButton_Click(object sender, RoutedEventArgs e)
        {
            // Afficher un état de chargement
            ApiStatusText.Text = "Connexion...";
            ApiStatusDot.Fill = _orangeBrush;
            
            // Désactiver le bouton pendant la vérification
            RefreshApiButton.IsEnabled = false;
            
            try
            {
                await CheckApiStatusAsync();
                
                // Afficher une notification selon le résultat
                if (ApiStatusText.Text == "En ligne")
                {
                    ShowNotification("✅ API connectée avec succès", NotificationType.Success);
                }
                else
                {
                    ShowNotification($"⚠️ API: {ApiStatusText.Text}", NotificationType.Warning);
                }
            }
            finally
            {
                RefreshApiButton.IsEnabled = true;
            }
        }

        private async Task CheckApiStatusAsync()
        {
            try
            {
                var apiUrl = ApiUrlTextBox.Text.TrimEnd('/');
                var response = await _httpClient.GetAsync($"{apiUrl}/api/auth/validate");
                
                // 401 = API fonctionne mais pas authentifié (c'est OK)
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    ApiStatusText.Text = "En ligne";
                    ApiStatusDot.Fill = _greenBrush;
                }
                else
                {
                    ApiStatusText.Text = $"Erreur ({(int)response.StatusCode})";
                    ApiStatusDot.Fill = _orangeBrush;
                }
            }
            catch (HttpRequestException)
            {
                ApiStatusText.Text = "Hors ligne";
                ApiStatusDot.Fill = _redBrush;
            }
            catch (TaskCanceledException)
            {
                ApiStatusText.Text = "Timeout";
                ApiStatusDot.Fill = _orangeBrush;
            }
            catch (Exception ex)
            {
                ApiStatusText.Text = $"Erreur: {ex.Message}";
                ApiStatusDot.Fill = _redBrush;
            }
        }

        private async Task CheckReactStatusAsync()
        {
            if (!_isRunning)
            {
                ReactStatusText.Text = "Non démarré";
                ReactStatusDot.Fill = _grayBrush;
                return;
            }

            try
            {
                var port = ReactPortTextBox.Text;
                var response = await _httpClient.GetAsync($"http://localhost:{port}");
                
                if (response.IsSuccessStatusCode)
                {
                    ReactStatusText.Text = $"En ligne (:{port})";
                    ReactStatusDot.Fill = _greenBrush;
                    OpenBrowserButton.IsEnabled = true;
                }
                else
                {
                    ReactStatusText.Text = "Démarrage...";
                    ReactStatusDot.Fill = _orangeBrush;
                }
            }
            catch
            {
                ReactStatusText.Text = "Démarrage...";
                ReactStatusDot.Fill = _orangeBrush;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // LAUNCH / STOP
        // ═══════════════════════════════════════════════════════════════════

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                ShowNotification("Le panel est déjà en cours d'exécution", NotificationType.Info);
                return;
            }

            // Vérifier que le dossier existe, sinon proposer de le sélectionner
            if (!CheckReactPanelPath())
            {
                return;
            }

            // Vérifier que npm est installé
            if (!await CheckNpmAvailableAsync())
            {
                MessageBox.Show(
                    "npm n'est pas installé ou n'est pas accessible dans le PATH.\n\nVeuillez installer Node.js depuis:\nhttps://nodejs.org/",
                    "Node.js requis",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Vérifier que node_modules existe
            var nodeModulesPath = Path.Combine(_reactPanelPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                var result = MessageBox.Show(
                    "Les dépendances npm ne sont pas installées.\nVoulez-vous les installer maintenant?\n\n(Cela peut prendre quelques minutes)",
                    "Installation requise",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await InstallDependenciesAsync();
                }
                else
                {
                    return;
                }
            }

            await StartReactPanelAsync();
        }

        private async Task InstallDependenciesAsync()
        {
            ShowLoading(true, "Installation des dépendances npm...");
            LaunchButton.IsEnabled = false;

            try
            {
                // Vérifier que npm est accessible
                if (!await CheckNpmAvailableAsync())
                {
                    ShowNotification("❌ npm n'est pas installé ou n'est pas dans le PATH.\nInstallez Node.js depuis nodejs.org", NotificationType.Error);
                    return;
                }

                var npmProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c npm install 2>&1",
                        WorkingDirectory = _reactPanelPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = false // Redirigé via 2>&1
                    }
                };

                npmProcess.Start();

                // Lire la sortie de manière asynchrone pour éviter le blocage
                var outputBuilder = new System.Text.StringBuilder();
                npmProcess.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        // Mettre à jour le message de chargement avec la progression
                        Dispatcher.Invoke(() =>
                        {
                            if (e.Data.Contains("added"))
                            {
                                ShowLoading(true, $"Installation: {e.Data}");
                            }
                        });
                    }
                };
                npmProcess.BeginOutputReadLine();

                await npmProcess.WaitForExitAsync();

                if (npmProcess.ExitCode == 0)
                {
                    ShowNotification("✅ Dépendances installées avec succès!", NotificationType.Success);
                }
                else
                {
                    var output = outputBuilder.ToString();
                    ShowNotification($"❌ Erreur npm (code {npmProcess.ExitCode})", NotificationType.Error);
                    // Log l'erreur pour debug
                    System.Diagnostics.Debug.WriteLine($"npm install failed: {output}");
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"❌ Erreur: {ex.Message}", NotificationType.Error);
            }
            finally
            {
                ShowLoading(false);
                LaunchButton.IsEnabled = true;
            }
        }

        private async Task StartReactPanelAsync()
        {
            ShowLoading(true, "Démarrage du serveur de développement...");
            LaunchButton.IsEnabled = false;

            try
            {
                var port = ReactPortTextBox.Text;
                var apiUrl = ApiUrlTextBox.Text;

                // Créer le fichier .env.local avec la configuration
                var envPath = Path.Combine(_reactPanelPath, ".env.local");
                await File.WriteAllTextAsync(envPath, $"VITE_API_URL={apiUrl}/api\nVITE_PORT={port}");

                // Démarrer le serveur Vite
                _nodeProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c npm run dev -- --port {port}",
                        WorkingDirectory = _reactPanelPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };

                _nodeProcess.Exited += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _isRunning = false;
                        UpdateUIForStoppedState();
                        ShowNotification("Panel React arrêté", NotificationType.Info);
                    });
                };

                _nodeProcess.Start();
                _isRunning = true;

                // Attendre que le serveur démarre
                await WaitForServerToStartAsync(port);

                ShowLoading(false);
                UpdateUIForRunningState();
                ShowNotification("✅ Panel Admin démarré avec succès!", NotificationType.Success);

                // Ouvrir le navigateur si configuré
                if (AutoOpenBrowserCheckBox.IsChecked == true)
                {
                    await Task.Delay(500);
                    OpenBrowser();
                }
            }
            catch (Exception ex)
            {
                ShowLoading(false);
                LaunchButton.IsEnabled = true;
                ShowNotification($"❌ Erreur: {ex.Message}", NotificationType.Error);
            }
        }

        private async Task WaitForServerToStartAsync(string port, int maxAttempts = 30)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    var response = await _httpClient.GetAsync($"http://localhost:{port}");
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch
                {
                    // Le serveur n'est pas encore prêt
                }

                LoadingText.Text = $"Démarrage en cours... ({i + 1}/{maxAttempts})";
                await Task.Delay(1000);
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopReactPanel();
        }

        private void StopReactPanel()
        {
            if (_nodeProcess != null && !_nodeProcess.HasExited)
            {
                try
                {
                    // Tuer le processus et ses enfants
                    KillProcessTree(_nodeProcess.Id);
                    _nodeProcess = null;
                    _isRunning = false;
                    UpdateUIForStoppedState();
                    ShowNotification("Panel React arrêté", NotificationType.Info);
                }
                catch (Exception ex)
                {
                    ShowNotification($"Erreur lors de l'arrêt: {ex.Message}", NotificationType.Error);
                }
            }
        }

        private void KillProcessTree(int pid)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {pid} /T /F",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();
            }
            catch
            {
                // Ignorer les erreurs
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // UI HELPERS
        // ═══════════════════════════════════════════════════════════════════

        private void UpdateUIForRunningState()
        {
            LaunchButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            OpenBrowserButton.IsEnabled = true;
            ReactStatusText.Text = $"En ligne (:{ReactPortTextBox.Text})";
            ReactStatusDot.Fill = _greenBrush;
        }

        private void UpdateUIForStoppedState()
        {
            LaunchButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            OpenBrowserButton.IsEnabled = false;
            ReactStatusText.Text = "Non démarré";
            ReactStatusDot.Fill = _grayBrush;
        }

        private void ShowLoading(bool show, string? message = null)
        {
            LoadingPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (message != null)
            {
                LoadingText.Text = message;
            }
        }

        private enum NotificationType { Info, Success, Warning, Error }

        private void ShowNotification(string message, NotificationType type)
        {
            // Pour l'instant, utiliser MessageBox
            // TODO: Implémenter un système de toast notifications
            var icon = type switch
            {
                NotificationType.Success => MessageBoxImage.Information,
                NotificationType.Warning => MessageBoxImage.Warning,
                NotificationType.Error => MessageBoxImage.Error,
                _ => MessageBoxImage.Information
            };

            if (type == NotificationType.Error || type == NotificationType.Warning)
            {
                MessageBox.Show(message, "PaL.Xtreme Admin Launcher", MessageBoxButton.OK, icon);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // BUTTON HANDLERS
        // ═══════════════════════════════════════════════════════════════════

        private void OpenBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            OpenBrowser();
        }

        private void OpenBrowser()
        {
            try
            {
                var port = ReactPortTextBox.Text;
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"http://localhost:{port}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowNotification($"Impossible d'ouvrir le navigateur: {ex.Message}", NotificationType.Error);
            }
        }

        private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logsPath = Path.Combine(_reactPanelPath, "..", "PaLX.API", "logs");
                if (Directory.Exists(logsPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = logsPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    ShowNotification("Dossier de logs introuvable", NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"Erreur: {ex.Message}", NotificationType.Error);
            }
        }

        private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Ouvrir une fenêtre de paramètres avancés
            ShowNotification("Paramètres avancés - Fonctionnalité à venir", NotificationType.Info);
        }

        // ═══════════════════════════════════════════════════════════════════
        // WINDOW CONTROLS
        // ═══════════════════════════════════════════════════════════════════

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ═══════════════════════════════════════════════════════════════════
        // SETTINGS PERSISTENCE
        // ═══════════════════════════════════════════════════════════════════

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<LauncherSettings>(json);
                    
                    if (settings != null)
                    {
                        // Utiliser les valeurs par défaut si vide ou null
                        ApiUrlTextBox.Text = string.IsNullOrWhiteSpace(settings.ApiUrl) 
                            ? "http://localhost:5145" 
                            : settings.ApiUrl;
                        ReactPortTextBox.Text = string.IsNullOrWhiteSpace(settings.ReactPort) 
                            ? "5173" 
                            : settings.ReactPort;
                        AutoOpenBrowserCheckBox.IsChecked = settings.AutoOpenBrowser;
                    }
                    else
                    {
                        // Fichier corrompu - utiliser valeurs par défaut
                        SetDefaultValues();
                    }
                }
                else
                {
                    // Pas de fichier settings - utiliser valeurs par défaut
                    SetDefaultValues();
                }
            }
            catch
            {
                // En cas d'erreur - utiliser valeurs par défaut
                SetDefaultValues();
            }
        }

        private void SetDefaultValues()
        {
            ApiUrlTextBox.Text = "http://localhost:5145";
            ReactPortTextBox.Text = "5173";
            AutoOpenBrowserCheckBox.IsChecked = true;
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new LauncherSettings
                {
                    ApiUrl = ApiUrlTextBox.Text,
                    ReactPort = ReactPortTextBox.Text,
                    AutoOpenBrowser = AutoOpenBrowserCheckBox.IsChecked ?? true
                };

                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch
            {
                // Ignorer les erreurs de sauvegarde
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════════════════════════════

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _statusTimer.Stop();
            SaveSettings();

            // Arrêter le serveur React si en cours
            if (_isRunning)
            {
                var result = MessageBox.Show(
                    "Le Panel Admin est en cours d'exécution.\nVoulez-vous l'arrêter avant de fermer?",
                    "Fermeture",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }

                if (result == MessageBoxResult.Yes)
                {
                    StopReactPanel();
                }
            }

            _httpClient.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SETTINGS MODEL
    // ═══════════════════════════════════════════════════════════════════════════

    public class LauncherSettings
    {
        public string? ApiUrl { get; set; }
        public string? ReactPort { get; set; }
        public bool AutoOpenBrowser { get; set; } = true;
    }
}

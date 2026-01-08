using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PaLX.Launcher;

public partial class MainWindow : Window
{
    private Process? _apiProcess;
    private bool _isApiRunning = false;
    private bool _isApiReady = false;
    private bool _isConsoleVisible = false;
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly DispatcherTimer _healthCheckTimer;
    private readonly DispatcherTimer _statsTimer;
    private readonly List<Process> _clientProcesses = new List<Process>();
    private DateTime _apiStartTime;
    private int _totalLaunched = 0;

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize Health Check Timer
        _healthCheckTimer = new DispatcherTimer();
        _healthCheckTimer.Interval = TimeSpan.FromSeconds(2);
        _healthCheckTimer.Tick += async (s, e) => await CheckApiHealth();

        // Initialize Stats Timer
        _statsTimer = new DispatcherTimer();
        _statsTimer.Interval = TimeSpan.FromSeconds(1);
        _statsTimer.Tick += (s, e) => UpdateStatistics();

        // Initial State
        UpdateButtonsState();
        UpdateDebugButtonState();
    }
    
    /// <summary>
    /// Retourne le chemin du fichier debug.enabled dans le dossier bin du client
    /// </summary>
    private string GetDebugFilePath()
    {
        var root = GetSolutionRoot();
        var projectPath = Path.Combine(root, "PaLX.Client");
        
        // Chercher dans win-x64 d'abord
        string binPath = Path.Combine(projectPath, "bin", "Debug", "net10.0-windows", "win-x64");
        if (!Directory.Exists(binPath))
            binPath = Path.Combine(projectPath, "bin", "Debug", "net10.0-windows");
        if (!Directory.Exists(binPath))
            binPath = Path.Combine(projectPath, "bin", "Release", "net10.0-windows", "win-x64");
        if (!Directory.Exists(binPath))
            binPath = Path.Combine(projectPath, "bin", "Release", "net10.0-windows");
            
        return Path.Combine(binPath, "debug.enabled");
    }
    
    /// <summary>
    /// Met à jour l'état visuel du bouton debug
    /// </summary>
    private void UpdateDebugButtonState()
    {
        var debugFile = GetDebugFilePath();
        bool isDebugEnabled = File.Exists(debugFile);
        
        if (isDebugEnabled)
        {
            DebugButtonText.Text = "Debug ON";
            DebugIcon.Text = "🔴";
            ToggleDebugButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c"));
        }
        else
        {
            DebugButtonText.Text = "Debug OFF";
            DebugIcon.Text = "🐛";
            ToggleDebugButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9b59b6"));
        }
    }

    private string GetSolutionRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "PaL.Xtreme.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }

    private void Api_Click(object sender, RoutedEventArgs e)
    {
        if (_isApiRunning && _apiProcess != null && !_apiProcess.HasExited)
        {
            // Stop API
            LogToConsole("Arrêt de l'API en cours...", "WARN");
            try
            {
                _apiProcess.Kill();
                _apiProcess.WaitForExit();
                LogToConsole("API arrêtée avec succès", "SUCCESS");
            }
            catch (Exception ex)
            {
                LogToConsole($"Erreur arrêt API: {ex.Message}", "ERROR");
            }
            finally
            {
                _apiProcess = null;
                _isApiRunning = false;
                _isApiReady = false;
                _healthCheckTimer.Stop();
                _statsTimer.Stop();
                UpdateApiStatus();
                UpdateButtonsState();
                ApiUptimeText.Text = "--:--:--";
            }
        }
        else
        {
            // Start API
            var root = GetSolutionRoot();
            var apiPath = Path.Combine(root, "PaLX.API");
            LogToConsole($"Démarrage API depuis: {apiPath}");
            
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run",
                WorkingDirectory = apiPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                _apiProcess = Process.Start(psi);
                _isApiRunning = true;
                _isApiReady = false;
                _apiStartTime = DateTime.Now;
                LogToConsole("Processus API démarré, en attente de disponibilité...");
                UpdateApiStatus();
                UpdateButtonsState();
                
                // Start checking for health
                _healthCheckTimer.Start();
                _statsTimer.Start();
            }
            catch (Exception ex)
            {
                LogToConsole($"Erreur lancement API: {ex.Message}", "ERROR");
                MessageBox.Show($"Erreur lors du lancement de l'API : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task CheckApiHealth()
    {
        try
        {
            var response = await _httpClient.GetAsync("http://localhost:5145/api/health");
            
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                if (!_isApiReady)
                {
                    _isApiReady = true;
                    LogToConsole("API prête et accessible sur le port 5145", "SUCCESS");
                    UpdateApiStatus();
                    UpdateButtonsState();
                }
            }
        }
        catch
        {
            if (_isApiReady)
            {
                _isApiReady = false;
                LogToConsole("API inaccessible - connexion perdue", "ERROR");
                UpdateApiStatus();
                UpdateButtonsState();
            }
        }
    }

    private void UpdateStatistics()
    {
        // Update API Uptime
        if (_isApiRunning)
        {
            var uptime = DateTime.Now - _apiStartTime;
            ApiUptimeText.Text = uptime.ToString(@"hh\:mm\:ss");
        }

        // Clean up dead client processes
        _clientProcesses.RemoveAll(p => p.HasExited);

        // Update Active Clients count
        int activeClients = _clientProcesses.Count;
        ActiveClientsText.Text = activeClients.ToString();

        // Update Total Launched
        TotalLaunchedText.Text = _totalLaunched.ToString();

        // Calculate memory usage of all client processes
        long totalMemory = 0;
        foreach (var process in _clientProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Refresh();
                    totalMemory += process.WorkingSet64;
                }
            }
            catch { }
        }
        MemoryUsageText.Text = $"{totalMemory / (1024 * 1024)} MB";

        // Update Stop All button state
        StopAllButton.IsEnabled = activeClients > 0;
    }

    private void UpdateApiStatus()
    {
        if (_isApiRunning)
        {
            if (_isApiReady)
            {
                ApiStatusText.Text = "En ligne (Prêt)";
                ApiStatusText.Foreground = (Brush)FindResource("SuccessBrush");
                ApiIndicator.Background = (Brush)FindResource("SuccessBrush");
            }
            else
            {
                ApiStatusText.Text = "Démarrage...";
                ApiStatusText.Foreground = Brushes.Orange;
                ApiIndicator.Background = Brushes.Orange;
            }
        }
        else
        {
            ApiStatusText.Text = "Arrêté";
            ApiStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6"));
            ApiIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6"));
        }
    }

    private void UpdateButtonsState()
    {
        if (ClientButton == null) return;

        if (_isApiReady)
        {
            ClientButton.IsEnabled = true;
            ClientButton.ToolTip = "Lancer une nouvelle instance client";
            ClientStatusText.Text = "Cliquez pour lancer";
        }
        else
        {
            ClientButton.IsEnabled = false;
            ClientButton.ToolTip = "En attente du démarrage de l'API…";
            ClientStatusText.Text = "En attente API...";
        }
    }

    private void Client_Click(object sender, RoutedEventArgs e)
    {
        LaunchClient();
    }

    private void LaunchClient()
    {
        var root = GetSolutionRoot();
        var projectPath = Path.Combine(root, "PaLX.Client");
        string exeName = "PaLX.Client.exe";
        
        // Check win-x64 first (RuntimeIdentifier)
        string binPath = Path.Combine(projectPath, "bin", "Debug", "net10.0-windows", "win-x64", exeName);
        
        if (!File.Exists(binPath))
        {
            binPath = Path.Combine(projectPath, "bin", "Debug", "net10.0-windows", exeName);
        }

        if (!File.Exists(binPath))
        {
            binPath = Path.Combine(projectPath, "bin", "Release", "net10.0-windows", "win-x64", exeName);
        }

        if (!File.Exists(binPath))
        {
            binPath = Path.Combine(projectPath, "bin", "Release", "net10.0-windows", exeName);
        }

        if (File.Exists(binPath))
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = binPath,
                    WorkingDirectory = Path.GetDirectoryName(binPath),
                    UseShellExecute = true 
                });

                if (process != null)
                {
                    _clientProcesses.Add(process);
                    _totalLaunched++;
                    LogToConsole($"Client #{_totalLaunched} lancé (PID: {process.Id})", "SUCCESS");
                    UpdateStatistics();
                }
                return;
            }
            catch (Exception ex)
            {
                LogToConsole($"Erreur lancement EXE: {ex.Message}", "ERROR");
                MessageBox.Show($"Erreur lancement EXE : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Fallback to dotnet run if EXE not found
        LogToConsole("EXE non trouvé, lancement via 'dotnet run'...", "WARN");
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run",
            WorkingDirectory = projectPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            var process = Process.Start(psi);
            if (process != null)
            {
                _clientProcesses.Add(process);
                _totalLaunched++;
                LogToConsole($"Client #{_totalLaunched} lancé via dotnet (PID: {process.Id})", "SUCCESS");
                UpdateStatistics();
            }
        }
        catch (Exception ex)
        {
            LogToConsole($"Erreur lancement client: {ex.Message}", "ERROR");
            MessageBox.Show($"Erreur lors du lancement du client : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopAllClients_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"Êtes-vous sûr de vouloir arrêter {_clientProcesses.Count} client(s) ?",
            "Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            StopAllClients();
        }
    }

    private void StopAllClients()
    {
        int stopped = 0;
        LogToConsole($"Arrêt de {_clientProcesses.Count} client(s)...", "WARN");
        foreach (var process in _clientProcesses.ToList())
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(1000);
                    stopped++;
                }
            }
            catch { }
        }
        _clientProcesses.Clear();
        UpdateStatistics();
        LogToConsole($"{stopped} client(s) arrêté(s)", "SUCCESS");

        if (stopped > 0)
        {
            MessageBox.Show($"{stopped} client(s) arrêté(s) avec succès.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        
        // Stop health check timer
        _healthCheckTimer.Stop();
        _statsTimer.Stop();
        
        // Stop all clients
        foreach (var process in _clientProcesses.ToList())
        {
            try 
            { 
                if (!process.HasExited) 
                {
                    process.Kill(true); // true = kill entire process tree
                    process.WaitForExit(2000);
                }
                process.Dispose();
            } 
            catch { }
        }
        _clientProcesses.Clear();

        // Kill API on exit
        if (_isApiRunning && _apiProcess != null && !_apiProcess.HasExited)
        {
            try 
            { 
                _apiProcess.Kill(true); // true = kill entire process tree
                _apiProcess.WaitForExit(2000);
                _apiProcess.Dispose();
            } 
            catch { }
        }
        
        // Dispose HttpClient
        _httpClient.Dispose();
        
        // Force application shutdown
        Application.Current.Shutdown();
        
        // Force process exit as safety net
        Environment.Exit(0);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // CONSOLE MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════
    
    private void ToggleConsole_Click(object sender, RoutedEventArgs e)
    {
        _isConsoleVisible = !_isConsoleVisible;
        
        if (_isConsoleVisible)
        {
            ConsolePanel.Visibility = Visibility.Visible;
            ConsoleButtonText.Text = "Cacher Console";
            ConsoleIcon.Text = "\uE7BA"; // Eye off icon
            this.Height = 800; // Agrandir la fenêtre pour la console
            LogToConsole("Console activée - Mode supervision");
        }
        else
        {
            ConsolePanel.Visibility = Visibility.Collapsed;
            ConsoleButtonText.Text = "Afficher Console";
            ConsoleIcon.Text = "\uE756"; // Eye icon
            this.Height = 620; // Taille normale
        }
    }
    
    private void ClearConsole_Click(object sender, RoutedEventArgs e)
    {
        ConsoleOutput.Text = "[Console effacée]\n";
    }
    
    /// <summary>
    /// Ajoute un message horodaté à la console de supervision
    /// </summary>
    private void LogToConsole(string message, string level = "INFO")
    {
        Dispatcher.Invoke(() =>
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string color = level switch
            {
                "ERROR" => "🔴",
                "WARN" => "🟡",
                "SUCCESS" => "🟢",
                _ => "⚪"
            };
            
            ConsoleOutput.Text += $"{color} [{timestamp}] {message}\n";
            ConsoleScroller.ScrollToEnd();
        });
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // CLIENT DEBUG MODE MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════
    
    private void ToggleClientDebug_Click(object sender, RoutedEventArgs e)
    {
        var debugFile = GetDebugFilePath();
        bool isCurrentlyEnabled = File.Exists(debugFile);
        
        try
        {
            if (isCurrentlyEnabled)
            {
                // Désactiver le debug
                File.Delete(debugFile);
                LogToConsole("Mode debug client DÉSACTIVÉ", "SUCCESS");
                MessageBox.Show(
                    "Mode debug désactivé.\n\nLes prochains clients lancés n'afficheront plus la console de debug.",
                    "Debug OFF",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                // Activer le debug - créer le dossier si nécessaire
                var debugDir = Path.GetDirectoryName(debugFile);
                if (!string.IsNullOrEmpty(debugDir) && !Directory.Exists(debugDir))
                {
                    Directory.CreateDirectory(debugDir);
                }
                
                File.WriteAllText(debugFile, $"Debug mode enabled at {DateTime.Now}");
                LogToConsole("Mode debug client ACTIVÉ", "WARN");
                MessageBox.Show(
                    "Mode debug activé.\n\nLes prochains clients lancés afficheront une console de debug.\n\n⚠️ Les clients déjà ouverts ne sont pas affectés.",
                    "Debug ON",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            
            UpdateDebugButtonState();
        }
        catch (Exception ex)
        {
            LogToConsole($"Erreur toggle debug: {ex.Message}", "ERROR");
            MessageBox.Show($"Erreur: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

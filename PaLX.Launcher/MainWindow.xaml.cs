using System;
using System.Diagnostics;
using System.IO;
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
    private HttpClient _httpClient = new HttpClient();
    private DispatcherTimer _healthCheckTimer;

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize Timer
        _healthCheckTimer = new DispatcherTimer();
        _healthCheckTimer.Interval = TimeSpan.FromSeconds(2);
        _healthCheckTimer.Tick += async (s, e) => await CheckApiHealth();

        // Initial State
        UpdateButtonsState();
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
            try
            {
                _apiProcess.Kill();
                _apiProcess.WaitForExit(); // Ensure it's dead
            }
            catch { }
            finally
            {
                _apiProcess = null;
                _isApiRunning = false;
                _isApiReady = false;
                _healthCheckTimer.Stop();
                UpdateApiStatus();
                UpdateButtonsState();
            }
        }
        else
        {
            // Start API
            var root = GetSolutionRoot();
            var apiPath = Path.Combine(root, "PaLX.API");
            
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
                _isApiReady = false; // Not ready yet
                UpdateApiStatus();
                UpdateButtonsState();
                
                // Start checking for health
                _healthCheckTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors du lancement de l'API : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task CheckApiHealth()
    {
        try
        {
            // Try to fetch a simple endpoint. 
            // Assuming API runs on localhost:5145 (http) or 7145 (https) based on default dotnet templates.
            // Adjust port if necessary. Based on previous context, it seems to be 5145.
            var response = await _httpClient.GetAsync("http://localhost:5145/api/health"); 
            // Or just root if health endpoint doesn't exist, but usually 404 means server is up.
            // Let's try a known endpoint or just root. If connection refused, it throws.
            
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // If we get a response (even 404 or 401), the server is reachable.
                if (!_isApiReady)
                {
                    _isApiReady = true;
                    UpdateApiStatus();
                    UpdateButtonsState();
                }
            }
        }
        catch
        {
            // Connection failed
            if (_isApiReady)
            {
                _isApiReady = false;
                UpdateApiStatus();
                UpdateButtonsState();
            }
        }
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
        if (AdminButton == null || ClientButton == null) return;

        if (_isApiReady)
        {
            AdminButton.IsEnabled = true;
            ClientButton.IsEnabled = true;
            AdminButton.ToolTip = "Lancer le panneau d'administration";
            ClientButton.ToolTip = "Lancer l'application client";
            
            AdminStatusText.Text = "Nouvelle instance";
            ClientStatusText.Text = "Nouvelle instance";
        }
        else
        {
            AdminButton.IsEnabled = false;
            ClientButton.IsEnabled = false;
            AdminButton.ToolTip = "En attente du démarrage de l’API…";
            ClientButton.ToolTip = "En attente du démarrage de l’API…";
            
            AdminStatusText.Text = "En attente API...";
            ClientStatusText.Text = "En attente API...";
        }
    }

    private void Admin_Click(object sender, RoutedEventArgs e)
    {
        LaunchApp("PaLX.Admin");
    }

    private void Client_Click(object sender, RoutedEventArgs e)
    {
        LaunchApp("PaLX.Client");
    }

    private void LaunchApp(string projectName)
    {
        var root = GetSolutionRoot();
        var projectPath = Path.Combine(root, projectName);

        // Try to find the executable directly
        // Assuming Debug build for now as per dev environment
        string exeName = projectName + ".exe";
        
        // Check win-x64 first (RuntimeIdentifier)
        string binPath = Path.Combine(projectPath, "bin", "Debug", "net10.0-windows", "win-x64", exeName);
        
        if (!File.Exists(binPath))
        {
             // Standard path
             binPath = Path.Combine(projectPath, "bin", "Debug", "net10.0-windows", exeName);
        }

        if (!File.Exists(binPath))
        {
            // Fallback to Release win-x64
            binPath = Path.Combine(projectPath, "bin", "Release", "net10.0-windows", "win-x64", exeName);
        }

        if (!File.Exists(binPath))
        {
            // Fallback to Release standard
            binPath = Path.Combine(projectPath, "bin", "Release", "net10.0-windows", exeName);
        }

        if (File.Exists(binPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = binPath,
                    WorkingDirectory = Path.GetDirectoryName(binPath),
                    UseShellExecute = true 
                });
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lancement EXE : {ex.Message}");
            }
        }

        // Fallback to dotnet run if EXE not found
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
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur lors du lancement de {projectName} : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // Kill API on exit if running? Maybe user wants to keep it running.
        // But usually a launcher manages the lifecycle.
        // Let's ask or just kill it. For now, let's kill it to be clean.
        if (_isApiRunning && _apiProcess != null && !_apiProcess.HasExited)
        {
            try { _apiProcess.Kill(); } catch { }
        }
    }
}
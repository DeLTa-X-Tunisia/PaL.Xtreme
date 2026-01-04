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
                _apiProcess.WaitForExit();
            }
            catch { }
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
                UpdateApiStatus();
                UpdateButtonsState();
                
                // Start checking for health
                _healthCheckTimer.Start();
                _statsTimer.Start();
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
            var response = await _httpClient.GetAsync("http://localhost:5145/api/health");
            
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
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
            if (_isApiReady)
            {
                _isApiReady = false;
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
                    UpdateStatistics();
                }
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lancement EXE : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var process = Process.Start(psi);
            if (process != null)
            {
                _clientProcesses.Add(process);
                _totalLaunched++;
                UpdateStatistics();
            }
        }
        catch (Exception ex)
        {
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

        if (stopped > 0)
        {
            MessageBox.Show($"{stopped} client(s) arrêté(s) avec succès.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        
        // Stop all clients
        foreach (var process in _clientProcesses)
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
        }

        // Kill API on exit
        if (_isApiRunning && _apiProcess != null && !_apiProcess.HasExited)
        {
            try { _apiProcess.Kill(); } catch { }
        }
    }
}

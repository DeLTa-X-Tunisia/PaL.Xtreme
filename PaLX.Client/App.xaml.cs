using System.Configuration;
using System.Data;
using System.Windows;

namespace PaLX.Client;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // DatabaseService removed - API First
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            // Tentative de déconnexion propre
            var disconnectTask = PaLX.Client.Services.ApiService.Instance.DisconnectAsync();
            disconnectTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch { }
        finally
        {
            base.OnExit(e);
            // Force l'arrêt du processus pour éviter les threads fantômes (WebRTC, etc.)
            System.Environment.Exit(0);
        }
    }
}


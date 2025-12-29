using System.Configuration;
using System.Data;
using System.Windows;

namespace PaLX.Admin;

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
        base.OnExit(e);
        // Force kill process to ensure no background threads (like WebRTC) keep it alive
        System.Diagnostics.Process.GetCurrentProcess().Kill();
    }
}


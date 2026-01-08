using System.Configuration;
using System.Data;
using System.Windows;

namespace PaLX.Launcher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        // Force l'arrêt complet du processus
        System.Environment.Exit(0);
    }
}


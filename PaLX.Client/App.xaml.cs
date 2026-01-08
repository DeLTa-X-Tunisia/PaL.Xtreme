// ============================================================================
// PaL.Xtreme - Modern Instant Messaging Solution
// Copyright © 2026 Azizi Mounir. All Rights Reserved.
// 
// This software is proprietary and confidential.
// Unauthorized copying, distribution, or use is strictly prohibited.
// See LICENSE file for details.
// ============================================================================

using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using PaLX.Client.Services;

namespace PaLX.Client;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // Pour forcer l'affichage d'une console de debug
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Ouvrir une console pour voir les logs (DEBUG)
        AllocConsole();
        Console.WriteLine("===========================================");
        Console.WriteLine("     PaL.Xtreme Client - Debug Console     ");
        Console.WriteLine("===========================================");
        
        // Gestionnaire d'exceptions non capturées pour debug
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Console.WriteLine($"[CRASH] UnhandledException: {ex?.Message}\n{ex?.StackTrace}");
            MessageBox.Show($"Erreur fatale:\n{ex?.Message}\n\n{ex?.StackTrace}", "Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        
        DispatcherUnhandledException += (sender, args) =>
        {
            Console.WriteLine($"[CRASH] DispatcherUnhandledException: {args.Exception.Message}\n{args.Exception.StackTrace}");
            MessageBox.Show($"Erreur UI:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}", "Crash UI", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; // Empêche le crash immédiat
        };
        
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            Console.WriteLine($"[CRASH] UnobservedTaskException: {args.Exception.Message}\n{args.Exception.StackTrace}");
            args.SetObserved(); // Empêche le crash
        };
        
        // Initialize theme from saved preferences
        ThemeService.Initialize();
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


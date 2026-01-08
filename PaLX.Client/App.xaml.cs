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
using System.IO;
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
    
    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();
    
    private static bool _debugConsoleEnabled = false;
    
    /// <summary>
    /// Vérifie si le mode debug est activé via un fichier "debug.enabled" à côté de l'exe
    /// </summary>
    private static bool IsDebugModeEnabled()
    {
        try
        {
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var exeDir = Path.GetDirectoryName(exePath);
            var debugFile = Path.Combine(exeDir ?? "", "debug.enabled");
            return File.Exists(debugFile);
        }
        catch
        {
            return false;
        }
    }
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Console de debug désactivée par défaut
        // Pour l'activer: créer un fichier "debug.enabled" à côté de l'exe
        _debugConsoleEnabled = IsDebugModeEnabled();
        
        if (_debugConsoleEnabled)
        {
            AllocConsole();
            Console.WriteLine("===========================================");
            Console.WriteLine("     PaL.Xtreme Client - Debug Console     ");
            Console.WriteLine("===========================================");
            Console.WriteLine("[INFO] Mode debug activé via fichier debug.enabled");
        }
        
        // Gestionnaire d'exceptions non capturées pour debug
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            if (_debugConsoleEnabled)
                Console.WriteLine($"[CRASH] UnhandledException: {ex?.Message}\n{ex?.StackTrace}");
            MessageBox.Show($"Erreur fatale:\n{ex?.Message}\n\n{ex?.StackTrace}", "Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        
        DispatcherUnhandledException += (sender, args) =>
        {
            if (_debugConsoleEnabled)
                Console.WriteLine($"[CRASH] DispatcherUnhandledException: {args.Exception.Message}\n{args.Exception.StackTrace}");
            MessageBox.Show($"Erreur UI:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}", "Crash UI", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; // Empêche le crash immédiat
        };
        
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            if (_debugConsoleEnabled)
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
            if (_debugConsoleEnabled)
                FreeConsole();
            base.OnExit(e);
            // Force l'arrêt du processus pour éviter les threads fantômes (WebRTC, etc.)
            System.Environment.Exit(0);
        }
    }
}


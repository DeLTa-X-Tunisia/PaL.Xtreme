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
using System.Windows;
using PaLX.Client.Services;

namespace PaLX.Client;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
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


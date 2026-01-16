// ============================================================================
// PaL.Xtreme Admin Launcher
// Copyright © 2026 Azizi Mounir. All Rights Reserved.
// ============================================================================

using System.Windows;

namespace PaL.X.Admin.Launcher
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Gérer les exceptions non gérées
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"Erreur critique: {ex?.Message}", "Erreur", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show($"Erreur: {args.Exception.Message}", "Erreur", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}

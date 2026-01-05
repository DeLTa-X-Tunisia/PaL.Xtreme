// Copyright (c) 2025 Azizi Mounir. All rights reserved.
// This software is proprietary and confidential.

using System.Windows;
using System.Windows.Input;
using PaLX.Client.Services;

namespace PaLX.Client
{
    /// <summary>
    /// Settings window for application preferences
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            
            // Initialize toggle state from ThemeService
            DarkModeToggle.IsChecked = ThemeService.IsDarkMode;
            
            // Subscribe to theme changes (for live preview)
            ThemeService.ThemeChanged += OnThemeChanged;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeService.ThemeChanged -= OnThemeChanged;
            Close();
        }

        private void DarkModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            bool isDarkMode = DarkModeToggle.IsChecked == true;
            ThemeService.SetTheme(isDarkMode);
        }

        private void OnThemeChanged()
        {
            // Update toggle if theme changed externally
            if (DarkModeToggle.IsChecked != ThemeService.IsDarkMode)
            {
                DarkModeToggle.IsChecked = ThemeService.IsDarkMode;
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            ThemeService.ThemeChanged -= OnThemeChanged;
            base.OnClosed(e);
        }
    }
}

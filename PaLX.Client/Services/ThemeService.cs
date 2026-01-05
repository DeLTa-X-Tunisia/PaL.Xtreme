// Copyright (c) 2025 Azizi Mounir. All rights reserved.
// This software is proprietary and confidential.

using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace PaLX.Client.Services
{
    /// <summary>
    /// Service for managing application themes (Light/Dark mode)
    /// </summary>
    public static class ThemeService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PaLX", "theme_settings.txt");

        public static bool IsDarkMode { get; private set; }

        public static event Action? ThemeChanged;

        /// <summary>
        /// Initialize theme from saved preferences
        /// </summary>
        public static void Initialize()
        {
            LoadThemePreference();
            ApplyTheme();
        }

        /// <summary>
        /// Toggle between light and dark theme
        /// </summary>
        public static void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;
            SaveThemePreference();
            ApplyTheme();
            ThemeChanged?.Invoke();
        }

        /// <summary>
        /// Set a specific theme
        /// </summary>
        public static void SetTheme(bool darkMode)
        {
            if (IsDarkMode != darkMode)
            {
                IsDarkMode = darkMode;
                SaveThemePreference();
                ApplyTheme();
                ThemeChanged?.Invoke();
            }
        }

        private static void LoadThemePreference()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var content = File.ReadAllText(SettingsPath).Trim();
                    IsDarkMode = content == "dark";
                }
            }
            catch
            {
                IsDarkMode = false; // Default to light theme
            }
        }

        private static void SaveThemePreference()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(SettingsPath, IsDarkMode ? "dark" : "light");
            }
            catch
            {
                // Silently fail if we can't save
            }
        }

        private static void ApplyTheme()
        {
            var resources = Application.Current.Resources;

            if (IsDarkMode)
            {
                // ═══════════════════════════════════════════════════════════
                // DARK THEME COLORS
                // ═══════════════════════════════════════════════════════════
                
                // Neutral Colors - Dark
                resources["BackgroundColor"] = ColorFromHex("#1A1A2E");
                resources["CardColor"] = ColorFromHex("#25253D");
                resources["SurfaceColor"] = ColorFromHex("#2D2D48");
                resources["BorderColor"] = ColorFromHex("#3D3D5C");
                resources["DividerColor"] = ColorFromHex("#4A4A6A");

                // Text Colors - Light for dark background
                resources["TextPrimaryColor"] = ColorFromHex("#EAEAEA");
                resources["TextSecondaryColor"] = ColorFromHex("#B0B0C3");
                resources["TextMutedColor"] = ColorFromHex("#7A7A8C");

                // Shadow Colors - Darker for dark theme
                resources["ShadowColor"] = ColorFromHex("#40000000");
                resources["ShadowDarkColor"] = ColorFromHex("#60000000");

                // Update Brushes
                resources["BackgroundBrush"] = new SolidColorBrush((Color)resources["BackgroundColor"]);
                resources["CardBackgroundBrush"] = new SolidColorBrush((Color)resources["CardColor"]);
                resources["SurfaceBrush"] = new SolidColorBrush((Color)resources["SurfaceColor"]);
                resources["BorderBrush"] = new SolidColorBrush((Color)resources["BorderColor"]);
                resources["DividerBrush"] = new SolidColorBrush((Color)resources["DividerColor"]);
                resources["TextBrush"] = new SolidColorBrush((Color)resources["TextPrimaryColor"]);
                resources["TextSecondaryBrush"] = new SolidColorBrush((Color)resources["TextSecondaryColor"]);
                resources["LightTextBrush"] = new SolidColorBrush((Color)resources["TextMutedColor"]);
            }
            else
            {
                // ═══════════════════════════════════════════════════════════
                // LIGHT THEME COLORS (Original)
                // ═══════════════════════════════════════════════════════════
                
                // Neutral Colors - Light
                resources["BackgroundColor"] = ColorFromHex("#F8F9FA");
                resources["CardColor"] = ColorFromHex("#FFFFFF");
                resources["SurfaceColor"] = ColorFromHex("#FFFFFF");
                resources["BorderColor"] = ColorFromHex("#E9ECEF");
                resources["DividerColor"] = ColorFromHex("#DEE2E6");

                // Text Colors - Dark for light background
                resources["TextPrimaryColor"] = ColorFromHex("#212529");
                resources["TextSecondaryColor"] = ColorFromHex("#6C757D");
                resources["TextMutedColor"] = ColorFromHex("#ADB5BD");

                // Shadow Colors
                resources["ShadowColor"] = ColorFromHex("#1A000000");
                resources["ShadowDarkColor"] = ColorFromHex("#33000000");

                // Update Brushes
                resources["BackgroundBrush"] = new SolidColorBrush((Color)resources["BackgroundColor"]);
                resources["CardBackgroundBrush"] = new SolidColorBrush((Color)resources["CardColor"]);
                resources["SurfaceBrush"] = new SolidColorBrush((Color)resources["SurfaceColor"]);
                resources["BorderBrush"] = new SolidColorBrush((Color)resources["BorderColor"]);
                resources["DividerBrush"] = new SolidColorBrush((Color)resources["DividerColor"]);
                resources["TextBrush"] = new SolidColorBrush((Color)resources["TextPrimaryColor"]);
                resources["TextSecondaryBrush"] = new SolidColorBrush((Color)resources["TextSecondaryColor"]);
                resources["LightTextBrush"] = new SolidColorBrush((Color)resources["TextMutedColor"]);
            }
        }

        private static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
    }
}

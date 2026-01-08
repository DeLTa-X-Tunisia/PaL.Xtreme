// Copyright (c) 2025 Azizi Mounir. All rights reserved.
// This software is proprietary and confidential.

using System;
using System.IO;
using System.Text.Json;

namespace PaLX.Client.Services
{
    /// <summary>
    /// Modèle pour les paramètres de l'application
    /// </summary>
    public class AppSettings
    {
        public bool DarkMode { get; set; } = true;
        public bool SoundNotifications { get; set; } = true;
        public bool StartupSound { get; set; } = true;
        public int SelectedCameraIndex { get; set; } = 0;
        public int SelectedMicrophoneIndex { get; set; } = 0;
        public int VideoQuality { get; set; } = 1; // 0=Basse, 1=Moyenne, 2=Haute
    }

    /// <summary>
    /// Configuration de qualité vidéo
    /// </summary>
    public class VideoQualityConfig
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Bitrate { get; set; }
        public int Fps { get; set; }
        public string Name { get; set; } = "";
    }

    /// <summary>
    /// Service de gestion des paramètres persistants de l'application
    /// </summary>
    public static class SettingsService
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PaL.Xtreme");
        
        private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");
        
        private static AppSettings? _currentSettings;
        
        /// <summary>
        /// Paramètres actuels de l'application
        /// </summary>
        public static AppSettings Current
        {
            get
            {
                if (_currentSettings == null)
                {
                    Load();
                }
                return _currentSettings!;
            }
        }

        /// <summary>
        /// Événement déclenché quand les paramètres changent
        /// </summary>
        public static event Action? SettingsChanged;

        /// <summary>
        /// Charge les paramètres depuis le fichier JSON
        /// </summary>
        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _currentSettings = new AppSettings();
                    Save(); // Créer le fichier avec les valeurs par défaut
                }
            }
            catch (Exception)
            {
                _currentSettings = new AppSettings();
            }
        }

        /// <summary>
        /// Sauvegarde les paramètres dans le fichier JSON
        /// </summary>
        public static void Save()
        {
            try
            {
                // Créer le dossier s'il n'existe pas
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }

                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                };
                
                string json = JsonSerializer.Serialize(_currentSettings, options);
                File.WriteAllText(SettingsFilePath, json);
                
                SettingsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur sauvegarde settings: {ex.Message}");
            }
        }

        #region Propriétés avec sauvegarde automatique

        /// <summary>
        /// Mode sombre activé/désactivé
        /// </summary>
        public static bool DarkMode
        {
            get => Current.DarkMode;
            set
            {
                if (Current.DarkMode != value)
                {
                    Current.DarkMode = value;
                    Save();
                }
            }
        }

        /// <summary>
        /// Sons de notification activés/désactivés
        /// </summary>
        public static bool SoundNotifications
        {
            get => Current.SoundNotifications;
            set
            {
                if (Current.SoundNotifications != value)
                {
                    Current.SoundNotifications = value;
                    Save();
                }
            }
        }

        /// <summary>
        /// Son de démarrage activé/désactivé
        /// </summary>
        public static bool StartupSound
        {
            get => Current.StartupSound;
            set
            {
                if (Current.StartupSound != value)
                {
                    Current.StartupSound = value;
                    Save();
                }
            }
        }

        /// <summary>
        /// Index de la caméra sélectionnée
        /// </summary>
        public static int SelectedCameraIndex
        {
            get => Current.SelectedCameraIndex;
            set
            {
                if (Current.SelectedCameraIndex != value)
                {
                    Current.SelectedCameraIndex = value;
                    Save();
                }
            }
        }

        /// <summary>
        /// Index du microphone sélectionné
        /// </summary>
        public static int SelectedMicrophoneIndex
        {
            get => Current.SelectedMicrophoneIndex;
            set
            {
                if (Current.SelectedMicrophoneIndex != value)
                {
                    Current.SelectedMicrophoneIndex = value;
                    Save();
                }
            }
        }

        /// <summary>
        /// Niveau de qualité vidéo (0=Basse, 1=Moyenne, 2=Haute)
        /// </summary>
        public static int VideoQuality
        {
            get => Current.VideoQuality;
            set
            {
                if (Current.VideoQuality != value)
                {
                    Current.VideoQuality = value;
                    Save();
                }
            }
        }

        /// <summary>
        /// Configurations de qualité vidéo prédéfinies
        /// Optimisées pour une bonne netteté avec VP8
        /// </summary>
        public static readonly VideoQualityConfig[] VideoQualityPresets = new[]
        {
            new VideoQualityConfig { Name = "Basse (480p)", Width = 640, Height = 480, Bitrate = 800, Fps = 24 },
            new VideoQualityConfig { Name = "Moyenne (540p)", Width = 960, Height = 540, Bitrate = 1500, Fps = 30 },
            new VideoQualityConfig { Name = "Haute (720p)", Width = 1280, Height = 720, Bitrate = 2500, Fps = 30 }
        };

        /// <summary>
        /// Obtient la configuration de qualité vidéo actuelle
        /// </summary>
        public static VideoQualityConfig CurrentVideoQuality
        {
            get
            {
                int index = Math.Clamp(VideoQuality, 0, VideoQualityPresets.Length - 1);
                return VideoQualityPresets[index];
            }
        }

        #endregion
    }
}

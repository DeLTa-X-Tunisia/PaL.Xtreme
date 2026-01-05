using System;
using System.Collections.Generic;
using System.Windows;

namespace PaLX.Client
{
    /// <summary>
    /// Service de notifications Toast moderne
    /// Remplace les MessageBox par des notifications élégantes non-bloquantes
    /// </summary>
    public static class ToastService
    {
        private static readonly List<ToastNotification> _activeToasts = new();
        private static readonly object _lock = new();

        /// <summary>
        /// Nombre de toasts actuellement affichés
        /// </summary>
        public static int ActiveToastCount
        {
            get
            {
                lock (_lock)
                {
                    return _activeToasts.Count;
                }
            }
        }

        /// <summary>
        /// Affiche une notification de succès
        /// </summary>
        public static void Success(string message, string title = "Succès", int durationMs = 4000)
        {
            ShowToast(title, message, ToastType.Success, durationMs);
        }

        /// <summary>
        /// Affiche une notification d'erreur
        /// </summary>
        public static void Error(string message, string title = "Erreur", int durationMs = 5000)
        {
            ShowToast(title, message, ToastType.Error, durationMs);
        }

        /// <summary>
        /// Affiche une notification d'avertissement
        /// </summary>
        public static void Warning(string message, string title = "Attention", int durationMs = 4500)
        {
            ShowToast(title, message, ToastType.Warning, durationMs);
        }

        /// <summary>
        /// Affiche une notification d'information
        /// </summary>
        public static void Info(string message, string title = "Information", int durationMs = 4000)
        {
            ShowToast(title, message, ToastType.Info, durationMs);
        }

        /// <summary>
        /// Affiche un toast personnalisé
        /// </summary>
        public static void Show(string title, string message, ToastType type, int durationMs = 4000)
        {
            ShowToast(title, message, type, durationMs);
        }

        private static void ShowToast(string title, string message, ToastType type, int durationMs)
        {
            // Ensure we're on the UI thread
            if (Application.Current?.Dispatcher == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var toast = new ToastNotification(title, message, type, durationMs);

                    lock (_lock)
                    {
                        _activeToasts.Add(toast);
                    }

                    toast.Show();
                }
                catch (Exception ex)
                {
                    // Fallback to MessageBox if toast fails
                    System.Diagnostics.Debug.WriteLine($"Toast error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Retire un toast de la liste active et repositionne les autres
        /// </summary>
        internal static void RemoveToast(ToastNotification toast)
        {
            lock (_lock)
            {
                int index = _activeToasts.IndexOf(toast);
                if (index >= 0)
                {
                    _activeToasts.RemoveAt(index);

                    // Repositionner les toasts restants
                    for (int i = 0; i < _activeToasts.Count; i++)
                    {
                        _activeToasts[i].UpdatePosition(i);
                    }
                }
            }
        }

        /// <summary>
        /// Ferme tous les toasts actifs
        /// </summary>
        public static void ClearAll()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                lock (_lock)
                {
                    foreach (var toast in _activeToasts.ToArray())
                    {
                        try
                        {
                            toast.Close();
                        }
                        catch { }
                    }
                    _activeToasts.Clear();
                }
            });
        }
    }
}

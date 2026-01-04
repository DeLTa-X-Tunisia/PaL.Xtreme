using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PaLX.Client.Models;

namespace PaLX.Client.Converters
{
    /// <summary>
    /// Convertit IsPlaying en icône Play/Pause (Data path)
    /// </summary>
    public class AudioIconConverter : IValueConverter
    {
        public static AudioIconConverter Instance { get; } = new AudioIconConverter();
        
        // Icône Play
        private const string PlayIcon = "M8,5.14V19.14L19,12.14L8,5.14Z";
        // Icône Pause
        private const string PauseIcon = "M14,19H18V5H14M6,19H10V5H6V19Z";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPlaying)
            {
                string pathData = isPlaying ? PauseIcon : PlayIcon;
                return Geometry.Parse(pathData);
            }
            return Geometry.Parse(PlayIcon);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit un booléen IsMine en HorizontalAlignment
    /// </summary>
    public class IsMineToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMine)
            {
                return isMine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            }
            return HorizontalAlignment.Left;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit un booléen IsMine en couleur de bulle
    /// </summary>
    public class IsMineToBubbleColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMine)
            {
                // Bleu clair pour mes messages, blanc pour les autres
                return isMine ? new SolidColorBrush(Color.FromRgb(227, 242, 253)) : new SolidColorBrush(Colors.White);
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit un booléen IsMine en bordure de bulle
    /// </summary>
    public class IsMineToBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMine)
            {
                return isMine ? new SolidColorBrush(Colors.Transparent) : new SolidColorBrush(Color.FromRgb(224, 224, 224));
            }
            return new SolidColorBrush(Color.FromRgb(224, 224, 224));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit un booléen IsMine en CornerRadius (coins arrondis différents)
    /// </summary>
    public class IsMineToCornerRadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMine)
            {
                // Coin inférieur droit plat pour mes messages, coin inférieur gauche plat pour les autres
                return isMine ? new CornerRadius(18, 18, 4, 18) : new CornerRadius(18, 18, 18, 4);
            }
            return new CornerRadius(18);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit un booléen IsMine en marge (pour positionner à gauche ou droite)
    /// </summary>
    public class IsMineToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMine)
            {
                // Marge à gauche pour mes messages, marge à droite pour les autres
                return isMine ? new Thickness(80, 4, 10, 4) : new Thickness(10, 4, 80, 4);
            }
            return new Thickness(10, 4, 10, 4);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit une URL en ImageSource
    /// </summary>
    public class UrlToImageSourceConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string url && !string.IsNullOrEmpty(url))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(url, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    return bitmap;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit une couleur hexadécimale en SolidColorBrush
    /// </summary>
    public class HexColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex)
            {
                try
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                }
                catch
                {
                    return new SolidColorBrush(Colors.Gray);
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit TransferStatus en Visibility
    /// </summary>
    public class TransferStatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TransferStatus status && parameter is string targetStatus)
            {
                bool visible = targetStatus.ToLower() switch
                {
                    "pending" => status == TransferStatus.Pending,
                    "accepted" => status == TransferStatus.Accepted,
                    "declined" => status == TransferStatus.Declined,
                    "notpending" => status != TransferStatus.Pending,
                    _ => false
                };
                return visible ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit booléen en Visibility
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                bool invert = parameter?.ToString()?.ToLower() == "invert";
                bool visible = invert ? !b : b;
                return visible ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit un double (pourcentage) en largeur de progression
    /// </summary>
    public class ProgressToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double progress)
            {
                // La largeur maximale est passée en paramètre ou défaut à 150
                double maxWidth = 150;
                if (parameter is double max) maxWidth = max;
                else if (parameter is string s && double.TryParse(s, out double parsed)) maxWidth = parsed;
                
                return (progress / 100.0) * maxWidth;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit StatusClass en couleur de fond pour les messages de statut
    /// </summary>
    public class StatusToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string statusClass)
            {
                return statusClass switch
                {
                    "status-online" => new SolidColorBrush(Color.FromRgb(232, 245, 233)),  // Light green
                    "status-busy" => new SolidColorBrush(Color.FromRgb(255, 235, 238)),    // Light red
                    "status-away" => new SolidColorBrush(Color.FromRgb(255, 243, 224)),    // Light orange
                    "status-call" => new SolidColorBrush(Color.FromRgb(227, 242, 253)),    // Light blue
                    "status-dnd" => new SolidColorBrush(Color.FromRgb(252, 228, 236)),     // Light pink/magenta
                    _ => new SolidColorBrush(Color.FromRgb(240, 240, 240))                  // Default gray
                };
            }
            return new SolidColorBrush(Color.FromRgb(240, 240, 240));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit StatusClass en couleur de texte pour les messages de statut
    /// </summary>
    public class StatusToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string statusClass)
            {
                return statusClass switch
                {
                    "status-online" => new SolidColorBrush(Color.FromRgb(46, 125, 50)),    // Green
                    "status-busy" => new SolidColorBrush(Color.FromRgb(198, 40, 40)),      // Red
                    "status-away" => new SolidColorBrush(Color.FromRgb(230, 81, 0)),       // Orange
                    "status-call" => new SolidColorBrush(Color.FromRgb(21, 101, 192)),     // Blue
                    "status-dnd" => new SolidColorBrush(Color.FromRgb(173, 20, 87)),       // Magenta/Pink
                    _ => new SolidColorBrush(Color.FromRgb(102, 102, 102))                 // Default gray
                };
            }
            return new SolidColorBrush(Color.FromRgb(102, 102, 102));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Sélecteur de template selon le type de message
    /// </summary>
    public class ChatMessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? TextMessageTemplate { get; set; }
        public DataTemplate? ImageMessageTemplate { get; set; }
        public DataTemplate? ImagePendingTemplate { get; set; }  // For pending images (receiver side)
        public DataTemplate? AudioMessageTemplate { get; set; }
        public DataTemplate? AudioPendingTemplate { get; set; }  // For pending audio files (receiver side)
        public DataTemplate? VideoMessageTemplate { get; set; }
        public DataTemplate? VideoPendingTemplate { get; set; }  // For pending videos (receiver side)
        public DataTemplate? FileTransferTemplate { get; set; }
        public DataTemplate? StatusMessageTemplate { get; set; }
        public DataTemplate? BuzzMessageTemplate { get; set; }
        public DataTemplate? BlockMessageTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is ChatMessageItem msg)
            {
                // For Image type:
                // - Sender (IsMine=true): Always show image preview
                // - Receiver + Accepted: Show full image
                // - Receiver + Pending: Show ImagePendingTemplate
                // - Declined: Show FileTransferTemplate
                if (msg.Type == ChatMessageType.Image)
                {
                    if (msg.IsMine)
                        return ImageMessageTemplate;
                    if (msg.TransferStatus == TransferStatus.Accepted)
                        return ImageMessageTemplate;
                    if (msg.TransferStatus == TransferStatus.Pending)
                        return ImagePendingTemplate ?? FileTransferTemplate;
                    return FileTransferTemplate; // Declined
                }
                
                // For Audio Files (.mp3, .wav, etc.):
                // - Sender: Always show audio player (they have the URL)
                // - Receiver + Accepted: Show audio player
                // - Receiver + Pending: Show AudioPendingTemplate or FileTransferTemplate
                // - Declined: Show FileTransferTemplate
                if (msg.Type == ChatMessageType.AudioFile)
                {
                    if (msg.IsMine)
                        return AudioMessageTemplate; // Sender sees audio player
                    if (msg.TransferStatus == TransferStatus.Accepted)
                        return AudioMessageTemplate; // Accepted = show audio player
                    if (msg.TransferStatus == TransferStatus.Pending)
                        return AudioPendingTemplate ?? FileTransferTemplate;
                    return FileTransferTemplate; // Declined
                }
                
                // For Video: Keep original behavior
                if (msg.Type == ChatMessageType.Video)
                {
                    return msg.TransferStatus == TransferStatus.Accepted ? VideoMessageTemplate : FileTransferTemplate;
                }
                
                return msg.Type switch
                {
                    ChatMessageType.Text => TextMessageTemplate,
                    ChatMessageType.AudioMessage => AudioMessageTemplate,
                    ChatMessageType.File or ChatMessageType.FileTransfer => FileTransferTemplate,
                    ChatMessageType.Status or ChatMessageType.System => StatusMessageTemplate,
                    ChatMessageType.Buzz => BuzzMessageTemplate,
                    ChatMessageType.Block => BlockMessageTemplate,
                    _ => TextMessageTemplate
                };
            }
            return TextMessageTemplate;
        }
    }

    /// <summary>
    /// Multi-converter pour combiner IsMine et TransferStatus pour les boutons d'action
    /// </summary>
    public class ShowActionsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is bool isMine && values[1] is TransferStatus status)
            {
                // Afficher les boutons d'action seulement si:
                // - Ce n'est PAS mon message (je reçois la demande)
                // - Le statut est en attente
                return (!isMine && status == TransferStatus.Pending) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

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
                    "status-unblock" => new SolidColorBrush(Color.FromRgb(214, 234, 248)), // Light blue for unblock (#D6EAF8)
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
                    "status-unblock" => new SolidColorBrush(Color.FromRgb(52, 152, 219)),  // Blue #3498DB for unblock
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
                
                // For Video:
                // - Sender (IsMine=true): Always show video player (they have the URL)
                // - Receiver + Accepted: Show video player
                // - Receiver + Pending: Show elegant VideoPendingTemplate
                // - Receiver + Declined: Show FileTransferTemplate
                if (msg.Type == ChatMessageType.Video)
                {
                    if (msg.IsMine)
                        return VideoMessageTemplate; // Sender always sees their video
                    if (msg.TransferStatus == TransferStatus.Accepted)
                        return VideoMessageTemplate;
                    if (msg.TransferStatus == TransferStatus.Pending)
                        return VideoPendingTemplate ?? FileTransferTemplate; // Elegant pending template
                    return FileTransferTemplate; // Declined
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

    /// <summary>
    /// Convertit une extension de fichier en icône SVG Path Data
    /// </summary>
    public class FileExtensionToIconConverter : IValueConverter
    {
        // Excel icon - spreadsheet moderne
        private const string ExcelIcon = "M21.17 3.25Q21.5 3.25 21.76 3.5 22 3.74 22 4.08V19.92Q22 20.26 21.76 20.5 21.5 20.75 21.17 20.75H7.83Q7.5 20.75 7.24 20.5 7 20.26 7 19.92V17H2.83Q2.5 17 2.24 16.76 2 16.5 2 16.17V7.83Q2 7.5 2.24 7.24 2.5 7 2.83 7H7V4.08Q7 3.74 7.24 3.5 7.5 3.25 7.83 3.25M7 13.06L8.18 15.28H9.97L8 12.06L9.93 8.89H8.22L7.13 10.9L7.09 10.96L7.06 11.03Q6.8 10.5 6.5 9.96 6.25 9.43 5.97 8.89H4.16L6.05 12.08L4 15.28H5.78M13.88 19.5V17H8.25V19.5M13.88 15.75V12.63H12V15.75M13.88 11.38V8.25H12V11.38M13.88 7V4.5H8.25V7M20.75 19.5V17H15.13V19.5M20.75 15.75V12.63H15.13V15.75M20.75 11.38V8.25H15.13V11.38M20.75 7V4.5H15.13V7Z";
        
        // Word icon - document moderne
        private const string WordIcon = "M21.17 3.25Q21.5 3.25 21.76 3.5 22 3.74 22 4.08V19.92Q22 20.26 21.76 20.5 21.5 20.75 21.17 20.75H7.83Q7.5 20.75 7.24 20.5 7 20.26 7 19.92V17H2.83Q2.5 17 2.24 16.76 2 16.5 2 16.17V7.83Q2 7.5 2.24 7.24 2.5 7 2.83 7H7V4.08Q7 3.74 7.24 3.5 7.5 3.25 7.83 3.25M7.43 11.22L8.55 15.22H9.8L11.04 8.78H9.86L9.28 12.69L8.21 8.78H6.72L5.61 12.72L5.05 8.78H3.79L5 15.22H6.27M13.88 19.5V17H8.25V19.5M13.88 15.75V12.63H12V15.75M13.88 11.38V8.25H12V11.38M13.88 7V4.5H8.25V7M20.75 19.5V17H15.13V19.5M20.75 15.75V12.63H15.13V15.75M20.75 11.38V8.25H15.13V11.38M20.75 7V4.5H15.13V7Z";
        
        // PDF icon - document PDF moderne
        private const string PdfIcon = "M19 3H5C3.9 3 3 3.9 3 5V19C3 20.1 3.9 21 5 21H19C20.1 21 21 20.1 21 19V5C21 3.9 20.1 3 19 3M9.5 11.5C9.5 12.3 8.8 13 8 13H7V15H5.5V9H8C8.8 9 9.5 9.7 9.5 10.5V11.5M14.5 13.5C14.5 14.3 13.8 15 13 15H10.5V9H13C13.8 9 14.5 9.7 14.5 10.5V13.5M18.5 10.5H17V11.5H18.5V13H17V15H15.5V9H18.5V10.5M12 10.5H13V13.5H12V10.5M7 10.5H8V11.5H7V10.5Z";
        
        // Archive icon - ZIP/RAR moderne
        private const string ArchiveIcon = "M20 6H12L10 4H4C2.9 4 2 4.9 2 6V18C2 19.1 2.9 20 4 20H20C21.1 20 22 19.1 22 18V8C22 6.9 21.1 6 20 6M18 12H16V14H18V16H16V18H14V16H16V14H14V12H16V10H14V8H16V10H18V12Z";
        
        // PowerPoint icon
        private const string PptIcon = "M21.17 3.25Q21.5 3.25 21.76 3.5 22 3.74 22 4.08V19.92Q22 20.26 21.76 20.5 21.5 20.75 21.17 20.75H7.83Q7.5 20.75 7.24 20.5 7 20.26 7 19.92V17H2.83Q2.5 17 2.24 16.76 2 16.5 2 16.17V7.83Q2 7.5 2.24 7.24 2.5 7 2.83 7H7V4.08Q7 3.74 7.24 3.5 7.5 3.25 7.83 3.25M8.34 15.22H9.84V13.08H11.08C11.63 13.08 12.09 12.86 12.45 12.42 12.81 12 13 11.46 13 10.84 13 10.22 12.81 9.69 12.45 9.27 12.09 8.84 11.63 8.63 11.08 8.63H8.34V15.22M9.84 11.81V9.89H10.84C10.97 9.89 11.08 9.95 11.17 10.06 11.27 10.16 11.31 10.33 11.31 10.54V11.13C11.31 11.34 11.27 11.5 11.17 11.64 11.08 11.75 10.97 11.81 10.84 11.81H9.84M13.88 19.5V17H8.25V19.5H13.88M13.88 15.75V12.63H12V15.75H13.88M13.88 11.38V8.25H12V11.38H13.88M13.88 7V4.5H8.25V7H13.88M20.75 19.5V17H15.13V19.5H20.75M20.75 15.75V12.63H15.13V15.75H20.75M20.75 11.38V8.25H15.13V11.38H20.75M20.75 7V4.5H15.13V7H20.75Z";
        
        // Default file icon
        private const string DefaultIcon = "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20Z";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string ext = (value as string)?.ToLowerInvariant() ?? "";
            ext = ext.TrimStart('.');
            
            string pathData = ext switch
            {
                "xls" or "xlsx" or "xlsm" or "csv" => ExcelIcon,
                "doc" or "docx" or "docm" or "rtf" => WordIcon,
                "pdf" => PdfIcon,
                "ppt" or "pptx" or "pptm" => PptIcon,
                "zip" or "rar" or "7z" or "tar" or "gz" or "bz2" => ArchiveIcon,
                _ => DefaultIcon
            };
            
            return Geometry.Parse(pathData);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit une extension de fichier en couleur d'icône
    /// </summary>
    public class FileExtensionToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string ext = (value as string)?.ToLowerInvariant() ?? "";
            ext = ext.TrimStart('.');
            
            Color color = ext switch
            {
                "xls" or "xlsx" or "xlsm" or "csv" => Color.FromRgb(33, 115, 70),      // Excel green
                "doc" or "docx" or "docm" or "rtf" => Color.FromRgb(43, 87, 154),      // Word blue
                "pdf" => Color.FromRgb(208, 45, 45),                                     // PDF red
                "ppt" or "pptx" or "pptm" => Color.FromRgb(207, 79, 42),               // PowerPoint orange
                "zip" or "rar" or "7z" or "tar" or "gz" or "bz2" => Color.FromRgb(255, 167, 38), // Archive amber
                _ => Color.FromRgb(102, 102, 102)                                       // Default gray
            };
            
            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convertit une extension de fichier en couleur de fond d'icône
    /// </summary>
    public class FileExtensionToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string ext = (value as string)?.ToLowerInvariant() ?? "";
            ext = ext.TrimStart('.');
            
            Color color = ext switch
            {
                "xls" or "xlsx" or "xlsm" or "csv" => Color.FromRgb(232, 245, 233),     // Light green
                "doc" or "docx" or "docm" or "rtf" => Color.FromRgb(227, 242, 253),     // Light blue
                "pdf" => Color.FromRgb(255, 235, 238),                                    // Light red
                "ppt" or "pptx" or "pptm" => Color.FromRgb(255, 243, 224),              // Light orange
                "zip" or "rar" or "7z" or "tar" or "gz" or "bz2" => Color.FromRgb(255, 248, 225), // Light amber
                _ => Color.FromRgb(245, 245, 245)                                        // Default light gray
            };
            
            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Multi-converter pour déterminer si un fichier peut être ouvert (IsMine OU Accepted)
    /// </summary>
    public class CanOpenFileConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is bool isMine && values[1] is TransferStatus status)
            {
                // Peut ouvrir si: expéditeur (IsMine) OU si accepté
                return (isMine || status == TransferStatus.Accepted) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

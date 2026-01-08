using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace PaLX.Client.Models
{
    /// <summary>
    /// Types de messages supportés dans le chat
    /// </summary>
    public enum ChatMessageType
    {
        Text,           // Message texte simple
        Image,          // Image (legacy [IMAGE] ou transfert accepté)
        AudioMessage,   // Message audio enregistré [AUDIO_MSG]
        AudioFile,      // Fichier audio transféré (.mp3, .wav, etc.)
        Video,          // Vidéo
        File,           // Fichier générique
        FileTransfer,   // Demande de transfert (en attente)
        Status,         // Message de statut (en ligne, hors ligne, etc.)
        Buzz,           // Message BUZZ
        Block,          // Message de blocage/déblocage
        System          // Message système
    }

    /// <summary>
    /// Statut d'un transfert de fichier
    /// </summary>
    public enum TransferStatus
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2
    }

    /// <summary>
    /// Modèle de données pour un message de chat - WPF natif
    /// </summary>
    public class ChatMessageItem : INotifyPropertyChanged
    {
        private int _id;
        private ChatMessageType _type;
        private string _content = string.Empty;
        private string _formattedContent = string.Empty;
        private bool _isMine;
        private DateTime _timestamp;
        private string _timeString = string.Empty;
        private string _senderName = string.Empty;
        
        // Pour les transferts de fichiers
        private int _transferId;
        private string _fileName = string.Empty;
        private string _fileUrl = string.Empty;
        private TransferStatus _transferStatus;
        private long _fileSize;
        private string _fileExtension = string.Empty;
        
        // Pour les messages audio
        private int _audioDuration;
        private bool _isAudioPlaying;
        private double _audioProgress;
        private bool _isAudioListened;
        
        // Pour les statuts
        private string _statusClass = string.Empty;
        private string _statusIcon = string.Empty;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public ChatMessageType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); }
        }

        public string FormattedContent
        {
            get => _formattedContent;
            set { _formattedContent = value; OnPropertyChanged(); }
        }

        public bool IsMine
        {
            get => _isMine;
            set { _isMine = value; OnPropertyChanged(); OnPropertyChanged(nameof(BubbleAlignment)); OnPropertyChanged(nameof(BubbleBackground)); }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(); TimeString = value.ToString("HH:mm"); }
        }

        public string TimeString
        {
            get => _timeString;
            set { _timeString = value; OnPropertyChanged(); }
        }

        public string SenderName
        {
            get => _senderName;
            set { _senderName = value; OnPropertyChanged(); }
        }

        // Propriétés calculées pour l'affichage
        public string BubbleAlignment => IsMine ? "Right" : "Left";
        public string BubbleBackground => IsMine ? "#E3F2FD" : "#FFFFFF";
        public string BubbleBorderBrush => IsMine ? "Transparent" : "#E0E0E0";

        // Transfert de fichiers
        public int TransferId
        {
            get => _transferId;
            set { _transferId = value; OnPropertyChanged(); }
        }

        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(); FileExtension = System.IO.Path.GetExtension(value)?.TrimStart('.').ToUpper() ?? ""; }
        }

        public string FileUrl
        {
            get => _fileUrl;
            set { _fileUrl = value; OnPropertyChanged(); }
        }

        public TransferStatus TransferStatus
        {
            get => _transferStatus;
            set { _transferStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPending)); OnPropertyChanged(nameof(IsAccepted)); OnPropertyChanged(nameof(IsDeclined)); OnPropertyChanged(nameof(StatusText)); }
        }

        public bool IsPending => TransferStatus == TransferStatus.Pending;
        public bool IsAccepted => TransferStatus == TransferStatus.Accepted;
        public bool IsDeclined => TransferStatus == TransferStatus.Declined;

        public string StatusText => TransferStatus switch
        {
            TransferStatus.Pending => "En attente...",
            TransferStatus.Accepted => "Accepté",
            TransferStatus.Declined => "Refusé",
            _ => ""
        };

        public long FileSize
        {
            get => _fileSize;
            set { _fileSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSizeString)); }
        }

        public string FileSizeString
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
                return $"{FileSize / (1024.0 * 1024.0):F1} MB";
            }
        }

        public string FileExtension
        {
            get => _fileExtension;
            set { _fileExtension = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileIcon)); }
        }

        public string FileIcon
        {
            get
            {
                var ext = FileExtension.ToLower();
                return ext switch
                {
                    "jpg" or "jpeg" or "png" or "gif" or "bmp" or "webp" => "📷",
                    "mp4" or "avi" or "mov" or "mkv" or "webm" => "🎥",
                    "mp3" or "wav" or "ogg" or "m4a" or "aac" or "wma" or "flac" => "🎵",
                    "pdf" => "📕",
                    "doc" or "docx" => "📘",
                    "xls" or "xlsx" => "📗",
                    "ppt" or "pptx" => "📙",
                    "zip" or "rar" or "7z" => "📦",
                    "txt" => "📝",
                    _ => "📄"
                };
            }
        }

        // Audio
        public int AudioDuration
        {
            get => _audioDuration;
            set { _audioDuration = value; OnPropertyChanged(); OnPropertyChanged(nameof(AudioDurationString)); }
        }

        public string AudioDurationString
        {
            get
            {
                int min = AudioDuration / 60;
                int sec = AudioDuration % 60;
                return $"{min}:{sec:D2}";
            }
        }

        public bool IsAudioPlaying
        {
            get => _isAudioPlaying;
            set { _isAudioPlaying = value; OnPropertyChanged(); OnPropertyChanged(nameof(AudioPlayIcon)); }
        }

        // Alias for backwards compatibility
        public bool IsPlaying
        {
            get => _isAudioPlaying;
            set => IsAudioPlaying = value;
        }

        public string AudioPlayIcon => IsAudioPlaying ? "⏸" : "▶";

        public double AudioProgress
        {
            get => _audioProgress;
            set { _audioProgress = Math.Max(0, Math.Min(100, value)); OnPropertyChanged(); }
        }

        public bool IsAudioListened
        {
            get => _isAudioListened;
            set { _isAudioListened = value; OnPropertyChanged(); }
        }

        // Alias properties for URL access
        public string ImageUrl
        {
            get => _fileUrl;
            set => FileUrl = value;
        }

        public string VideoUrl
        {
            get => _fileUrl;
            set => FileUrl = value;
        }

        public string AudioUrl
        {
            get => _fileUrl;
            set => FileUrl = value;
        }

        // Statut
        public string StatusClass
        {
            get => _statusClass;
            set { _statusClass = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); }
        }

        public string StatusColor
        {
            get
            {
                return StatusClass switch
                {
                    "status-online" => "#4CAF50",
                    "status-busy" => "#F44336",
                    "status-away" => "#FF9800",
                    "status-offline" => "#9E9E9E",
                    "status-blocked" => "#D32F2F",
                    "status-unblocked" => "#2E7D32",
                    "status-buzz-sent" => "#2196F3",
                    "status-buzz-received" => "#FF9800",
                    _ => "#9E9E9E"
                };
            }
        }

        public string StatusIcon
        {
            get => _statusIcon;
            set { _statusIcon = value; OnPropertyChanged(); }
        }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // Factory methods
        public static ChatMessageItem CreateTextMessage(string content, bool isMine, DateTime timestamp, int id = 0)
        {
            return new ChatMessageItem
            {
                Id = id,
                Type = ChatMessageType.Text,
                Content = content,
                FormattedContent = EmojiConverter.ConvertEmojis(content),
                IsMine = isMine,
                Timestamp = timestamp
            };
        }

        // Overload with sender for compatibility
        public static ChatMessageItem CreateTextMessage(string sender, bool isMine, string content, DateTime timestamp, int id = 0)
        {
            var msg = CreateTextMessage(content, isMine, timestamp, id);
            msg.SenderName = sender;
            return msg;
        }

        public static ChatMessageItem CreateImageMessage(string url, bool isMine, DateTime timestamp, int id = 0)
        {
            return new ChatMessageItem
            {
                Id = id,
                Type = ChatMessageType.Image,
                FileUrl = url,
                IsMine = isMine,
                Timestamp = timestamp
            };
        }

        // Overload with sender for compatibility
        public static ChatMessageItem CreateImageMessage(string sender, bool isMine, string url, DateTime timestamp)
        {
            var msg = CreateImageMessage(url, isMine, timestamp);
            msg.SenderName = sender;
            return msg;
        }

        public static ChatMessageItem CreateAudioMessage(string url, int duration, bool isMine, DateTime timestamp, int id = 0)
        {
            return new ChatMessageItem
            {
                Id = id,
                Type = ChatMessageType.AudioMessage,
                FileUrl = url,
                AudioDuration = duration,
                IsMine = isMine,
                Timestamp = timestamp,
                SenderName = string.Empty
            };
        }

        // Overload with sender and duration string
        public static ChatMessageItem CreateAudioMessage(string sender, bool isMine, string url, string durationString, DateTime timestamp)
        {
            // Parse duration string like "1:30" to seconds
            int duration = 0;
            if (!string.IsNullOrEmpty(durationString))
            {
                var parts = durationString.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int min) && int.TryParse(parts[1], out int sec))
                {
                    duration = min * 60 + sec;
                }
            }
            var msg = CreateAudioMessage(url, duration, isMine, timestamp);
            msg.SenderName = sender;
            return msg;
        }

        public static ChatMessageItem CreateFileTransfer(int transferId, string fileName, string url, bool isMine, TransferStatus status, DateTime timestamp)
        {
            var ext = System.IO.Path.GetExtension(fileName)?.TrimStart('.').ToLower() ?? "";
            var type = ext switch
            {
                "jpg" or "jpeg" or "png" or "gif" or "bmp" or "webp" => ChatMessageType.Image,
                "mp4" or "avi" or "mov" or "mkv" or "webm" => ChatMessageType.Video,
                "mp3" or "wav" or "ogg" or "m4a" or "aac" or "wma" or "flac" => ChatMessageType.AudioMessage,
                _ => ChatMessageType.File
            };

            // Si accepté et c'est une image/vidéo, on affiche directement
            if (status == TransferStatus.Accepted && (type == ChatMessageType.Image || type == ChatMessageType.Video))
            {
                return new ChatMessageItem
                {
                    TransferId = transferId,
                    Type = type,
                    FileName = fileName,
                    FileUrl = url,
                    IsMine = isMine,
                    TransferStatus = status,
                    Timestamp = timestamp
                };
            }

            return new ChatMessageItem
            {
                TransferId = transferId,
                Type = ChatMessageType.FileTransfer,
                FileName = fileName,
                FileUrl = url,
                IsMine = isMine,
                TransferStatus = status,
                Timestamp = timestamp
            };
        }

        // Overload with sender and label for compatibility
        public static ChatMessageItem CreateFileTransfer(int transferId, string sender, bool isMine, string fileName, string url, string label, TransferStatus status, DateTime timestamp)
        {
            var msg = CreateFileTransfer(transferId, fileName, url, isMine, status, timestamp);
            msg.SenderName = sender;
            return msg;
        }

        public static ChatMessageItem CreateStatusMessage(string content, string statusClass, string icon = "")
        {
            return new ChatMessageItem
            {
                Type = ChatMessageType.Status,
                Content = content,
                StatusClass = statusClass,
                StatusIcon = icon,
                Timestamp = DateTime.Now
            };
        }

        // Overload with DateTime parameter for compatibility
        public static ChatMessageItem CreateStatusMessage(string content, DateTime timestamp)
        {
            return new ChatMessageItem
            {
                Type = ChatMessageType.Status,
                Content = content,
                StatusClass = "status-offline",
                Timestamp = timestamp
            };
        }

        public static ChatMessageItem CreateBuzzMessage(string content, bool isSent)
        {
            return new ChatMessageItem
            {
                Type = ChatMessageType.Buzz,
                Content = content,
                StatusClass = isSent ? "status-buzz-sent" : "status-buzz-received",
                Timestamp = DateTime.Now
            };
        }

        // Overload with DateTime for compatibility
        public static ChatMessageItem CreateBuzzMessage(string content, bool isSent, DateTime timestamp)
        {
            return new ChatMessageItem
            {
                Type = ChatMessageType.Buzz,
                Content = content,
                StatusClass = isSent ? "status-buzz-sent" : "status-buzz-received",
                Timestamp = timestamp
            };
        }

        public static ChatMessageItem CreateBlockMessage(string content, bool isBlock)
        {
            return new ChatMessageItem
            {
                Type = ChatMessageType.Block,
                Content = content,
                StatusClass = isBlock ? "status-blocked" : "status-unblocked",
                StatusIcon = isBlock ? "🔒" : "🔓",
                Timestamp = DateTime.Now
            };
        }

        // Overload with DateTime for compatibility
        public static ChatMessageItem CreateBlockMessage(string content, bool isBlock, DateTime timestamp)
        {
            return new ChatMessageItem
            {
                Type = ChatMessageType.Block,
                Content = content,
                StatusClass = isBlock ? "status-blocked" : "status-unblocked",
                StatusIcon = isBlock ? "🔒" : "🔓",
                Timestamp = timestamp
            };
        }
    }

    /// <summary>
    /// Convertisseur d'emojis - transforme les codes en emojis/images
    /// </summary>
    public static class EmojiConverter
    {
        private static readonly Dictionary<string, string> TextEmojis = new()
        {
            { ":)", "😊" }, { ":-)", "😊" }, { ":D", "😃" }, { ":-D", "😃" },
            { ":(", "😞" }, { ":-(", "😞" }, { ";)", "😉" }, { ";-)", "😉" },
            { ":P", "😛" }, { ":-P", "😛" }, { ":p", "😛" }, { ":-p", "😛" },
            { ":O", "😮" }, { ":-O", "😮" }, { ":o", "😮" }, { ":-o", "😮" },
            { "<3", "❤️" }, { ":*", "😘" }, { ":-*", "😘" },
            { "XD", "😆" }, { "xD", "😆" }, { "B)", "😎" }, { "B-)", "😎" },
            { ":/", "😕" }, { ":-/", "😕" }, { ":|", "😐" }, { ":-|", "😐" },
            { ":'(", "😢" }, { ":'-(", "😢" }, { ">:(", "😠" }, { ">:-(", "😠" },
            { ":@", "😡" }, { "O:)", "😇" }, { "0:)", "😇" },
            { "^_^", "😊" }, { "-_-", "😑" }, { ">_<", "😣" },
            { "(y)", "👍" }, { "(n)", "👎" }, { "(ok)", "👌" },
            { "(heart)", "❤️" }, { "(star)", "⭐" }, { "(sun)", "☀️" },
            { "(moon)", "🌙" }, { "(fire)", "🔥" }, { "(thumbsup)", "👍" },
            { "(thumbsdown)", "👎" }, { "(clap)", "👏" }, { "(wave)", "👋" }
        };

        public static string ConvertEmojis(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Protéger les tags [smiley:...] avant conversion
            var smileyTags = new List<string>();
            var smileyRegex = new System.Text.RegularExpressions.Regex(@"\[smiley:[^\]]+\]");
            string result = smileyRegex.Replace(input, m => {
                smileyTags.Add(m.Value);
                return $"<<SMILEY_{smileyTags.Count - 1}>>";
            });

            // Convertir les codes texte en emojis Unicode
            foreach (var kvp in TextEmojis)
            {
                result = result.Replace(kvp.Key, kvp.Value);
            }

            // Restaurer les tags [smiley:...]
            for (int i = 0; i < smileyTags.Count; i++)
            {
                result = result.Replace($"<<SMILEY_{i}>>", smileyTags[i]);
            }

            return result;
        }

        public static bool ContainsCustomSmiley(string input)
        {
            return input.Contains("[smiley:");
        }

        public static List<(string Code, string ImagePath)> ExtractCustomSmileys(string input)
        {
            var result = new List<(string, string)>();
            var regex = new System.Text.RegularExpressions.Regex(@"\[smiley:(b_s_\d+\.png)\]");
            var matches = regex.Matches(input);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string code = match.Value;
                string imageName = match.Groups[1].Value;
                string imagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Smiley", imageName);
                result.Add((code, imagePath));
            }
            
            return result;
        }
    }
}

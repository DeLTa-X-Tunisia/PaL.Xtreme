using System;

namespace PaLX.API.Models
{
    public class FileTransfer
    {
        public int Id { get; set; }
        public string SenderUsername { get; set; } = string.Empty;
        public string ReceiverUsername { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? ContentType { get; set; }
        public DateTime SentAt { get; set; }
        public int Status { get; set; } // 0: Pending, 1: Accepted, 2: Declined
    }
}

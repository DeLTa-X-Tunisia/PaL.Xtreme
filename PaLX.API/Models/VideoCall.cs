// Copyright (c) 2026 Azizi Mounir. All rights reserved.

namespace PaLX.API.Models
{
    /// <summary>
    /// Video call entity for database
    /// </summary>
    public class VideoCall
    {
        public int Id { get; set; }
        public Guid CallId { get; set; }
        public int CallerId { get; set; }
        public int CalleeId { get; set; }
        public VideoCallStatus Status { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Duration { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation
        public string? CallerUsername { get; set; }
        public string? CalleeUsername { get; set; }
    }

    public enum VideoCallStatus
    {
        Pending = 0,    // Appel créé, en attente
        Ringing = 1,    // Sonnerie en cours
        Active = 2,     // Appel en cours
        Ended = 3,      // Appel terminé normalement
        Missed = 4,     // Appel manqué
        Declined = 5    // Appel refusé
    }

    /// <summary>
    /// Video call log entry
    /// </summary>
    public class VideoCallLog
    {
        public int Id { get; set; }
        public int VideoCallId { get; set; }
        public string Event { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

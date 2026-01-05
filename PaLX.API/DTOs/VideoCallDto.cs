// Copyright (c) 2026 Azizi Mounir. All rights reserved.

namespace PaLX.API.DTOs
{
    /// <summary>
    /// DTO for initiating a video call
    /// </summary>
    public class VideoCallRequestDto
    {
        public string CalleeUsername { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for video call response
    /// </summary>
    public class VideoCallDto
    {
        public int Id { get; set; }
        public Guid CallId { get; set; }
        public string CallerUsername { get; set; } = string.Empty;
        public string CalleeUsername { get; set; } = string.Empty;
        public string? CallerAvatar { get; set; }
        public string? CalleeAvatar { get; set; }
        public int Status { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Duration { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO for video call history
    /// </summary>
    public class VideoCallHistoryDto
    {
        public List<VideoCallDto> Calls { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalDuration { get; set; }
    }

    /// <summary>
    /// DTO for WebRTC signaling
    /// </summary>
    public class VideoSignalingDto
    {
        public Guid CallId { get; set; }
        public string Type { get; set; } = string.Empty; // offer, answer, ice-candidate
        public string Sdp { get; set; } = string.Empty;
        public string? IceCandidate { get; set; }
        public int? SdpMLineIndex { get; set; }
        public string? SdpMid { get; set; }
    }
}

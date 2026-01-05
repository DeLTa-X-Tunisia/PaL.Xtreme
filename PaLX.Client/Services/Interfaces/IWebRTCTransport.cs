// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// WebRTC Transport Interface - Abstraction for peer connection management

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PaLX.Client.Services.Interfaces
{
    /// <summary>
    /// WebRTC connection state
    /// </summary>
    public enum WebRTCConnectionState
    {
        New,
        Connecting,
        Connected,
        Disconnected,
        Failed,
        Closed
    }

    /// <summary>
    /// ICE connection state
    /// </summary>
    public enum IceConnectionState
    {
        New,
        Checking,
        Connected,
        Completed,
        Failed,
        Disconnected,
        Closed
    }

    /// <summary>
    /// ICE server configuration
    /// </summary>
    public class IceServerConfig
    {
        /// <summary>
        /// Server URLs (stun:host:port or turn:host:port)
        /// </summary>
        public List<string> Urls { get; set; } = new();

        /// <summary>
        /// Username for TURN authentication (optional for STUN)
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Credential for TURN authentication (optional for STUN)
        /// </summary>
        public string? Credential { get; set; }

        /// <summary>
        /// Credential type (typically "password")
        /// </summary>
        public string CredentialType { get; set; } = "password";
    }

    /// <summary>
    /// WebRTC transport configuration
    /// </summary>
    public class WebRTCConfig
    {
        /// <summary>
        /// List of ICE servers (STUN/TURN)
        /// </summary>
        public List<IceServerConfig> IceServers { get; set; } = new();

        /// <summary>
        /// Enable DTLS-SRTP encryption (should always be true)
        /// </summary>
        public bool EnableDtlsSrtp { get; set; } = true;

        /// <summary>
        /// Enable RTCP feedback for quality adaptation
        /// </summary>
        public bool EnableRtcpFeedback { get; set; } = true;

        /// <summary>
        /// ICE candidate policy
        /// </summary>
        public string IceCandidatePolicy { get; set; } = "all";
    }

    /// <summary>
    /// SDP (Session Description Protocol) data
    /// </summary>
    public class SdpMessage
    {
        public string Type { get; set; } = "offer"; // "offer" or "answer"
        public string Sdp { get; set; } = string.Empty;
    }

    /// <summary>
    /// ICE candidate data
    /// </summary>
    public class IceCandidateMessage
    {
        public string Candidate { get; set; } = string.Empty;
        public string? SdpMid { get; set; }
        public int SdpMLineIndex { get; set; }
    }

    /// <summary>
    /// Interface for WebRTC peer connection transport
    /// </summary>
    public interface IWebRTCTransport : IDisposable
    {
        /// <summary>
        /// Current connection state
        /// </summary>
        WebRTCConnectionState ConnectionState { get; }

        /// <summary>
        /// Current ICE connection state
        /// </summary>
        IceConnectionState IceState { get; }

        /// <summary>
        /// Initialize the peer connection with configuration
        /// </summary>
        Task InitializeAsync(WebRTCConfig config);

        /// <summary>
        /// Create an SDP offer (caller side)
        /// </summary>
        Task<SdpMessage> CreateOfferAsync();

        /// <summary>
        /// Create an SDP answer (callee side)
        /// </summary>
        Task<SdpMessage> CreateAnswerAsync();

        /// <summary>
        /// Set the local SDP description
        /// </summary>
        Task SetLocalDescriptionAsync(SdpMessage sdp);

        /// <summary>
        /// Set the remote SDP description
        /// </summary>
        Task SetRemoteDescriptionAsync(SdpMessage sdp);

        /// <summary>
        /// Add an ICE candidate from remote peer
        /// </summary>
        Task AddIceCandidateAsync(IceCandidateMessage candidate);

        /// <summary>
        /// Send encoded video frame
        /// </summary>
        void SendVideo(uint timestamp, byte[] encodedFrame);

        /// <summary>
        /// Send encoded audio data
        /// </summary>
        void SendAudio(uint timestamp, byte[] encodedData);

        /// <summary>
        /// Close the connection
        /// </summary>
        void Close();

        // Events
        
        /// <summary>
        /// Fired when a new ICE candidate is discovered
        /// </summary>
        event Action<IceCandidateMessage>? OnIceCandidate;

        /// <summary>
        /// Fired when connection state changes
        /// </summary>
        event Action<WebRTCConnectionState>? OnConnectionStateChanged;

        /// <summary>
        /// Fired when ICE state changes
        /// </summary>
        event Action<IceConnectionState>? OnIceStateChanged;

        /// <summary>
        /// Fired when video frame is received from remote peer
        /// </summary>
        event Action<uint, byte[]>? OnVideoReceived;

        /// <summary>
        /// Fired when audio data is received from remote peer
        /// </summary>
        event Action<uint, byte[]>? OnAudioReceived;

        /// <summary>
        /// Fired on error
        /// </summary>
        event Action<string>? OnError;
    }
}

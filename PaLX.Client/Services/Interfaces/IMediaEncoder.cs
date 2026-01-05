// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// Media Encoder Interface - Abstraction for video/audio encoding

using System;
using System.Collections.Generic;

namespace PaLX.Client.Services.Interfaces
{
    /// <summary>
    /// Supported audio codecs
    /// </summary>
    public enum AudioCodec
    {
        /// <summary>
        /// Opus - High quality, low latency, adaptive bitrate (recommended)
        /// </summary>
        Opus,

        /// <summary>
        /// G.711 μ-law - Legacy compatibility, fixed 64kbps
        /// </summary>
        PCMU,

        /// <summary>
        /// G.711 A-law - European variant of G.711
        /// </summary>
        PCMA
    }

    /// <summary>
    /// Supported video codecs
    /// </summary>
    public enum VideoCodec
    {
        /// <summary>
        /// VP8 - Open, royalty-free, WebRTC standard
        /// </summary>
        VP8,

        /// <summary>
        /// VP9 - Better compression than VP8
        /// </summary>
        VP9,

        /// <summary>
        /// H.264 - Wide hardware support
        /// </summary>
        H264
    }

    /// <summary>
    /// Encoded frame data
    /// </summary>
    public class EncodedFrame
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public uint Timestamp { get; set; }
        public bool IsKeyFrame { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// Decoded frame data
    /// </summary>
    public class DecodedFrame
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// Interface for audio encoding/decoding
    /// </summary>
    public interface IPaLXAudioEncoder : IDisposable
    {
        /// <summary>
        /// The codec used by this encoder
        /// </summary>
        AudioCodec Codec { get; }

        /// <summary>
        /// Target bitrate in kbps (for adaptive codecs like Opus)
        /// </summary>
        int TargetBitrate { get; set; }

        /// <summary>
        /// Sample rate expected by the encoder
        /// </summary>
        int SampleRate { get; }

        /// <summary>
        /// Number of channels expected
        /// </summary>
        int Channels { get; }

        /// <summary>
        /// Encode PCM audio samples to compressed format
        /// </summary>
        byte[] Encode(byte[] pcmSamples, int length);

        /// <summary>
        /// Decode compressed audio to PCM samples
        /// </summary>
        byte[] Decode(byte[] encodedData);

        /// <summary>
        /// RTP payload type for this codec
        /// </summary>
        int RtpPayloadType { get; }
    }

    /// <summary>
    /// Interface for video encoding/decoding
    /// </summary>
    public interface IPaLXVideoEncoder : IDisposable
    {
        /// <summary>
        /// The codec used by this encoder
        /// </summary>
        VideoCodec Codec { get; }

        /// <summary>
        /// Target bitrate in kbps
        /// </summary>
        int TargetBitrate { get; set; }

        /// <summary>
        /// Target frames per second
        /// </summary>
        int TargetFps { get; set; }

        /// <summary>
        /// Encode a raw video frame
        /// </summary>
        EncodedFrame? Encode(byte[] rawFrame, int width, int height);

        /// <summary>
        /// Decode an encoded video frame
        /// </summary>
        DecodedFrame? Decode(byte[] encodedFrame);

        /// <summary>
        /// Request a keyframe on next encode
        /// </summary>
        void RequestKeyFrame();

        /// <summary>
        /// RTP payload type for this codec
        /// </summary>
        int RtpPayloadType { get; }
    }

    /// <summary>
    /// Factory for creating encoders
    /// </summary>
    public interface IEncoderFactory
    {
        /// <summary>
        /// Create an audio encoder for the specified codec
        /// </summary>
        IPaLXAudioEncoder CreateAudioEncoder(AudioCodec codec);

        /// <summary>
        /// Create a video encoder for the specified codec
        /// </summary>
        IPaLXVideoEncoder CreateVideoEncoder(VideoCodec codec);

        /// <summary>
        /// Get supported audio codecs
        /// </summary>
        IEnumerable<AudioCodec> SupportedAudioCodecs { get; }

        /// <summary>
        /// Get supported video codecs
        /// </summary>
        IEnumerable<VideoCodec> SupportedVideoCodecs { get; }
    }
}

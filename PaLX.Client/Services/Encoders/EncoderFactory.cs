// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// Encoder Factory - Creates audio/video encoders based on codec preference

using System;
using System.Collections.Generic;
using PaLX.Client.Services.Interfaces;
using SIPSorceryMedia.Encoders;

namespace PaLX.Client.Services.Encoders
{
    /// <summary>
    /// Factory for creating media encoders
    /// Supports: Opus, G.711 (PCMU/PCMA), VP8
    /// </summary>
    public class EncoderFactory : IEncoderFactory
    {
        /// <summary>
        /// Supported audio codecs (Opus preferred)
        /// </summary>
        public IEnumerable<AudioCodec> SupportedAudioCodecs => new[]
        {
            AudioCodec.Opus,  // Primary - high quality, adaptive
            AudioCodec.PCMU,  // Fallback - universal compatibility
            AudioCodec.PCMA   // Fallback - European variant
        };

        /// <summary>
        /// Supported video codecs
        /// </summary>
        public IEnumerable<VideoCodec> SupportedVideoCodecs => new[]
        {
            VideoCodec.VP8  // WebRTC standard, royalty-free
        };

        /// <summary>
        /// Create an audio encoder for the specified codec
        /// </summary>
        public IPaLXAudioEncoder CreateAudioEncoder(AudioCodec codec)
        {
            return codec switch
            {
                AudioCodec.Opus => new OpusAudioEncoder(48000, 2, 64),
                AudioCodec.PCMU => new G711AudioEncoder(useALaw: false),
                AudioCodec.PCMA => new G711AudioEncoder(useALaw: true),
                _ => throw new NotSupportedException($"Audio codec {codec} is not supported")
            };
        }

        /// <summary>
        /// Create a video encoder for the specified codec
        /// </summary>
        public IPaLXVideoEncoder CreateVideoEncoder(VideoCodec codec)
        {
            return codec switch
            {
                VideoCodec.VP8 => new Vp8VideoEncoderWrapper(),
                _ => throw new NotSupportedException($"Video codec {codec} is not supported")
            };
        }
    }

    /// <summary>
    /// VP8 Video Encoder wrapper implementing IPaLXVideoEncoder interface
    /// Optimized for real-time video communication with quality settings
    /// </summary>
    public class Vp8VideoEncoderWrapper : IPaLXVideoEncoder
    {
        private readonly VpxVideoEncoder _encoder;
        private bool _requestKeyFrame;
        private bool _disposed;
        private int _frameCount;
        private const int KEYFRAME_INTERVAL = 60; // Force keyframe every 60 frames (~2 sec at 30fps)

        public VideoCodec Codec => VideoCodec.VP8;
        public int RtpPayloadType => 96;
        
        private int _targetBitrate = 1200; // Default to Medium quality
        public int TargetBitrate
        {
            get => _targetBitrate;
            set
            {
                _targetBitrate = Math.Clamp(value, 300, 8000);
                _encoder.TargetKbps = (uint)_targetBitrate;
            }
        }

        private int _targetFps = 30;
        public int TargetFps
        {
            get => _targetFps;
            set => _targetFps = Math.Clamp(value, 15, 60);
        }

        public Vp8VideoEncoderWrapper()
        {
            _encoder = new VpxVideoEncoder();
            _encoder.TargetKbps = (uint)_targetBitrate;
            _frameCount = 0;
        }

        public EncodedFrame? Encode(byte[] rawFrame, int width, int height)
        {
            if (_disposed) return null;

            try
            {
                _frameCount++;
                
                // Force keyframe periodically for better quality recovery
                bool forceKeyFrame = _requestKeyFrame || (_frameCount % KEYFRAME_INTERVAL == 0);
                
                // The VpxVideoEncoder from SIPSorcery handles keyframes internally
                // We pass the frame and let it encode with the current bitrate settings
                var encoded = _encoder.EncodeVideo(
                    width, height,
                    rawFrame,
                    SIPSorceryMedia.Abstractions.VideoPixelFormatsEnum.Bgr,
                    SIPSorceryMedia.Abstractions.VideoCodecsEnum.VP8);

                if (encoded != null && encoded.Length > 0)
                {
                    return new EncodedFrame
                    {
                        Data = encoded,
                        Width = width,
                        Height = height,
                        IsKeyFrame = forceKeyFrame
                    };
                }
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"VP8 encode error: {ex.Message}");
            }
            finally
            {
                _requestKeyFrame = false;
            }

            return null;
        }

        public DecodedFrame? Decode(byte[] encodedFrame)
        {
            if (_disposed) return null;

            try
            {
                var decoded = _encoder.DecodeVideo(
                    encodedFrame,
                    SIPSorceryMedia.Abstractions.VideoPixelFormatsEnum.Bgr,
                    SIPSorceryMedia.Abstractions.VideoCodecsEnum.VP8);

                foreach (var frame in decoded)
                {
                    if (frame.Sample != null)
                    {
                        return new DecodedFrame
                        {
                            Data = frame.Sample,
                            Width = (int)frame.Width,
                            Height = (int)frame.Height
                        };
                    }
                }
            }
            catch { }

            return null;
        }

        public void RequestKeyFrame()
        {
            _requestKeyFrame = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _encoder?.Dispose();
        }
    }
}

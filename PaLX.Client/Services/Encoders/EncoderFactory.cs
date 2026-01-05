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
    /// </summary>
    public class Vp8VideoEncoderWrapper : IPaLXVideoEncoder
    {
        private readonly VpxVideoEncoder _encoder;
        private bool _requestKeyFrame;
        private bool _disposed;

        public VideoCodec Codec => VideoCodec.VP8;
        public int RtpPayloadType => 96;
        
        private int _targetBitrate = 500;
        public int TargetBitrate
        {
            get => _targetBitrate;
            set
            {
                _targetBitrate = Math.Clamp(value, 100, 5000);
                _encoder.TargetKbps = (uint)_targetBitrate;
            }
        }

        private int _targetFps = 30;
        public int TargetFps
        {
            get => _targetFps;
            set => _targetFps = Math.Clamp(value, 10, 60);
        }

        public Vp8VideoEncoderWrapper()
        {
            _encoder = new VpxVideoEncoder();
            _encoder.TargetKbps = (uint)_targetBitrate;
        }

        public EncodedFrame? Encode(byte[] rawFrame, int width, int height)
        {
            if (_disposed) return null;

            try
            {
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
                        IsKeyFrame = _requestKeyFrame
                    };
                }
            }
            catch { }
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

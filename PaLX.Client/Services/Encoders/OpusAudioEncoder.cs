// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// Opus Audio Encoder - High quality, low latency audio codec using Concentus

using System;
using Concentus;
using Concentus.Enums;
using PaLX.Client.Services.Interfaces;

namespace PaLX.Client.Services.Encoders
{
    /// <summary>
    /// Opus audio encoder/decoder using Concentus (pure managed .NET implementation)
    /// 
    /// Features:
    /// - Adaptive bitrate (6-510 kbps)
    /// - Ultra-low latency (~2.5ms)
    /// - Built-in FEC (Forward Error Correction)
    /// - Excellent quality even on poor networks
    /// - WebRTC native codec
    /// </summary>
    public class OpusAudioEncoder : IPaLXAudioEncoder
    {
        private readonly IOpusEncoder _encoder;
        private readonly IOpusDecoder _decoder;
        
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _frameSize;
        private int _targetBitrate;
        
        private readonly byte[] _encodeBuffer;
        private readonly short[] _decodeBuffer;
        
        private bool _disposed;

        /// <summary>
        /// RTP payload type for Opus (dynamic, typically 111)
        /// </summary>
        public int RtpPayloadType => 111;

        /// <summary>
        /// Sample rate (48000 Hz for WebRTC Opus)
        /// </summary>
        public int SampleRate => _sampleRate;

        /// <summary>
        /// Number of channels (2 for stereo WebRTC)
        /// </summary>
        public int Channels => _channels;

        /// <summary>
        /// Audio codec type
        /// </summary>
        public AudioCodec Codec => AudioCodec.Opus;

        /// <summary>
        /// Target bitrate in kbps (6-510)
        /// </summary>
        public int TargetBitrate
        {
            get => _targetBitrate;
            set
            {
                _targetBitrate = Math.Clamp(value, 6, 510);
                _encoder.Bitrate = _targetBitrate * 1000;
            }
        }

        /// <summary>
        /// Create a new Opus encoder with WebRTC-compatible settings
        /// </summary>
        /// <param name="sampleRate">Sample rate (default: 48000 Hz)</param>
        /// <param name="channels">Channels (default: 2 for stereo)</param>
        /// <param name="targetBitrateKbps">Target bitrate in kbps (default: 64)</param>
        public OpusAudioEncoder(int sampleRate = 48000, int channels = 2, int targetBitrateKbps = 64)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _targetBitrate = Math.Clamp(targetBitrateKbps, 6, 510);
            
            // Frame size: 20ms at 48kHz = 960 samples
            _frameSize = _sampleRate * 20 / 1000;
            
            // Create encoder with VOIP application (optimized for speech)
            _encoder = OpusCodecFactory.CreateEncoder(_sampleRate, _channels, OpusApplication.OPUS_APPLICATION_VOIP);
            _encoder.Bitrate = _targetBitrate * 1000;
            _encoder.Complexity = 5; // Balance between quality and CPU
            _encoder.UseInbandFEC = true; // Enable Forward Error Correction
            _encoder.PacketLossPercent = 10; // Expect some packet loss
            _encoder.UseVBR = true; // Variable bitrate for efficiency
            _encoder.UseDTX = false; // Disable discontinuous transmission for better quality
            
            // Create decoder
            _decoder = OpusCodecFactory.CreateDecoder(_sampleRate, _channels);
            
            // Buffers
            _encodeBuffer = new byte[4000]; // Max Opus frame is ~4000 bytes
            _decodeBuffer = new short[_frameSize * _channels * 2]; // Decoded samples buffer
        }

        /// <summary>
        /// Encode PCM audio samples to Opus format
        /// </summary>
        /// <param name="pcmSamples">PCM samples (16-bit signed, little-endian)</param>
        /// <param name="length">Number of bytes</param>
        /// <returns>Opus encoded data</returns>
        public byte[] Encode(byte[] pcmSamples, int length)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OpusAudioEncoder));
            if (pcmSamples == null || length == 0) return Array.Empty<byte>();

            try
            {
                // Convert bytes to shorts (PCM 16-bit)
                int sampleCount = length / 2;
                var samples = new short[sampleCount];
                Buffer.BlockCopy(pcmSamples, 0, samples, 0, length);

                // Encode using Span API (Concentus 2.x)
                ReadOnlySpan<short> inputSpan = samples.AsSpan(0, Math.Min(_frameSize * _channels, sampleCount));
                Span<byte> outputSpan = _encodeBuffer.AsSpan();
                
                int encodedLength = _encoder.Encode(inputSpan, _frameSize, outputSpan, _encodeBuffer.Length);
                
                if (encodedLength > 0)
                {
                    var result = new byte[encodedLength];
                    Buffer.BlockCopy(_encodeBuffer, 0, result, 0, encodedLength);
                    return result;
                }
            }
            catch (Exception)
            {
                // Encoding failed, return empty
            }

            return Array.Empty<byte>();
        }

        /// <summary>
        /// Decode Opus data to PCM samples
        /// </summary>
        /// <param name="encodedData">Opus encoded data</param>
        /// <returns>PCM samples (16-bit signed, little-endian)</returns>
        public byte[] Decode(byte[] encodedData)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OpusAudioEncoder));
            if (encodedData == null || encodedData.Length == 0) return Array.Empty<byte>();

            try
            {
                // Decode using Span API (Concentus 2.x)
                ReadOnlySpan<byte> inputSpan = encodedData.AsSpan();
                Span<short> outputSpan = _decodeBuffer.AsSpan();
                
                int decodedSamples = _decoder.Decode(inputSpan, outputSpan, _frameSize, false);
                
                if (decodedSamples > 0)
                {
                    // Convert shorts to bytes
                    int byteLength = decodedSamples * _channels * 2;
                    var result = new byte[byteLength];
                    Buffer.BlockCopy(_decodeBuffer, 0, result, 0, byteLength);
                    return result;
                }
            }
            catch (Exception)
            {
                // Decoding failed, try packet loss concealment
                try
                {
                    Span<short> outputSpan = _decodeBuffer.AsSpan();
                    int decodedSamples = _decoder.Decode(ReadOnlySpan<byte>.Empty, outputSpan, _frameSize, true);
                    if (decodedSamples > 0)
                    {
                        int byteLength = decodedSamples * _channels * 2;
                        var result = new byte[byteLength];
                        Buffer.BlockCopy(_decodeBuffer, 0, result, 0, byteLength);
                        return result;
                    }
                }
                catch { }
            }

            return Array.Empty<byte>();
        }

        /// <summary>
        /// Handle packet loss by generating concealment audio
        /// </summary>
        /// <returns>Concealed PCM samples</returns>
        public byte[] GenerateLossConcealment()
        {
            if (_disposed) return Array.Empty<byte>();

            try
            {
                // Use FEC (Forward Error Correction) for packet loss concealment
                Span<short> outputSpan = _decodeBuffer.AsSpan();
                int decodedSamples = _decoder.Decode(ReadOnlySpan<byte>.Empty, outputSpan, _frameSize, true);
                if (decodedSamples > 0)
                {
                    int byteLength = decodedSamples * _channels * 2;
                    var result = new byte[byteLength];
                    Buffer.BlockCopy(_decodeBuffer, 0, result, 0, byteLength);
                    return result;
                }
            }
            catch { }

            return Array.Empty<byte>();
        }

        /// <summary>
        /// Set encoder bandwidth (narrow, medium, wide, super-wide, full)
        /// </summary>
        public void SetBandwidth(OpusBandwidth bandwidth)
        {
            if (!_disposed)
            {
                _encoder.Bandwidth = bandwidth;
            }
        }

        /// <summary>
        /// Set expected packet loss percentage for FEC optimization
        /// </summary>
        public void SetPacketLossPercent(int percent)
        {
            if (!_disposed)
            {
                _encoder.PacketLossPercent = Math.Clamp(percent, 0, 100);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            // Concentus encoders don't implement IDisposable but we mark as disposed
            // to prevent further use
        }
    }
}

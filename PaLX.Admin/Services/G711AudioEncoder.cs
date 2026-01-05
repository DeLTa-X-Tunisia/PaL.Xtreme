// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// G.711 Audio Encoder - Legacy compatibility codec (μ-law/A-law)

using System;
using PaLX.Client.Services.Interfaces;

namespace PaLX.Client.Services.Encoders
{
    /// <summary>
    /// G.711 μ-law (PCMU) audio encoder/decoder
    /// 
    /// Features:
    /// - Universal compatibility
    /// - Fixed 64 kbps bitrate
    /// - 8000 Hz sample rate
    /// - Simple, low CPU usage
    /// - Fallback codec when Opus not supported
    /// </summary>
    public class G711AudioEncoder : IPaLXAudioEncoder
    {
        private readonly AudioCodec _codec;
        private bool _disposed;

        /// <summary>
        /// RTP payload type (0 for PCMU, 8 for PCMA)
        /// </summary>
        public int RtpPayloadType => _codec == AudioCodec.PCMU ? 0 : 8;

        /// <summary>
        /// Sample rate (8000 Hz for G.711)
        /// </summary>
        public int SampleRate => 8000;

        /// <summary>
        /// Channels (1 for mono)
        /// </summary>
        public int Channels => 1;

        /// <summary>
        /// Audio codec type
        /// </summary>
        public AudioCodec Codec => _codec;

        /// <summary>
        /// Target bitrate (fixed at 64 kbps for G.711)
        /// </summary>
        public int TargetBitrate
        {
            get => 64;
            set { } // G.711 has fixed bitrate
        }

        /// <summary>
        /// Create G.711 encoder
        /// </summary>
        /// <param name="useALaw">Use A-law (PCMA) instead of μ-law (PCMU)</param>
        public G711AudioEncoder(bool useALaw = false)
        {
            _codec = useALaw ? AudioCodec.PCMA : AudioCodec.PCMU;
        }

        /// <summary>
        /// Encode PCM to G.711
        /// </summary>
        public byte[] Encode(byte[] pcmSamples, int length)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(G711AudioEncoder));
            if (pcmSamples == null || length == 0) return Array.Empty<byte>();

            // G.711 compresses 16-bit samples to 8-bit
            var encoded = new byte[length / 2];
            
            for (int i = 0, j = 0; i < length && j < encoded.Length; i += 2, j++)
            {
                short sample = BitConverter.ToInt16(pcmSamples, i);
                encoded[j] = _codec == AudioCodec.PCMU 
                    ? LinearToMuLaw(sample) 
                    : LinearToALaw(sample);
            }

            return encoded;
        }

        /// <summary>
        /// Decode G.711 to PCM
        /// </summary>
        public byte[] Decode(byte[] encodedData)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(G711AudioEncoder));
            if (encodedData == null || encodedData.Length == 0) return Array.Empty<byte>();

            // G.711 expands 8-bit samples to 16-bit
            var decoded = new byte[encodedData.Length * 2];
            
            for (int i = 0; i < encodedData.Length; i++)
            {
                short sample = _codec == AudioCodec.PCMU 
                    ? MuLawToLinear(encodedData[i]) 
                    : ALawToLinear(encodedData[i]);
                
                decoded[i * 2] = (byte)(sample & 0xFF);
                decoded[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return decoded;
        }

        #region μ-law Conversion

        private static byte LinearToMuLaw(short sample)
        {
            const int MULAW_MAX = 0x1FFF;
            const int MULAW_BIAS = 33;
            
            int sign = (sample >> 8) & 0x80;
            if (sign != 0) sample = (short)-sample;
            if (sample > MULAW_MAX) sample = MULAW_MAX;
            
            sample += MULAW_BIAS;
            int exponent = 7;
            for (int mask = 0x4000; (sample & mask) == 0 && exponent > 0; exponent--, mask >>= 1) { }
            
            int mantissa = (sample >> (exponent + 3)) & 0x0F;
            byte muLawByte = (byte)(sign | (exponent << 4) | mantissa);
            return (byte)~muLawByte;
        }

        private static short MuLawToLinear(byte muLawByte)
        {
            muLawByte = (byte)~muLawByte;
            int sign = muLawByte & 0x80;
            int exponent = (muLawByte >> 4) & 0x07;
            int mantissa = muLawByte & 0x0F;
            
            int sample = ((mantissa << 3) + 0x84) << exponent;
            sample -= 0x84;
            
            return (short)(sign != 0 ? -sample : sample);
        }

        #endregion

        #region A-law Conversion

        private static byte LinearToALaw(short sample)
        {
            int sign = (sample >> 8) & 0x80;
            if (sign != 0) sample = (short)-sample;
            
            if (sample > 32635) sample = 32635;
            
            int exponent;
            int mantissa;
            
            if (sample < 256)
            {
                exponent = 0;
                mantissa = sample >> 4;
            }
            else
            {
                exponent = 1;
                while (sample >= 512)
                {
                    sample >>= 1;
                    exponent++;
                }
                mantissa = (sample >> 4) & 0x0F;
            }
            
            byte aLawByte = (byte)(sign | (exponent << 4) | mantissa);
            return (byte)(aLawByte ^ 0x55);
        }

        private static short ALawToLinear(byte aLawByte)
        {
            aLawByte ^= 0x55;
            int sign = aLawByte & 0x80;
            int exponent = (aLawByte >> 4) & 0x07;
            int mantissa = aLawByte & 0x0F;
            
            int sample;
            if (exponent == 0)
            {
                sample = (mantissa << 4) + 8;
            }
            else
            {
                sample = ((mantissa << 4) + 0x108) << (exponent - 1);
            }
            
            return (short)(sign != 0 ? -sample : sample);
        }

        #endregion

        public void Dispose()
        {
            _disposed = true;
        }
    }
}

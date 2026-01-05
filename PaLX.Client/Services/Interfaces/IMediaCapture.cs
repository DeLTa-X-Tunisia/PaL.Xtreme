// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// Media Capture Interface - Abstraction for video/audio capture

using System;
using System.Windows.Media.Imaging;

namespace PaLX.Client.Services.Interfaces
{
    /// <summary>
    /// Interface for media capture devices (camera, microphone)
    /// Provides abstraction layer for different capture implementations
    /// </summary>
    public interface IMediaCapture : IDisposable
    {
        /// <summary>
        /// Whether the capture device is currently running
        /// </summary>
        bool IsCapturing { get; }

        /// <summary>
        /// Start capturing from the device
        /// </summary>
        void StartCapture();

        /// <summary>
        /// Stop capturing from the device
        /// </summary>
        void StopCapture();

        /// <summary>
        /// Enable or disable the capture
        /// </summary>
        bool IsEnabled { get; set; }
    }

    /// <summary>
    /// Interface for video capture (camera)
    /// </summary>
    public interface IVideoCapture : IMediaCapture
    {
        /// <summary>
        /// Video resolution width
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Video resolution height
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Target frames per second
        /// </summary>
        int TargetFps { get; set; }

        /// <summary>
        /// Event fired when a new video frame is captured
        /// </summary>
        event Action<byte[], int, int>? OnFrameCaptured;

        /// <summary>
        /// Event fired with preview bitmap for local display
        /// </summary>
        event Action<BitmapSource>? OnPreviewFrame;
    }

    /// <summary>
    /// Interface for audio capture (microphone)
    /// </summary>
    public interface IAudioCapture : IMediaCapture
    {
        /// <summary>
        /// Audio sample rate in Hz
        /// </summary>
        int SampleRate { get; }

        /// <summary>
        /// Number of audio channels (1 = mono, 2 = stereo)
        /// </summary>
        int Channels { get; }

        /// <summary>
        /// Bits per sample (typically 16)
        /// </summary>
        int BitsPerSample { get; }

        /// <summary>
        /// Event fired when audio samples are captured
        /// </summary>
        event Action<byte[], int>? OnAudioCaptured;
    }

    /// <summary>
    /// Interface for audio playback (speaker)
    /// </summary>
    public interface IAudioPlayback : IDisposable
    {
        /// <summary>
        /// Whether playback is active
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Start audio playback
        /// </summary>
        void StartPlayback();

        /// <summary>
        /// Stop audio playback
        /// </summary>
        void StopPlayback();

        /// <summary>
        /// Add audio samples to the playback buffer
        /// </summary>
        void AddSamples(byte[] samples, int count);

        /// <summary>
        /// Playback volume (0.0 to 1.0)
        /// </summary>
        float Volume { get; set; }
    }
}

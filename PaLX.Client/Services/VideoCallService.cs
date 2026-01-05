// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// Video Call Service - Real-time video calling with OpenCvSharp + NAudio

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.AspNetCore.SignalR.Client;
using NAudio.Wave;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace PaLX.Client.Services
{
    /// <summary>
    /// Service for managing video calls
    /// Uses OpenCvSharp for camera capture and NAudio for audio
    /// </summary>
    public class VideoCallService : IDisposable
    {
        private readonly HubConnection _hubConnection;
        
        // Camera
        private VideoCapture? _camera;
        private Thread? _cameraThread;
        private volatile bool _isCameraRunning;
        
        // Audio Capture (Microphone)
        private WaveInEvent? _waveIn;
        private BufferedWaveProvider? _audioBuffer;
        
        // Audio Playback (Speaker)
        private WaveOutEvent? _waveOut;
        private BufferedWaveProvider? _playbackBuffer;
        
        // Call State
        private bool _isCallActive = false;
        private bool _isVideoEnabled = true;
        private bool _isAudioEnabled = true;
        private string _currentCallId = string.Empty;
        private string _currentPartner = string.Empty;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

        // Events
        public event Action<string, string>? OnIncomingVideoCall;      // (caller, callId)
        public event Action<string, string>? OnVideoCallAccepted;      // (callee, callId)
        public event Action<string, string>? OnVideoCallDeclined;      // (callee, callId)
        public event Action<string, string>? OnVideoCallEnded;         // (partner, callId)
        public event Action<string>? OnStatusChanged;                  // status message
        public event Action<BitmapSource?>? OnLocalVideoFrame;         // local preview
        public event Action<BitmapSource?>? OnRemoteVideoFrame;        // remote video
        public event Action<bool>? OnPartnerVideoToggled;              // partner video on/off
        public event Action<bool>? OnPartnerAudioToggled;              // partner audio on/off
        public event Action<string>? OnError;                          // error message

        public bool IsCallActive => _isCallActive;
        public bool IsVideoEnabled => _isVideoEnabled;
        public bool IsAudioEnabled => _isAudioEnabled;
        public string CurrentCallId => _currentCallId;
        public string CurrentPartner => _currentPartner;

        public VideoCallService(HubConnection hubConnection)
        {
            _hubConnection = hubConnection;
            InitializeSignalR();
        }

        private void InitializeSignalR()
        {
            // Incoming video call request
            _hubConnection.On<string, string>("IncomingVideoCall", (sender, callId) =>
            {
                if (_isCallActive)
                {
                    // Already in a call, decline
                    _hubConnection.SendAsync("DeclineVideoCall", sender, callId);
                    return;
                }
                OnIncomingVideoCall?.Invoke(sender, callId);
            });

            // Video call accepted
            _hubConnection.On<string, string>("VideoCallAccepted", async (callee, callId) =>
            {
                _currentPartner = callee;
                _currentCallId = callId;
                _isCallActive = true;
                OnVideoCallAccepted?.Invoke(callee, callId);
                OnStatusChanged?.Invoke("Connecté");
                
                // Start media capture
                await StartMediaCaptureAsync();
            });

            // Video call declined
            _hubConnection.On<string, string>("VideoCallDeclined", (callee, callId) =>
            {
                OnVideoCallDeclined?.Invoke(callee, callId);
                OnStatusChanged?.Invoke("Appel refusé");
                CleanupCall();
            });

            // Video call ended
            _hubConnection.On<string, string>("VideoCallEnded", (partner, callId) =>
            {
                OnVideoCallEnded?.Invoke(partner, callId);
                OnStatusChanged?.Invoke("Appel terminé");
                CleanupCall();
            });

            // Receive video frame from partner
            _hubConnection.On<string, string, byte[]>("ReceiveVideoFrame", (sender, callId, frameData) =>
            {
                if (_currentCallId != callId || !_isCallActive) return;
                
                try
                {
                    // Decode JPEG frame
                    using var mat = Cv2.ImDecode(frameData, ImreadModes.Color);
                    if (mat != null && !mat.Empty())
                    {
                        var bitmap = mat.ToBitmapSource();
                        bitmap.Freeze();
                        OnRemoteVideoFrame?.Invoke(bitmap);
                    }
                }
                catch { /* Frame decode error */ }
            });

            // Receive audio data from partner
            _hubConnection.On<string, string, byte[]>("ReceiveAudioData", (sender, callId, audioData) =>
            {
                if (_currentCallId != callId || !_isCallActive) return;
                
                try
                {
                    // Add audio to playback buffer
                    _playbackBuffer?.AddSamples(audioData, 0, audioData.Length);
                }
                catch { /* Audio playback error */ }
            });

            // Partner toggled video
            _hubConnection.On<string, string, bool>("PartnerVideoToggled", (sender, callId, isEnabled) =>
            {
                if (_currentCallId == callId)
                {
                    OnPartnerVideoToggled?.Invoke(isEnabled);
                    if (!isEnabled)
                    {
                        OnRemoteVideoFrame?.Invoke(null);
                    }
                }
            });

            // Partner toggled audio
            _hubConnection.On<string, string, bool>("PartnerAudioToggled", (sender, callId, isEnabled) =>
            {
                if (_currentCallId == callId)
                {
                    OnPartnerAudioToggled?.Invoke(isEnabled);
                }
            });
        }

        /// <summary>
        /// Request a video call to another user
        /// </summary>
        public async Task RequestVideoCall(string receiver)
        {
            if (_isCallActive)
            {
                OnError?.Invoke("Un appel est déjà en cours");
                return;
            }

            _currentCallId = Guid.NewGuid().ToString();
            _currentPartner = receiver;
            OnStatusChanged?.Invoke($"Appel en cours...");
            
            await _hubConnection.SendAsync("RequestVideoCall", receiver, _currentCallId);
        }

        /// <summary>
        /// Accept an incoming video call
        /// </summary>
        public async Task AcceptVideoCall(string caller, string callId)
        {
            _currentPartner = caller;
            _currentCallId = callId;
            _isCallActive = true;
            
            OnStatusChanged?.Invoke("Connexion...");
            await _hubConnection.SendAsync("AcceptVideoCall", caller, callId);
            
            // Start media capture
            await StartMediaCaptureAsync();
        }

        /// <summary>
        /// Decline an incoming video call
        /// </summary>
        public async Task DeclineVideoCall(string caller, string callId)
        {
            await _hubConnection.SendAsync("DeclineVideoCall", caller, callId);
            OnStatusChanged?.Invoke("Appel refusé");
        }

        /// <summary>
        /// End the current video call
        /// </summary>
        public async Task EndVideoCall()
        {
            if (!_isCallActive || string.IsNullOrEmpty(_currentPartner)) return;
            
            await _hubConnection.SendAsync("EndVideoCall", _currentPartner, _currentCallId);
            OnStatusChanged?.Invoke("Appel terminé");
            CleanupCall();
        }

        /// <summary>
        /// Toggle local video on/off
        /// </summary>
        public async Task ToggleVideo()
        {
            _isVideoEnabled = !_isVideoEnabled;

            if (!string.IsNullOrEmpty(_currentPartner))
            {
                await _hubConnection.SendAsync("ToggleVideoStream", _currentPartner, _currentCallId, _isVideoEnabled);
            }
            
            if (!_isVideoEnabled)
            {
                OnLocalVideoFrame?.Invoke(null);
            }
            
            OnStatusChanged?.Invoke(_isVideoEnabled ? "Caméra activée" : "Caméra désactivée");
        }

        /// <summary>
        /// Toggle local audio on/off
        /// </summary>
        public async Task ToggleAudio()
        {
            _isAudioEnabled = !_isAudioEnabled;

            if (!string.IsNullOrEmpty(_currentPartner))
            {
                await _hubConnection.SendAsync("ToggleVideoAudio", _currentPartner, _currentCallId, _isAudioEnabled);
            }
            
            OnStatusChanged?.Invoke(_isAudioEnabled ? "Micro activé" : "Micro désactivé");
        }

        private async Task StartMediaCaptureAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                // Initialize camera
                StartCameraCapture();
                
                // Initialize audio
                StartAudioCapture();
                StartAudioPlayback();
                
                OnStatusChanged?.Invoke("Connecté");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur média: {ex.Message}");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void StartCameraCapture()
        {
            try
            {
                _camera = new VideoCapture(0);
                
                if (!_camera.IsOpened())
                {
                    OnError?.Invoke("Impossible d'accéder à la caméra");
                    return;
                }

                // Set camera properties
                _camera.Set(VideoCaptureProperties.FrameWidth, 640);
                _camera.Set(VideoCaptureProperties.FrameHeight, 480);
                _camera.Set(VideoCaptureProperties.Fps, 30);

                _isCameraRunning = true;
                _cameraThread = new Thread(CameraCaptureLoop)
                {
                    IsBackground = true,
                    Name = "VideoCallCameraThread"
                };
                _cameraThread.Start();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur caméra: {ex.Message}");
            }
        }

        private void CameraCaptureLoop()
        {
            using var frame = new Mat();
            var lastSendTime = DateTime.Now;
            var sendInterval = TimeSpan.FromMilliseconds(50); // ~20 FPS for network

            while (_isCameraRunning && _camera != null && _camera.IsOpened())
            {
                try
                {
                    if (!_camera.Read(frame) || frame.Empty())
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    // Local preview (higher FPS)
                    if (_isVideoEnabled)
                    {
                        try
                        {
                            var localBitmap = frame.ToBitmapSource();
                            localBitmap.Freeze();
                            OnLocalVideoFrame?.Invoke(localBitmap);
                        }
                        catch { /* Preview error */ }
                    }

                    // Send to partner (throttled)
                    if (_isCallActive && _isVideoEnabled && DateTime.Now - lastSendTime >= sendInterval)
                    {
                        lastSendTime = DateTime.Now;
                        
                        try
                        {
                            // Resize for network transmission
                            using var resized = new Mat();
                            Cv2.Resize(frame, resized, new OpenCvSharp.Size(320, 240));
                            
                            // Encode as JPEG
                            var jpegData = resized.ToBytes(".jpg", new int[] { (int)ImwriteFlags.JpegQuality, 60 });
                            
                            if (jpegData != null && jpegData.Length > 0)
                            {
                                _hubConnection.SendAsync("SendVideoFrame", _currentPartner, _currentCallId, jpegData);
                            }
                        }
                        catch { /* Send error */ }
                    }

                    Thread.Sleep(16); // ~60 FPS for local preview
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void StartAudioCapture()
        {
            try
            {
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 16, 1), // 16kHz, 16-bit, mono
                    BufferMilliseconds = 50
                };

                _waveIn.DataAvailable += async (s, e) =>
                {
                    if (_isCallActive && _isAudioEnabled && e.BytesRecorded > 0)
                    {
                        try
                        {
                            var audioData = new byte[e.BytesRecorded];
                            Buffer.BlockCopy(e.Buffer, 0, audioData, 0, e.BytesRecorded);
                            await _hubConnection.SendAsync("SendAudioData", _currentPartner, _currentCallId, audioData);
                        }
                        catch { /* Send error */ }
                    }
                };

                _waveIn.StartRecording();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur micro: {ex.Message}");
            }
        }

        private void StartAudioPlayback()
        {
            try
            {
                _playbackBuffer = new BufferedWaveProvider(new WaveFormat(16000, 16, 1))
                {
                    BufferDuration = TimeSpan.FromSeconds(2),
                    DiscardOnBufferOverflow = true
                };

                _waveOut = new WaveOutEvent();
                _waveOut.Init(_playbackBuffer);
                _waveOut.Play();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur audio: {ex.Message}");
            }
        }

        private void CleanupCall()
        {
            _isCallActive = false;
            _currentCallId = string.Empty;
            _currentPartner = string.Empty;
            _isVideoEnabled = true;
            _isAudioEnabled = true;

            // Stop camera
            _isCameraRunning = false;
            try
            {
                _cameraThread?.Interrupt();
                _cameraThread?.Join(500);
            }
            catch { }
            _cameraThread = null;

            try
            {
                _camera?.Release();
                _camera?.Dispose();
            }
            catch { }
            _camera = null;

            // Stop audio capture
            try
            {
                _waveIn?.StopRecording();
                _waveIn?.Dispose();
            }
            catch { }
            _waveIn = null;

            // Stop audio playback
            try
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
            }
            catch { }
            _waveOut = null;
            _playbackBuffer = null;

            OnLocalVideoFrame?.Invoke(null);
            OnRemoteVideoFrame?.Invoke(null);
        }

        public void Dispose()
        {
            CleanupCall();
            _connectionLock.Dispose();
        }
    }
}

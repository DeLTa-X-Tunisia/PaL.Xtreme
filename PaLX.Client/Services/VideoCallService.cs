// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// Video Call Service v2.0 - Professional WebRTC with Modular Architecture
// Features: Opus audio (priority) + G.711 fallback, VP8 video, STUN/TURN, DTLS-SRTP

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.AspNetCore.SignalR.Client;
using NAudio.Wave;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using PaLX.Client.Services.Interfaces;
using PaLX.Client.Services.Encoders;

namespace PaLX.Client.Services
{
    /// <summary>
    /// Professional WebRTC Video Call Service v2.0
    /// 
    /// Architecture:
    /// - Modular design with separate capture, encoding, and transport layers
    /// - Opus audio codec (priority) with G.711 fallback
    /// - VP8 video codec with adaptive bitrate
    /// - STUN + TURN support for reliable NAT traversal
    /// - DTLS-SRTP encryption
    /// </summary>
    public class VideoCallService : IDisposable
    {
        #region Fields

        private readonly HubConnection _hubConnection;
        
        // WebRTC
        private RTCPeerConnection? _peerConnection;
        private List<RTCIceServer> _iceServers;
        
        // Video
        private VideoCapture? _camera;
        private Thread? _cameraThread;
        private volatile bool _isCameraRunning;
        private IPaLXVideoEncoder? _videoEncoder;
        private readonly object _encoderLock = new();
        
        // Audio - Using modular encoders
        private WaveInEvent? _waveIn;
        private WaveOutEvent? _waveOut;
        private BufferedWaveProvider? _playbackBuffer;
        private IPaLXAudioEncoder? _audioEncoder;
        private bool _useOpus = true; // Opus priority, fallback to G.711
        
        // Encoder Factory
        private readonly IEncoderFactory _encoderFactory;
        
        // State
        private bool _isCallActive = false;
        private bool _isVideoEnabled = true;
        private bool _isAudioEnabled = true;
        private string _currentCallId = string.Empty;
        private string _currentPartner = string.Empty;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        
        // Quality settings
        private int _targetBitrate = 500;
        private int _currentFps = 30;
        private DateTime _lastQualityCheck = DateTime.Now;
        
        // TURN Configuration
        private TurnServerConfig? _turnConfig;

        #endregion

        #region Events

        public event Action<string, string>? OnIncomingVideoCall;
        public event Action<string, string>? OnVideoCallAccepted;
        public event Action<string, string>? OnVideoCallDeclined;
        public event Action<string, string>? OnVideoCallEnded;
        public event Action<string>? OnStatusChanged;
        public event Action<BitmapSource?>? OnLocalVideoFrame;
        public event Action<BitmapSource?>? OnRemoteVideoFrame;
        public event Action<bool>? OnPartnerVideoToggled;
        public event Action<bool>? OnPartnerAudioToggled;
        public event Action<string>? OnError;
        public event Action<int>? OnBitrateChanged;
        public event Action<AudioCodec>? OnAudioCodecChanged;

        #endregion

        #region Properties

        public bool IsCallActive => _isCallActive;
        public bool IsVideoEnabled => _isVideoEnabled;
        public bool IsAudioEnabled => _isAudioEnabled;
        public string CurrentCallId => _currentCallId;
        public string CurrentPartner => _currentPartner;
        public int CurrentBitrate => _targetBitrate;
        public AudioCodec CurrentAudioCodec => _audioEncoder?.Codec ?? AudioCodec.Opus;
        public bool IsOpusEnabled => _useOpus;

        #endregion

        #region Constructor

        public VideoCallService(HubConnection hubConnection, TurnServerConfig? turnConfig = null)
        {
            _hubConnection = hubConnection;
            _turnConfig = turnConfig;
            _encoderFactory = new EncoderFactory();
            
            // Configure ICE servers
            _iceServers = BuildIceServers(turnConfig);
            
            InitializeSignalR();
        }

        /// <summary>
        /// Build ICE server list with STUN and optional TURN
        /// </summary>
        private List<RTCIceServer> BuildIceServers(TurnServerConfig? turnConfig)
        {
            var servers = new List<RTCIceServer>
            {
                // Public STUN servers (always available)
                new RTCIceServer { urls = "stun:stun.l.google.com:19302" },
                new RTCIceServer { urls = "stun:stun1.l.google.com:19302" },
                new RTCIceServer { urls = "stun:stun2.l.google.com:19302" },
                new RTCIceServer { urls = "stun:stun3.l.google.com:19302" },
                new RTCIceServer { urls = "stun:stun4.l.google.com:19302" },
                new RTCIceServer { urls = "stun:stun.stunprotocol.org:3478" }
            };
            
            // Add TURN server if configured (essential for ~20% of connections)
            if (turnConfig != null && !string.IsNullOrEmpty(turnConfig.Url))
            {
                servers.Add(new RTCIceServer
                {
                    urls = turnConfig.Url,
                    username = turnConfig.Username,
                    credential = turnConfig.Credential
                });
                
                // Add TURNS (TLS) if available
                if (!string.IsNullOrEmpty(turnConfig.TlsUrl))
                {
                    servers.Add(new RTCIceServer
                    {
                        urls = turnConfig.TlsUrl,
                        username = turnConfig.Username,
                        credential = turnConfig.Credential
                    });
                }
            }
            
            return servers;
        }

        #endregion

        #region TURN Configuration

        /// <summary>
        /// Configure TURN server dynamically
        /// </summary>
        public void ConfigureTurnServer(TurnServerConfig config)
        {
            _turnConfig = config;
            _iceServers = BuildIceServers(config);
            OnStatusChanged?.Invoke($"TURN configuré: {config.Url}");
        }

        /// <summary>
        /// Update TURN credentials (for time-limited auth)
        /// </summary>
        public void UpdateTurnCredentials(string username, string credential)
        {
            if (_turnConfig != null)
            {
                _turnConfig.Username = username;
                _turnConfig.Credential = credential;
                _iceServers = BuildIceServers(_turnConfig);
            }
        }

        #endregion

        #region Audio Codec Configuration

        /// <summary>
        /// Enable/disable Opus codec (use G.711 as fallback)
        /// </summary>
        public void SetOpusEnabled(bool enabled)
        {
            _useOpus = enabled;
            OnStatusChanged?.Invoke(enabled ? "Opus activé (haute qualité)" : "G.711 activé (compatibilité)");
        }

        /// <summary>
        /// Set audio bitrate for Opus (ignored for G.711)
        /// </summary>
        public void SetAudioBitrate(int kbps)
        {
            if (_audioEncoder is OpusAudioEncoder opusEncoder)
            {
                opusEncoder.TargetBitrate = kbps;
                OnStatusChanged?.Invoke($"Bitrate audio: {kbps} kbps");
            }
        }

        #endregion

        #region SignalR Setup

        private void InitializeSignalR()
        {
            // Incoming video call
            _hubConnection.On<string, string>("IncomingVideoCall", (sender, callId) =>
            {
                if (_isCallActive)
                {
                    _hubConnection.SendAsync("DeclineVideoCall", sender, callId);
                    return;
                }
                OnIncomingVideoCall?.Invoke(sender, callId);
            });

            // Call accepted - create offer
            _hubConnection.On<string, string>("VideoCallAccepted", async (callee, callId) =>
            {
                _currentPartner = callee;
                _currentCallId = callId;
                _isCallActive = true;
                OnVideoCallAccepted?.Invoke(callee, callId);
                OnStatusChanged?.Invoke("Connexion WebRTC...");
                
                // Start camera early for faster preview (parallel with WebRTC init)
                _ = Task.Run(() => StartCameraCapture());
                
                await InitializeWebRTC(true);
            });

            // Call declined
            _hubConnection.On<string, string>("VideoCallDeclined", (callee, callId) =>
            {
                OnVideoCallDeclined?.Invoke(callee, callId);
                OnStatusChanged?.Invoke("Appel refusé");
                CleanupCall();
            });

            // Call ended
            _hubConnection.On<string, string>("VideoCallEnded", (partner, callId) =>
            {
                OnVideoCallEnded?.Invoke(partner, callId);
                OnStatusChanged?.Invoke("Appel terminé");
                CleanupCall();
            });

            // Receive SDP offer
            _hubConnection.On<string, string, string>("ReceiveVideoOffer", async (sender, callId, sdpOffer) =>
            {
                if (_currentCallId != callId) return;
                
                try
                {
                    // Start camera early for faster preview (parallel with WebRTC init)
                    _ = Task.Run(() => StartCameraCapture());
                    
                    await InitializeWebRTC(false);
                    
                    if (_peerConnection != null)
                    {
                        var result = _peerConnection.setRemoteDescription(new RTCSessionDescriptionInit
                        {
                            type = RTCSdpType.offer,
                            sdp = sdpOffer
                        });
                        
                        if (result == SetDescriptionResultEnum.OK)
                        {
                            var answer = _peerConnection.createAnswer();
                            await _peerConnection.setLocalDescription(answer);
                            
                            await _hubConnection.SendAsync("SendVideoAnswer", _currentPartner, _currentCallId, answer.sdp);
                            OnStatusChanged?.Invoke("Négociation...");
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Erreur SDP: {ex.Message}");
                }
            });

            // Receive SDP answer
            _hubConnection.On<string, string, string>("ReceiveVideoAnswer", (sender, callId, sdpAnswer) =>
            {
                if (_currentCallId != callId || _peerConnection == null) return;
                
                _peerConnection.setRemoteDescription(new RTCSessionDescriptionInit
                {
                    type = RTCSdpType.answer,
                    sdp = sdpAnswer
                });
                OnStatusChanged?.Invoke("Connexion établie");
            });

            // Receive ICE candidate
            _hubConnection.On<string, string, string, int, string>("ReceiveVideoIceCandidate",
                (sender, callId, candidate, sdpMLineIndex, sdpMid) =>
            {
                if (_currentCallId != callId || _peerConnection == null) return;
                
                _peerConnection.addIceCandidate(new RTCIceCandidateInit
                {
                    candidate = candidate,
                    sdpMid = sdpMid,
                    sdpMLineIndex = (ushort)sdpMLineIndex
                });
            });

            // Partner toggled video/audio
            _hubConnection.On<string, string, bool>("PartnerVideoToggled", (sender, callId, isEnabled) =>
            {
                if (_currentCallId == callId) OnPartnerVideoToggled?.Invoke(isEnabled);
            });

            _hubConnection.On<string, string, bool>("PartnerAudioToggled", (sender, callId, isEnabled) =>
            {
                if (_currentCallId == callId) OnPartnerAudioToggled?.Invoke(isEnabled);
            });
        }

        #endregion

        #region Call Management

        public async Task RequestVideoCall(string receiver)
        {
            if (_isCallActive)
            {
                OnError?.Invoke("Un appel est déjà en cours");
                return;
            }

            _currentCallId = Guid.NewGuid().ToString();
            _currentPartner = receiver;
            OnStatusChanged?.Invoke("Appel en cours...");
            
            await _hubConnection.SendAsync("RequestVideoCall", receiver, _currentCallId);
        }

        public async Task AcceptVideoCall(string caller, string callId)
        {
            _currentPartner = caller;
            _currentCallId = callId;
            _isCallActive = true;
            
            OnStatusChanged?.Invoke("Connexion...");
            await _hubConnection.SendAsync("AcceptVideoCall", caller, callId);
        }

        public async Task DeclineVideoCall(string caller, string callId)
        {
            await _hubConnection.SendAsync("DeclineVideoCall", caller, callId);
        }

        public async Task EndVideoCall()
        {
            if (!_isCallActive || string.IsNullOrEmpty(_currentPartner)) return;
            
            await _hubConnection.SendAsync("EndVideoCall", _currentPartner, _currentCallId);
            CleanupCall();
        }

        public async Task ToggleVideo()
        {
            _isVideoEnabled = !_isVideoEnabled;
            
            if (!string.IsNullOrEmpty(_currentPartner))
            {
                await _hubConnection.SendAsync("ToggleVideoStream", _currentPartner, _currentCallId, _isVideoEnabled);
            }
            
            if (!_isVideoEnabled) OnLocalVideoFrame?.Invoke(null);
            OnStatusChanged?.Invoke(_isVideoEnabled ? "Caméra activée" : "Caméra désactivée");
        }

        public async Task ToggleAudio()
        {
            _isAudioEnabled = !_isAudioEnabled;
            
            if (!string.IsNullOrEmpty(_currentPartner))
            {
                await _hubConnection.SendAsync("ToggleVideoAudio", _currentPartner, _currentCallId, _isAudioEnabled);
            }
            
            OnStatusChanged?.Invoke(_isAudioEnabled ? "Micro activé" : "Micro désactivé");
        }

        #endregion

        #region WebRTC Initialization

        private async Task InitializeWebRTC(bool isCaller)
        {
            await _connectionLock.WaitAsync();
            try
            {
                var config = new RTCConfiguration
                {
                    iceServers = _iceServers,
                    X_UseRtpFeedbackProfile = true
                };

                _peerConnection = new RTCPeerConnection(config);

                // Initialize encoders using factory
                _videoEncoder = _encoderFactory.CreateVideoEncoder(VideoCodec.VP8);
                _videoEncoder.TargetBitrate = _targetBitrate;
                
                // Choose audio codec: Opus (priority) or G.711 (fallback)
                _audioEncoder = _useOpus 
                    ? _encoderFactory.CreateAudioEncoder(AudioCodec.Opus)
                    : _encoderFactory.CreateAudioEncoder(AudioCodec.PCMU);
                
                OnAudioCodecChanged?.Invoke(_audioEncoder.Codec);
                OnStatusChanged?.Invoke($"Audio: {_audioEncoder.Codec}");

                // Add video track (VP8) - dynamic format with ID 96
                var videoFormat = new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000);
                var videoTrack = new MediaStreamTrack(
                    SDPMediaTypesEnum.video,
                    false,
                    new List<SDPAudioVideoMediaFormat> { videoFormat },
                    MediaStreamStatusEnum.SendRecv);
                _peerConnection.addTrack(videoTrack);

                // Add audio track based on selected codec
                SDPAudioVideoMediaFormat audioFormat;
                if (_useOpus)
                {
                    // Opus: dynamic ID 111, 48kHz, stereo
                    audioFormat = new SDPAudioVideoMediaFormat(
                        SDPMediaTypesEnum.audio, 111, "opus", 48000, 2, "minptime=10;useinbandfec=1");
                }
                else
                {
                    // G.711 PCMU: well-known format
                    audioFormat = new SDPAudioVideoMediaFormat(SDPWellKnownMediaFormatsEnum.PCMU);
                }
                
                var audioTrack = new MediaStreamTrack(
                    SDPMediaTypesEnum.audio,
                    false,
                    new List<SDPAudioVideoMediaFormat> { audioFormat },
                    MediaStreamStatusEnum.SendRecv);
                _peerConnection.addTrack(audioTrack);

                // ICE candidate callback
                _peerConnection.onicecandidate += (candidate) =>
                {
                    if (candidate != null && !string.IsNullOrEmpty(_currentPartner))
                    {
                        _hubConnection.SendAsync("SendVideoIceCandidate", 
                            _currentPartner, _currentCallId,
                            candidate.candidate, candidate.sdpMLineIndex, candidate.sdpMid ?? "");
                    }
                };

                // Connection state
                _peerConnection.onconnectionstatechange += (state) =>
                {
                    switch (state)
                    {
                        case RTCPeerConnectionState.connected:
                            OnStatusChanged?.Invoke($"Connecté (WebRTC + {_audioEncoder?.Codec})");
                            _isCallActive = true;
                            StartMediaCapture();
                            break;
                        case RTCPeerConnectionState.disconnected:
                            OnStatusChanged?.Invoke("Déconnecté");
                            break;
                        case RTCPeerConnectionState.failed:
                            OnError?.Invoke("Échec de connexion WebRTC");
                            CleanupCall();
                            break;
                    }
                };

                // ICE state
                _peerConnection.oniceconnectionstatechange += (state) =>
                {
                    OnStatusChanged?.Invoke($"ICE: {state}");
                };

                // Receive remote video
                _peerConnection.OnVideoFrameReceived += OnRemoteVideoFrameReceived;

                // Receive remote audio
                _peerConnection.OnRtpPacketReceived += OnRtpPacketReceived;

                // Create offer if caller
                if (isCaller)
                {
                    var offer = _peerConnection.createOffer();
                    await _peerConnection.setLocalDescription(offer);
                    
                    await _hubConnection.SendAsync("SendVideoOffer", _currentPartner, _currentCallId, offer.sdp);
                    OnStatusChanged?.Invoke("Offre envoyée...");
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur WebRTC: {ex.Message}");
                CleanupCall();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        #endregion

        #region Media Capture

        private void StartMediaCapture()
        {
            // Only start camera if not already running (may have been pre-started)
            if (!_isCameraRunning)
            {
                StartCameraCapture();
            }
            StartAudioCapture();
            StartAudioPlayback();
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

                _camera.Set(VideoCaptureProperties.FrameWidth, 640);
                _camera.Set(VideoCaptureProperties.FrameHeight, 480);
                _camera.Set(VideoCaptureProperties.Fps, 30);

                _isCameraRunning = true;
                _cameraThread = new Thread(CameraCaptureLoop)
                {
                    IsBackground = true,
                    Name = "WebRTCCameraThread",
                    Priority = ThreadPriority.AboveNormal
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
            var frameInterval = TimeSpan.FromMilliseconds(1000.0 / _currentFps);
            var lastFrameTime = DateTime.Now;
            uint rtpTimestamp = 0;

            while (_isCameraRunning && _camera != null && _camera.IsOpened())
            {
                try
                {
                    var elapsed = DateTime.Now - lastFrameTime;
                    if (elapsed < frameInterval)
                    {
                        Thread.Sleep(frameInterval - elapsed);
                    }
                    lastFrameTime = DateTime.Now;

                    if (!_camera.Read(frame) || frame.Empty())
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    // Local preview
                    if (_isVideoEnabled)
                    {
                        try
                        {
                            var previewBitmap = frame.ToBitmapSource();
                            previewBitmap.Freeze();
                            OnLocalVideoFrame?.Invoke(previewBitmap);
                        }
                        catch { }
                    }

                    // Encode and send via WebRTC
                    if (_isCallActive && _isVideoEnabled && _peerConnection != null && _videoEncoder != null)
                    {
                        try
                        {
                            // Convert BGR to byte array
                            byte[] bgrData = new byte[frame.Total() * frame.ElemSize()];
                            System.Runtime.InteropServices.Marshal.Copy(frame.Data, bgrData, 0, bgrData.Length);
                            
                            lock (_encoderLock)
                            {
                                var encodedFrame = _videoEncoder.Encode(bgrData, 640, 480);
                                
                                if (encodedFrame != null && encodedFrame.Data.Length > 0)
                                {
                                    rtpTimestamp += (uint)(90000 / _currentFps);
                                    _peerConnection.SendVideo(rtpTimestamp, encodedFrame.Data);
                                }
                            }
                            
                            CheckAndAdaptQuality();
                        }
                        catch { }
                    }
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
                catch
                {
                    Thread.Sleep(50);
                }
            }
        }

        private void OnRemoteVideoFrameReceived(IPEndPoint ep, uint timestamp, byte[] encodedFrame, VideoFormat format)
        {
            try
            {
                if (_videoEncoder == null) return;

                var decodedFrame = _videoEncoder.Decode(encodedFrame);
                
                if (decodedFrame != null && decodedFrame.Data != null)
                {
                    int width = decodedFrame.Width;
                    int height = decodedFrame.Height;
                    
                    using var mat = Mat.FromPixelData(height, width, MatType.CV_8UC3, decodedFrame.Data);
                    var bitmap = mat.ToBitmapSource();
                    bitmap.Freeze();
                    OnRemoteVideoFrame?.Invoke(bitmap);
                }
            }
            catch { }
        }

        #endregion

        #region Audio Capture/Playback

        private void StartAudioCapture()
        {
            try
            {
                // Configure audio format based on codec
                int sampleRate = _audioEncoder?.SampleRate ?? 48000;
                int channels = _audioEncoder?.Channels ?? 2;
                
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(sampleRate, 16, channels),
                    BufferMilliseconds = 20
                };

                _waveIn.DataAvailable += (s, e) =>
                {
                    if (!_isCallActive || !_isAudioEnabled || _peerConnection == null || _audioEncoder == null)
                        return;

                    try
                    {
                        // Encode using selected codec (Opus or G.711)
                        var encodedData = _audioEncoder.Encode(e.Buffer, e.BytesRecorded);
                        
                        if (encodedData.Length > 0)
                        {
                            _peerConnection.SendAudio((uint)encodedData.Length, encodedData);
                        }
                    }
                    catch { }
                };

                _waveIn.StartRecording();
                OnStatusChanged?.Invoke($"Audio capture démarrée ({_audioEncoder?.Codec})");
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
                int sampleRate = _audioEncoder?.SampleRate ?? 48000;
                int channels = _audioEncoder?.Channels ?? 2;
                
                _playbackBuffer = new BufferedWaveProvider(new WaveFormat(sampleRate, 16, channels))
                {
                    BufferDuration = TimeSpan.FromSeconds(1),
                    DiscardOnBufferOverflow = true
                };

                _waveOut = new WaveOutEvent { DesiredLatency = 100 };
                _waveOut.Init(_playbackBuffer);
                _waveOut.Play();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur audio: {ex.Message}");
            }
        }

        private void OnRtpPacketReceived(IPEndPoint ep, SDPMediaTypesEnum mediaType, RTPPacket rtpPacket)
        {
            if (mediaType != SDPMediaTypesEnum.audio || _playbackBuffer == null || _audioEncoder == null)
                return;

            try
            {
                // Decode using selected codec
                var pcmData = _audioEncoder.Decode(rtpPacket.Payload);
                
                if (pcmData.Length > 0)
                {
                    _playbackBuffer.AddSamples(pcmData, 0, pcmData.Length);
                }
            }
            catch { }
        }

        #endregion

        #region Quality Management

        private void CheckAndAdaptQuality()
        {
            if ((DateTime.Now - _lastQualityCheck).TotalSeconds < 3) return;
            _lastQualityCheck = DateTime.Now;

            if (_peerConnection?.connectionState == RTCPeerConnectionState.connected)
            {
                // Future: implement RTCStats-based adaptation
            }
        }

        public void SetQualityPreset(VideoQuality quality)
        {
            switch (quality)
            {
                case VideoQuality.Low:
                    _targetBitrate = 200;
                    _currentFps = 15;
                    break;
                case VideoQuality.Medium:
                    _targetBitrate = 500;
                    _currentFps = 24;
                    break;
                case VideoQuality.High:
                    _targetBitrate = 1000;
                    _currentFps = 30;
                    break;
                case VideoQuality.HD:
                    _targetBitrate = 2000;
                    _currentFps = 30;
                    break;
            }
            
            if (_videoEncoder != null)
            {
                _videoEncoder.TargetBitrate = _targetBitrate;
            }
            
            OnBitrateChanged?.Invoke(_targetBitrate);
        }

        #endregion

        #region Cleanup

        private void CleanupCall()
        {
            _isCallActive = false;
            _currentCallId = string.Empty;
            _currentPartner = string.Empty;
            _isVideoEnabled = true;
            _isAudioEnabled = true;

            // Stop camera
            _isCameraRunning = false;
            try { _cameraThread?.Interrupt(); _cameraThread?.Join(500); } catch { }
            _cameraThread = null;

            try { _camera?.Release(); _camera?.Dispose(); } catch { }
            _camera = null;

            // Stop audio
            try { _waveIn?.StopRecording(); _waveIn?.Dispose(); } catch { }
            _waveIn = null;

            try { _waveOut?.Stop(); _waveOut?.Dispose(); } catch { }
            _waveOut = null;
            _playbackBuffer = null;

            // Cleanup encoders
            try { _videoEncoder?.Dispose(); } catch { }
            _videoEncoder = null;
            
            try { _audioEncoder?.Dispose(); } catch { }
            _audioEncoder = null;

            // Close peer connection
            try { _peerConnection?.Close("call ended"); } catch { }
            _peerConnection = null;

            OnLocalVideoFrame?.Invoke(null);
            OnRemoteVideoFrame?.Invoke(null);
        }

        public void Dispose()
        {
            CleanupCall();
            _connectionLock.Dispose();
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Video quality presets
    /// </summary>
    public enum VideoQuality
    {
        Low,      // 200kbps, 15fps
        Medium,   // 500kbps, 24fps
        High,     // 1Mbps, 30fps
        HD        // 2Mbps, 30fps
    }

    /// <summary>
    /// TURN server configuration
    /// </summary>
    public class TurnServerConfig
    {
        /// <summary>
        /// TURN server URL (turn:host:port)
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// TURN over TLS URL (turns:host:port) - optional
        /// </summary>
        public string? TlsUrl { get; set; }

        /// <summary>
        /// TURN username
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// TURN credential/password
        /// </summary>
        public string? Credential { get; set; }

        /// <summary>
        /// Credential type (default: "password")
        /// </summary>
        public string CredentialType { get; set; } = "password";

        /// <summary>
        /// Create default Coturn configuration
        /// </summary>
        public static TurnServerConfig CreateCoturn(string host, int port, string username, string password)
        {
            return new TurnServerConfig
            {
                Url = $"turn:{host}:{port}",
                TlsUrl = $"turns:{host}:{port + 1}",
                Username = username,
                Credential = password
            };
        }
    }

    #endregion
}

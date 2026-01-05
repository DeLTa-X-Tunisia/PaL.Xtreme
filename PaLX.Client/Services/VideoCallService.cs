// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// Video Call Service - Independent WebRTC video calling

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.MixedReality.WebRTC;

namespace PaLX.Client.Services
{
    /// <summary>
    /// Service for managing video calls with WebRTC
    /// Independent from VoiceCallService
    /// </summary>
    public class VideoCallService : IDisposable
    {
        private readonly HubConnection _hubConnection;
        private PeerConnection? _peerConnection;
        private LocalVideoTrack? _localVideoTrack;
        private LocalAudioTrack? _localAudioTrack;
        private VideoTrackSource? _videoSource;
        private AudioTrackSource? _audioSource;
        private Transceiver? _videoTransceiver;
        private Transceiver? _audioTransceiver;

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
                OnStatusChanged?.Invoke("Connexion vidéo...");
                
                // Initialize and create offer
                await InitializePeerConnection(true);
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

            // Receive WebRTC offer
            _hubConnection.On<string, string, string>("ReceiveVideoOffer", async (sender, callId, sdp) =>
            {
                if (_currentCallId != callId) return;
                
                await InitializePeerConnection(false);
                
                if (_peerConnection != null)
                {
                    var sdpMsg = new SdpMessage { Type = SdpMessageType.Offer, Content = sdp };
                    await _peerConnection.SetRemoteDescriptionAsync(sdpMsg);
                    _peerConnection.CreateAnswer();
                }
            });

            // Receive WebRTC answer
            _hubConnection.On<string, string, string>("ReceiveVideoAnswer", async (sender, callId, sdp) =>
            {
                if (_currentCallId != callId || _peerConnection == null) return;
                
                var sdpMsg = new SdpMessage { Type = SdpMessageType.Answer, Content = sdp };
                await _peerConnection.SetRemoteDescriptionAsync(sdpMsg);
            });

            // Receive ICE candidate
            _hubConnection.On<string, string, string, int, string>("ReceiveVideoIceCandidate", 
                (sender, callId, candidate, sdpMlineIndex, sdpMid) =>
            {
                if (_currentCallId != callId || _peerConnection == null) return;
                
                _peerConnection.AddIceCandidate(new IceCandidate 
                { 
                    Content = candidate, 
                    SdpMlineIndex = sdpMlineIndex, 
                    SdpMid = sdpMid 
                });
            });

            // Partner toggled video
            _hubConnection.On<string, string, bool>("PartnerVideoToggled", (sender, callId, isEnabled) =>
            {
                if (_currentCallId == callId)
                {
                    OnPartnerVideoToggled?.Invoke(isEnabled);
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
            OnStatusChanged?.Invoke($"Appel vidéo vers {receiver}...");
            
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
            
            // Initialize PC and wait for offer
            await InitializePeerConnection(false);
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
            
            if (_localVideoTrack != null)
            {
                _localVideoTrack.Enabled = _isVideoEnabled;
            }

            if (!string.IsNullOrEmpty(_currentPartner))
            {
                await _hubConnection.SendAsync("ToggleVideoStream", _currentPartner, _currentCallId, _isVideoEnabled);
            }
            
            OnStatusChanged?.Invoke(_isVideoEnabled ? "Caméra activée" : "Caméra désactivée");
        }

        /// <summary>
        /// Toggle local audio on/off
        /// </summary>
        public async Task ToggleAudio()
        {
            _isAudioEnabled = !_isAudioEnabled;
            
            if (_localAudioTrack != null)
            {
                _localAudioTrack.Enabled = _isAudioEnabled;
            }

            if (!string.IsNullOrEmpty(_currentPartner))
            {
                await _hubConnection.SendAsync("ToggleVideoAudio", _currentPartner, _currentCallId, _isAudioEnabled);
            }
            
            OnStatusChanged?.Invoke(_isAudioEnabled ? "Micro activé" : "Micro désactivé");
        }

        private async Task InitializePeerConnection(bool isCaller)
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (_peerConnection != null) return;

                _peerConnection = new PeerConnection();

                var config = new PeerConnectionConfiguration
                {
                    IceServers = new System.Collections.Generic.List<IceServer>
                    {
                        new IceServer { Urls = { "stun:stun.l.google.com:19302" } },
                        new IceServer { Urls = { "stun:stun1.l.google.com:19302" } },
                        new IceServer { Urls = { "stun:stun2.l.google.com:19302" } }
                    }
                };

                await _peerConnection.InitializeAsync(config);

                // SDP ready callback
                _peerConnection.LocalSdpReadytoSend += async (message) =>
                {
                    if (message.Type == SdpMessageType.Offer)
                    {
                        await _hubConnection.SendAsync("SendVideoOffer", _currentPartner, _currentCallId, message.Content);
                    }
                    else if (message.Type == SdpMessageType.Answer)
                    {
                        await _hubConnection.SendAsync("SendVideoAnswer", _currentPartner, _currentCallId, message.Content);
                    }
                };

                // ICE candidate callback
                _peerConnection.IceCandidateReadytoSend += async (candidate) =>
                {
                    await _hubConnection.SendAsync("SendVideoIceCandidate", _currentPartner, _currentCallId,
                        candidate.Content, candidate.SdpMlineIndex, candidate.SdpMid);
                };

                // Connection state changed
                _peerConnection.Connected += () =>
                {
                    OnStatusChanged?.Invoke("Connecté");
                    _isCallActive = true;
                };

                // Video track added (remote)
                _peerConnection.VideoTrackAdded += (track) =>
                {
                    track.I420AVideoFrameReady += (frame) =>
                    {
                        // Convert I420A frame to BitmapSource for WPF
                        var bitmap = ConvertI420ToBitmap(frame);
                        OnRemoteVideoFrame?.Invoke(bitmap);
                    };
                };

                // Initialize local media
                await InitializeLocalMedia();

                // Create offer if caller
                if (isCaller)
                {
                    _peerConnection.CreateOffer();
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur connexion: {ex.Message}");
                CleanupCall();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task InitializeLocalMedia()
        {
            try
            {
                // Initialize video source
                _videoSource = await DeviceVideoTrackSource.CreateAsync();
                
                if (_videoSource != null)
                {
                    var videoSettings = new LocalVideoTrackInitConfig
                    {
                        trackName = "video_track"
                    };
                    _localVideoTrack = LocalVideoTrack.CreateFromSource(_videoSource, videoSettings);

                    // Local preview
                    _videoSource.I420AVideoFrameReady += (frame) =>
                    {
                        var bitmap = ConvertI420ToBitmap(frame);
                        OnLocalVideoFrame?.Invoke(bitmap);
                    };

                    // Add video transceiver
                    _videoTransceiver = _peerConnection?.AddTransceiver(MediaKind.Video);
                    if (_videoTransceiver != null)
                    {
                        _videoTransceiver.LocalVideoTrack = _localVideoTrack;
                        _videoTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                    }
                }

                // Initialize audio source
                _audioSource = await DeviceAudioTrackSource.CreateAsync();
                
                if (_audioSource != null)
                {
                    var audioSettings = new LocalAudioTrackInitConfig
                    {
                        trackName = "audio_track"
                    };
                    _localAudioTrack = LocalAudioTrack.CreateFromSource(_audioSource, audioSettings);

                    // Add audio transceiver
                    _audioTransceiver = _peerConnection?.AddTransceiver(MediaKind.Audio);
                    if (_audioTransceiver != null)
                    {
                        _audioTransceiver.LocalAudioTrack = _localAudioTrack;
                        _audioTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                    }
                }

                OnStatusChanged?.Invoke("Médias initialisés");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur média: {ex.Message}");
            }
        }

        private BitmapSource? ConvertI420ToBitmap(I420AVideoFrame frame)
        {
            try
            {
                int width = (int)frame.width;
                int height = (int)frame.height;
                
                // Create BGRA buffer
                byte[] bgraData = new byte[width * height * 4];
                
                // Convert I420 to BGRA
                unsafe
                {
                    byte* yPlane = (byte*)frame.dataY;
                    byte* uPlane = (byte*)frame.dataU;
                    byte* vPlane = (byte*)frame.dataV;
                    
                    int yStride = (int)frame.strideY;
                    int uStride = (int)frame.strideU;
                    int vStride = (int)frame.strideV;

                    for (int j = 0; j < height; j++)
                    {
                        for (int i = 0; i < width; i++)
                        {
                            int y = yPlane[j * yStride + i];
                            int u = uPlane[(j / 2) * uStride + (i / 2)];
                            int v = vPlane[(j / 2) * vStride + (i / 2)];

                            // YUV to RGB conversion
                            int c = y - 16;
                            int d = u - 128;
                            int e = v - 128;

                            int r = Clamp((298 * c + 409 * e + 128) >> 8);
                            int g = Clamp((298 * c - 100 * d - 208 * e + 128) >> 8);
                            int b = Clamp((298 * c + 516 * d + 128) >> 8);

                            int idx = (j * width + i) * 4;
                            bgraData[idx + 0] = (byte)b;     // B
                            bgraData[idx + 1] = (byte)g;     // G
                            bgraData[idx + 2] = (byte)r;     // R
                            bgraData[idx + 3] = 255;         // A
                        }
                    }
                }

                // Create BitmapSource
                return System.Windows.Media.Imaging.BitmapSource.Create(
                    width, height,
                    96, 96,
                    System.Windows.Media.PixelFormats.Bgra32,
                    null,
                    bgraData,
                    width * 4
                );
            }
            catch
            {
                return null;
            }
        }

        private static int Clamp(int value) => Math.Max(0, Math.Min(255, value));

        private void CleanupCall()
        {
            _isCallActive = false;
            _currentCallId = string.Empty;
            _currentPartner = string.Empty;
            _isVideoEnabled = true;
            _isAudioEnabled = true;

            _localVideoTrack?.Dispose();
            _localVideoTrack = null;

            _localAudioTrack?.Dispose();
            _localAudioTrack = null;

            _videoSource?.Dispose();
            _videoSource = null;

            _audioSource?.Dispose();
            _audioSource = null;

            _peerConnection?.Close();
            _peerConnection?.Dispose();
            _peerConnection = null;

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

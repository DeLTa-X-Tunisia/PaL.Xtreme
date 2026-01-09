// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// Video Call Service v3.0 - MixedReality.WebRTC Native Implementation
// Features: Native camera/mic, H.264/VP8 codecs, fast startup, screen sharing

using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.MixedReality.WebRTC;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PaLX.Client.Services
{
    /// <summary>
    /// Professional WebRTC Video Call Service v3.0
    /// Uses MixedReality.WebRTC for native camera/mic access and encoding
    /// Independent from VoiceCallService
    /// </summary>
    public class VideoCallService : IDisposable
    {
        #region Fields

        private readonly HubConnection _hubConnection;
        
        // WebRTC
        private PeerConnection? _peerConnection;
        private Transceiver? _videoTransceiver;
        private Transceiver? _audioTransceiver;
        
        // Media Sources
        private DeviceVideoTrackSource? _videoSource;
        private DeviceAudioTrackSource? _audioSource;
        private ExternalVideoTrackSource? _screenSource;
        
        // Local Tracks
        private LocalVideoTrack? _localVideoTrack;
        private LocalAudioTrack? _localAudioTrack;
        
        // State
        private bool _isCallActive = false;
        private bool _isVideoEnabled = true;
        private bool _isAudioEnabled = true;
        private bool _isScreenSharing = false;
        private string _currentCallId = string.Empty;
        private string _currentPartner = string.Empty;
        private bool _disposed = false;
        
        // Screen capture
        private Thread? _screenCaptureThread;
        private volatile bool _isScreenCaptureRunning;
        
        // Frame conversion
        private readonly object _frameLock = new();
        
        // Cleanup synchronization
        private readonly object _cleanupLock = new();
        private bool _isCleaningUp = false;

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
        public event Action<bool>? OnScreenShareToggled;
        public event Action<string>? OnError;

        #endregion

        #region Properties

        public bool IsCallActive => _isCallActive;
        public bool IsVideoEnabled => _isVideoEnabled;
        public bool IsAudioEnabled => _isAudioEnabled;
        public bool IsScreenSharing => _isScreenSharing;

        #endregion

        #region Constructor

        public VideoCallService(HubConnection hubConnection)
        {
            _hubConnection = hubConnection;
            InitializeSignalR();
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
                    _ = _hubConnection.SendAsync("DeclineVideoCall", sender, callId);
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
                
                await InitializeWebRTC(true);
            });

            // Call declined
            _hubConnection.On<string, string>("VideoCallDeclined", async (callee, callId) =>
            {
                OnVideoCallDeclined?.Invoke(callee, callId);
                OnStatusChanged?.Invoke("Appel refusé");
                await ApiService.Instance.UpdateStatusAsync(0); // Reset to Online
                CleanupCall();
            });

            // Call ended
            _hubConnection.On<string, string>("VideoCallEnded", async (partner, callId) =>
            {
                OnVideoCallEnded?.Invoke(partner, callId);
                OnStatusChanged?.Invoke("Appel terminé");
                await ApiService.Instance.UpdateStatusAsync(0); // Reset to Online
                CleanupCall();
            });

            // Receive SDP offer
            _hubConnection.On<string, string, string>("ReceiveVideoOffer", async (sender, callId, sdpOffer) =>
            {
                if (_currentCallId != callId) return;
                
                try
                {
                    await InitializeWebRTC(false);
                    
                    if (_peerConnection != null)
                    {
                        var desc = new SdpMessage { Type = SdpMessageType.Offer, Content = sdpOffer };
                        await _peerConnection.SetRemoteDescriptionAsync(desc);
                        
                        if (_peerConnection.CreateAnswer())
                        {
                            OnStatusChanged?.Invoke("Négociation...");
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Erreur SDP offer: {ex.Message}");
                }
            });

            // Receive SDP answer
            _hubConnection.On<string, string, string>("ReceiveVideoAnswer", async (sender, callId, sdpAnswer) =>
            {
                if (_currentCallId != callId || _peerConnection == null) return;
                
                try
                {
                    var desc = new SdpMessage { Type = SdpMessageType.Answer, Content = sdpAnswer };
                    await _peerConnection.SetRemoteDescriptionAsync(desc);
                    OnStatusChanged?.Invoke("Connecté");
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Erreur SDP answer: {ex.Message}");
                }
            });

            // Receive ICE candidate
            _hubConnection.On<string, string, string, int, string>("ReceiveVideoIceCandidate", 
                (sender, callId, candidate, sdpMlineIndex, sdpMid) =>
            {
                if (_currentCallId != callId || _peerConnection == null) return;
                
                try
                {
                    _peerConnection.AddIceCandidate(new IceCandidate
                    {
                        Content = candidate,
                        SdpMlineIndex = sdpMlineIndex,
                        SdpMid = sdpMid
                    });
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Erreur ICE: {ex.Message}");
                }
            });

            // Partner toggled video
            _hubConnection.On<string, string, bool>("PartnerVideoToggled", (sender, callId, isEnabled) =>
            {
                if (_currentCallId != callId) return;
                OnPartnerVideoToggled?.Invoke(isEnabled);
                if (!isEnabled) OnRemoteVideoFrame?.Invoke(null);
            });

            // Partner toggled audio
            _hubConnection.On<string, string, bool>("PartnerAudioToggled", (sender, callId, isEnabled) =>
            {
                if (_currentCallId != callId) return;
                OnPartnerAudioToggled?.Invoke(isEnabled);
            });
        }

        #endregion

        #region Call Management

        public async Task RequestVideoCall(string targetUser)
        {
            if (_isCallActive)
            {
                OnError?.Invoke("Un appel est déjà en cours");
                return;
            }

            _currentPartner = targetUser;
            _currentCallId = Guid.NewGuid().ToString();
            
            OnStatusChanged?.Invoke($"Appel vers {targetUser}...");
            await _hubConnection.SendAsync("RequestVideoCall", targetUser, _currentCallId);
        }

        public async Task AcceptVideoCall(string caller, string callId)
        {
            _currentPartner = caller;
            _currentCallId = callId;
            _isCallActive = true;
            
            await ApiService.Instance.UpdateStatusAsync(3); // En appel
            OnStatusChanged?.Invoke("Connexion...");
            await _hubConnection.SendAsync("AcceptVideoCall", caller, callId);
        }

        public async Task DeclineVideoCall(string caller, string callId)
        {
            await _hubConnection.SendAsync("DeclineVideoCall", caller, callId);
            CleanupCall();
        }

        public async Task EndVideoCall()
        {
            if (!string.IsNullOrEmpty(_currentPartner) && !string.IsNullOrEmpty(_currentCallId))
            {
                await _hubConnection.SendAsync("EndVideoCall", _currentPartner, _currentCallId);
            }
            
            await ApiService.Instance.UpdateStatusAsync(0); // En ligne
            CleanupCall();
        }

        #endregion

        #region Media Controls

        public async Task ToggleVideo()
        {
            _isVideoEnabled = !_isVideoEnabled;
            System.Diagnostics.Debug.WriteLine($"[VideoCall] ToggleVideo: _isVideoEnabled={_isVideoEnabled}");
            
            // Don't disable the track itself - just stop sending frames to UI
            // Disabling the native track can cause crashes
            
            if (!string.IsNullOrEmpty(_currentPartner))
            {
                await _hubConnection.SendAsync("ToggleVideoStream", _currentPartner, _currentCallId, _isVideoEnabled);
            }
            
            OnStatusChanged?.Invoke(_isVideoEnabled ? "Caméra activée" : "Caméra désactivée");
        }

        public async Task ToggleAudio()
        {
            _isAudioEnabled = !_isAudioEnabled;
            System.Diagnostics.Debug.WriteLine($"[VideoCall] ToggleAudio: _isAudioEnabled={_isAudioEnabled}, _localAudioTrack={_localAudioTrack != null}");
            
            if (_localAudioTrack != null)
            {
                _localAudioTrack.Enabled = _isAudioEnabled;
                System.Diagnostics.Debug.WriteLine($"[VideoCall] Audio track enabled set to: {_localAudioTrack.Enabled}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[VideoCall] WARNING: _localAudioTrack is null!");
            }
            
            if (!string.IsNullOrEmpty(_currentPartner))
            {
                await _hubConnection.SendAsync("ToggleVideoAudio", _currentPartner, _currentCallId, _isAudioEnabled);
            }
            
            OnStatusChanged?.Invoke(_isAudioEnabled ? "Micro activé" : "Micro désactivé");
        }

        public async Task ToggleScreenShare()
        {
            if (!_isCallActive) return;
            
            _isScreenSharing = !_isScreenSharing;
            
            if (_isScreenSharing)
            {
                await StartScreenShare();
            }
            else
            {
                await StopScreenShare();
            }
            
            OnScreenShareToggled?.Invoke(_isScreenSharing);
            OnStatusChanged?.Invoke(_isScreenSharing ? "Partage d'écran activé" : "Partage d'écran désactivé");
        }

        #endregion

        #region WebRTC Initialization

        private async Task InitializeWebRTC(bool isCaller)
        {
            try
            {
                OnStatusChanged?.Invoke("Initialisation WebRTC...");
                
                // Initialize media FIRST - before peer connection
                await InitializeMedia();
                
                // Create peer connection
                var config = new PeerConnectionConfiguration
                {
                    IceServers = new System.Collections.Generic.List<IceServer>
                    {
                        new IceServer { Urls = { "stun:stun.l.google.com:19302" } },
                        new IceServer { Urls = { "stun:stun1.l.google.com:19302" } }
                    }
                };
                
                _peerConnection = new PeerConnection();
                await _peerConnection.InitializeAsync(config);
                
                // Setup event handlers
                _peerConnection.LocalSdpReadytoSend += OnLocalSdpReady;
                _peerConnection.IceCandidateReadytoSend += OnIceCandidateReady;
                _peerConnection.Connected += () => 
                {
                    OnStatusChanged?.Invoke("Connecté ✓");
                    System.Diagnostics.Debug.WriteLine("[VideoCall] PeerConnection connected!");
                };
                _peerConnection.IceStateChanged += state => 
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoCall] ICE state: {state}");
                    if (state == IceConnectionState.Disconnected || state == IceConnectionState.Failed)
                    {
                        OnStatusChanged?.Invoke("Connexion perdue");
                    }
                };
                
                // Handle remote video - subscribe to BOTH frame formats
                _peerConnection.VideoTrackAdded += (track) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoCall] Remote VIDEO track added: {track.Name}");
                    track.I420AVideoFrameReady += OnRemoteI420AFrameReady;
                    track.Argb32VideoFrameReady += OnRemoteArgb32FrameReady;
                };
                
                // Handle remote audio
                _peerConnection.AudioTrackAdded += (track) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoCall] Remote AUDIO track added: {track.Name}");
                    OnStatusChanged?.Invoke("Audio distant connecté 🔊");
                };
                
                if (isCaller)
                {
                    // CALLER: Add transceivers and attach local tracks, then create offer
                    _videoTransceiver = _peerConnection.AddTransceiver(MediaKind.Video);
                    _videoTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                    if (_localVideoTrack != null)
                    {
                        _videoTransceiver.LocalVideoTrack = _localVideoTrack;
                        System.Diagnostics.Debug.WriteLine("[VideoCall] CALLER: Video track attached");
                    }
                    
                    _audioTransceiver = _peerConnection.AddTransceiver(MediaKind.Audio);
                    _audioTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                    if (_localAudioTrack != null)
                    {
                        _audioTransceiver.LocalAudioTrack = _localAudioTrack;
                        System.Diagnostics.Debug.WriteLine("[VideoCall] CALLER: Audio track attached");
                    }
                    
                    // Create offer
                    System.Diagnostics.Debug.WriteLine("[VideoCall] CALLER: Creating offer...");
                    _peerConnection.CreateOffer();
                }
                else
                {
                    // RECEIVER: Register TransceiverAdded handler BEFORE SetRemoteDescription
                    // The transceivers will be created when SetRemoteDescription processes the offer
                    _peerConnection.TransceiverAdded += (transceiver) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[VideoCall] RECEIVER: TransceiverAdded - {transceiver.MediaKind}");
                        
                        transceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                        
                        if (transceiver.MediaKind == MediaKind.Video)
                        {
                            _videoTransceiver = transceiver;
                            if (_localVideoTrack != null)
                            {
                                transceiver.LocalVideoTrack = _localVideoTrack;
                                System.Diagnostics.Debug.WriteLine("[VideoCall] RECEIVER: Video track attached to transceiver");
                            }
                        }
                        else if (transceiver.MediaKind == MediaKind.Audio)
                        {
                            _audioTransceiver = transceiver;
                            if (_localAudioTrack != null)
                            {
                                transceiver.LocalAudioTrack = _localAudioTrack;
                                System.Diagnostics.Debug.WriteLine("[VideoCall] RECEIVER: Audio track attached to transceiver");
                            }
                        }
                    };
                    System.Diagnostics.Debug.WriteLine("[VideoCall] RECEIVER: TransceiverAdded handler registered, ready for SetRemoteDescription");
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur WebRTC: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[VideoCall] WebRTC Error: {ex}");
                CleanupCall();
            }
        }

        private async Task InitializeMedia()
        {
            try
            {
                OnStatusChanged?.Invoke("Accès caméra...");
                
                // Get selected camera from settings
                int cameraIndex = SettingsService.SelectedCameraIndex;
                var devices = await DeviceVideoTrackSource.GetCaptureDevicesAsync();
                
                string? deviceId = null;
                if (devices.Count > cameraIndex)
                {
                    deviceId = devices[cameraIndex].id;
                }
                else if (devices.Count > 0)
                {
                    deviceId = devices[0].id;
                }
                
                // Create video source with configured quality
                var quality = SettingsService.CurrentVideoQuality;
                var videoSettings = new LocalVideoDeviceInitConfig
                {
                    videoDevice = new VideoCaptureDevice { id = deviceId ?? "" },
                    width = (uint)quality.Width,
                    height = (uint)quality.Height,
                    framerate = quality.Fps
                };
                
                _videoSource = await DeviceVideoTrackSource.CreateAsync(videoSettings);
                System.Diagnostics.Debug.WriteLine($"[VideoCall] Video source created for device: {deviceId}");
                
                // Subscribe to BOTH frame formats - Windows may use either
                _videoSource.I420AVideoFrameReady += OnLocalI420AFrameReady;
                _videoSource.Argb32VideoFrameReady += OnLocalArgb32FrameReady;
                System.Diagnostics.Debug.WriteLine("[VideoCall] Subscribed to I420A and ARGB32 frame events");
                
                // Create local video track
                _localVideoTrack = LocalVideoTrack.CreateFromSource(_videoSource, new LocalVideoTrackInitConfig
                {
                    trackName = "video_track"
                });
                _localVideoTrack.Enabled = true; // Ensure video is enabled
                System.Diagnostics.Debug.WriteLine($"[VideoCall] Video track created, enabled: {_localVideoTrack.Enabled}");
                
                OnStatusChanged?.Invoke("Accès micro...");
                
                // Create audio source with default device
                _audioSource = await DeviceAudioTrackSource.CreateAsync();
                System.Diagnostics.Debug.WriteLine("[VideoCall] Audio source created");
                
                // Create local audio track
                _localAudioTrack = LocalAudioTrack.CreateFromSource(_audioSource, new LocalAudioTrackInitConfig
                {
                    trackName = "audio_track"
                });
                _localAudioTrack.Enabled = true; // Ensure audio is enabled
                System.Diagnostics.Debug.WriteLine($"[VideoCall] Audio track created, enabled: {_localAudioTrack.Enabled}");
                
                OnStatusChanged?.Invoke("Média initialisé ✓");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur média: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[VideoCall] Media Error: {ex}");
            }
        }

        #endregion

        #region Screen Sharing

        private async Task StartScreenShare()
        {
            try
            {
                // Stop camera
                _localVideoTrack?.Dispose();
                _videoSource?.Dispose();
                
                // Create external source for screen capture
                _screenSource = ExternalVideoTrackSource.CreateFromArgb32Callback(ScreenCaptureCallback);
                
                _localVideoTrack = LocalVideoTrack.CreateFromSource(_screenSource, new LocalVideoTrackInitConfig
                {
                    trackName = "screen_track"
                });
                
                if (_videoTransceiver != null)
                {
                    _videoTransceiver.LocalVideoTrack = _localVideoTrack;
                }
                
                // Start screen capture thread
                _isScreenCaptureRunning = true;
                _screenCaptureThread = new Thread(ScreenCaptureLoop)
                {
                    IsBackground = true,
                    Name = "ScreenCaptureThread"
                };
                _screenCaptureThread.Start();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur partage écran: {ex.Message}");
                _isScreenSharing = false;
            }
        }

        private async Task StopScreenShare()
        {
            // Stop capture thread first
            _isScreenCaptureRunning = false;
            _screenCaptureThread?.Join(1000);
            _screenCaptureThread = null;
            
            // Detach screen track from transceiver BEFORE disposing
            if (_videoTransceiver != null)
            {
                _videoTransceiver.LocalVideoTrack = null;
            }
            
            // Dispose the screen video track BEFORE disposing the source
            _localVideoTrack?.Dispose();
            _localVideoTrack = null;
            
            // Now safe to dispose the screen source
            _screenSource?.Dispose();
            _screenSource = null;
            
            // Reinitialize camera
            await InitializeMedia();
            
            // Reattach camera track
            if (_videoTransceiver != null && _localVideoTrack != null)
            {
                _videoTransceiver.LocalVideoTrack = _localVideoTrack;
            }
        }

        // Screen capture buffer
        private IntPtr _screenArgb32Buffer = IntPtr.Zero;
        private int _screenWidth;
        private int _screenHeight;
        private int _screenBufferSize;

        private void ScreenCaptureCallback(in FrameRequest request)
        {
            lock (_frameLock)
            {
                if (_screenArgb32Buffer != IntPtr.Zero && _screenWidth > 0 && _screenHeight > 0)
                {
                    var frame = new Argb32VideoFrame
                    {
                        data = _screenArgb32Buffer,
                        width = (uint)_screenWidth,
                        height = (uint)_screenHeight,
                        stride = _screenWidth * 4
                    };
                    
                    request.CompleteRequest(frame);
                }
            }
        }

        private void ScreenCaptureLoop()
        {
            var frameInterval = TimeSpan.FromMilliseconds(1000.0 / 15); // 15 FPS for screen share
            
            while (_isScreenCaptureRunning)
            {
                try
                {
                    var startTime = DateTime.Now;
                    
                    // Capture screen
                    using var bitmap = CaptureScreen();
                    if (bitmap != null)
                    {
                        // Convert to ARGB32 buffer
                        lock (_frameLock)
                        {
                            _screenWidth = bitmap.Width;
                            _screenHeight = bitmap.Height;
                            
                            var bmpData = bitmap.LockBits(
                                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                ImageLockMode.ReadOnly,
                                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            
                            try
                            {
                                int requiredSize = bmpData.Stride * bmpData.Height;
                                
                                // Allocate or reallocate buffer if needed
                                if (_screenArgb32Buffer == IntPtr.Zero || _screenBufferSize < requiredSize)
                                {
                                    if (_screenArgb32Buffer != IntPtr.Zero)
                                    {
                                        Marshal.FreeHGlobal(_screenArgb32Buffer);
                                    }
                                    _screenArgb32Buffer = Marshal.AllocHGlobal(requiredSize);
                                    _screenBufferSize = requiredSize;
                                }
                                
                                // Copy bitmap data
                                unsafe
                                {
                                    Buffer.MemoryCopy(
                                        (void*)bmpData.Scan0,
                                        (void*)_screenArgb32Buffer,
                                        requiredSize,
                                        requiredSize);
                                }
                            }
                            finally
                            {
                                bitmap.UnlockBits(bmpData);
                            }
                        }
                        
                        // Also show local preview
                        var bitmapSource = BitmapToBitmapSource(bitmap);
                        bitmapSource?.Freeze();
                        OnLocalVideoFrame?.Invoke(bitmapSource);
                    }
                    
                    var elapsed = DateTime.Now - startTime;
                    if (elapsed < frameInterval)
                    {
                        Thread.Sleep(frameInterval - elapsed);
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
            
            // Free screen buffer
            if (_screenArgb32Buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_screenArgb32Buffer);
                _screenArgb32Buffer = IntPtr.Zero;
            }
        }

        private Bitmap? CaptureScreen()
        {
            try
            {
                var screenBounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds;
                if (screenBounds == null) return null;
                
                var bitmap = new Bitmap(screenBounds.Value.Width, screenBounds.Value.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(screenBounds.Value.Location, System.Drawing.Point.Empty, screenBounds.Value.Size);
                
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Frame Conversion

        private int _localFrameCount = 0;
        private int _remoteFrameCount = 0;

        private void OnLocalI420AFrameReady(I420AVideoFrame frame)
        {
            if (!_isVideoEnabled) return;
            
            try
            {
                _localFrameCount++;
                if (_localFrameCount % 30 == 1) // Log every 30 frames
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoCall] Local I420A frame #{_localFrameCount}, size: {frame.width}x{frame.height}");
                }
                
                var bitmapSource = I420AToBitmapSource(frame);
                bitmapSource?.Freeze();
                OnLocalVideoFrame?.Invoke(bitmapSource);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoCall] Local I420A frame error: {ex.Message}");
            }
        }

        private void OnLocalArgb32FrameReady(Argb32VideoFrame frame)
        {
            if (!_isVideoEnabled) return;
            
            try
            {
                _localFrameCount++;
                if (_localFrameCount % 30 == 1) // Log every 30 frames
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoCall] Local ARGB32 frame #{_localFrameCount}, size: {frame.width}x{frame.height}");
                }
                
                var bitmapSource = Argb32ToBitmapSource(frame);
                bitmapSource?.Freeze();
                OnLocalVideoFrame?.Invoke(bitmapSource);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoCall] Local ARGB32 frame error: {ex.Message}");
            }
        }

        private void OnRemoteI420AFrameReady(I420AVideoFrame frame)
        {
            try
            {
                _remoteFrameCount++;
                if (_remoteFrameCount % 30 == 1) // Log every 30 frames
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoCall] Remote I420A frame #{_remoteFrameCount}, size: {frame.width}x{frame.height}");
                }
                
                var bitmapSource = I420AToBitmapSource(frame);
                bitmapSource?.Freeze();
                OnRemoteVideoFrame?.Invoke(bitmapSource);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoCall] Remote I420A frame error: {ex.Message}");
            }
        }

        private void OnRemoteArgb32FrameReady(Argb32VideoFrame frame)
        {
            try
            {
                _remoteFrameCount++;
                if (_remoteFrameCount % 30 == 1) // Log every 30 frames
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoCall] Remote ARGB32 frame #{_remoteFrameCount}, size: {frame.width}x{frame.height}");
                }
                
                var bitmapSource = Argb32ToBitmapSource(frame);
                bitmapSource?.Freeze();
                OnRemoteVideoFrame?.Invoke(bitmapSource);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoCall] Remote ARGB32 frame error: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert ARGB32 frame to WPF BitmapSource (much simpler - direct format)
        /// </summary>
        private BitmapSource? Argb32ToBitmapSource(Argb32VideoFrame frame)
        {
            try
            {
                int width = (int)frame.width;
                int height = (int)frame.height;
                int stride = (int)frame.stride;
                
                if (width <= 0 || height <= 0) return null;
                
                // ARGB32 can be copied directly - it's already in a compatible format
                return BitmapSource.Create(
                    width, height,
                    96, 96,
                    PixelFormats.Bgra32, // ARGB32 is stored as BGRA on little-endian systems
                    null,
                    frame.data,
                    stride * height,
                    stride);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoCall] Argb32ToBitmapSource error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Convert I420A frame to WPF BitmapSource
        /// Uses IntPtr for data access with proper stride handling
        /// </summary>
        private BitmapSource? I420AToBitmapSource(I420AVideoFrame frame)
        {
            try
            {
                int width = (int)frame.width;
                int height = (int)frame.height;
                
                if (width <= 0 || height <= 0) return null;
                
                // Get strides - these may be different from width!
                int strideY = (int)frame.strideY;
                int strideU = (int)frame.strideU;
                int strideV = (int)frame.strideV;
                
                // Use width as stride if stride is 0 (shouldn't happen but safety)
                if (strideY == 0) strideY = width;
                if (strideU == 0) strideU = width / 2;
                if (strideV == 0) strideV = width / 2;
                
                var rgbData = new byte[width * height * 3];
                
                // Copy frame data to managed arrays with correct sizes based on strides
                int ySize = strideY * height;
                int uvHeight = (height + 1) / 2;
                int uSize = strideU * uvHeight;
                int vSize = strideV * uvHeight;
                
                var yData = new byte[ySize];
                var uData = new byte[uSize];
                var vData = new byte[vSize];
                
                Marshal.Copy(frame.dataY, yData, 0, ySize);
                Marshal.Copy(frame.dataU, uData, 0, uSize);
                Marshal.Copy(frame.dataV, vData, 0, vSize);
                
                // YUV to RGB conversion with proper stride handling
                int rgbIndex = 0;
                
                for (int j = 0; j < height; j++)
                {
                    for (int i = 0; i < width; i++)
                    {
                        // Use stride for Y plane indexing
                        int yIndex = j * strideY + i;
                        // Use stride for UV plane indexing (subsampled)
                        int uvIndex = (j / 2) * strideU + (i / 2);
                        
                        int y = yData[yIndex] & 0xFF;
                        int u = uData[uvIndex] & 0xFF;
                        int v = vData[uvIndex] & 0xFF;
                        
                        int c = y - 16;
                        int d = u - 128;
                        int e = v - 128;
                        
                        int r = Clamp((298 * c + 409 * e + 128) >> 8);
                        int g = Clamp((298 * c - 100 * d - 208 * e + 128) >> 8);
                        int b = Clamp((298 * c + 516 * d + 128) >> 8);
                        
                        rgbData[rgbIndex++] = (byte)r;
                        rgbData[rgbIndex++] = (byte)g;
                        rgbData[rgbIndex++] = (byte)b;
                    }
                }
                
                return BitmapSource.Create(
                    width, height,
                    96, 96,
                    PixelFormats.Rgb24,
                    null,
                    rgbData,
                    width * 3);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoCall] I420AToBitmapSource error: {ex.Message}");
                return null;
            }
        }

        private static int Clamp(int value) => value < 0 ? 0 : (value > 255 ? 255 : value);

        private BitmapSource? BitmapToBitmapSource(Bitmap bitmap)
        {
            try
            {
                var bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                
                try
                {
                    return BitmapSource.Create(
                        bitmap.Width, bitmap.Height,
                        96, 96,
                        PixelFormats.Bgra32,
                        null,
                        bmpData.Scan0,
                        bmpData.Height * bmpData.Stride,
                        bmpData.Stride);
                }
                finally
                {
                    bitmap.UnlockBits(bmpData);
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region SignalR Callbacks

        private void OnLocalSdpReady(SdpMessage sdp)
        {
            if (string.IsNullOrEmpty(_currentPartner)) return;
            
            if (sdp.Type == SdpMessageType.Offer)
            {
                _ = _hubConnection.SendAsync("SendVideoOffer", _currentPartner, _currentCallId, sdp.Content);
            }
            else if (sdp.Type == SdpMessageType.Answer)
            {
                _ = _hubConnection.SendAsync("SendVideoAnswer", _currentPartner, _currentCallId, sdp.Content);
            }
        }

        private void OnIceCandidateReady(IceCandidate candidate)
        {
            if (string.IsNullOrEmpty(_currentPartner)) return;
            
            _ = _hubConnection.SendAsync("SendVideoIceCandidate", 
                _currentPartner, _currentCallId,
                candidate.Content, candidate.SdpMlineIndex, candidate.SdpMid ?? "");
        }

        #endregion

        #region Cleanup

        private void CleanupCall()
        {
            // Prevent multiple simultaneous cleanups
            lock (_cleanupLock)
            {
                if (_isCleaningUp) return;
                _isCleaningUp = true;
            }
            
            // Reset frame counters
            _localFrameCount = 0;
            _remoteFrameCount = 0;
            
            // Run cleanup on background thread to avoid UI blocking
            Task.Run(() =>
            {
                try
                {
                    _isCallActive = false;
                    _isScreenSharing = false;
                    _isScreenCaptureRunning = false;
                    _currentCallId = string.Empty;
                    _currentPartner = string.Empty;
                    
                    // Reset video/audio enabled for next call
                    _isVideoEnabled = true;
                    _isAudioEnabled = true;
                    
                    // Stop screen capture with timeout
                    if (_screenCaptureThread != null && _screenCaptureThread.IsAlive)
                    {
                        _screenCaptureThread.Join(200);
                        _screenCaptureThread = null;
                    }
                    
                    // Free screen buffer
                    if (_screenArgb32Buffer != IntPtr.Zero)
                    {
                        try { Marshal.FreeHGlobal(_screenArgb32Buffer); } catch { }
                        _screenArgb32Buffer = IntPtr.Zero;
                    }
                    
                    // Dispose local tracks first
                    try { _localVideoTrack?.Dispose(); } catch { }
                    _localVideoTrack = null;
                    
                    try { _localAudioTrack?.Dispose(); } catch { }
                    _localAudioTrack = null;
                    
                    // Dispose media sources
                    try { _videoSource?.Dispose(); } catch { }
                    _videoSource = null;
                    
                    try { _audioSource?.Dispose(); } catch { }
                    _audioSource = null;
                    
                    try { _screenSource?.Dispose(); } catch { }
                    _screenSource = null;
                    
                    // Dispose peer connection last
                    try { _peerConnection?.Dispose(); } catch { }
                    _peerConnection = null;
                    
                    _videoTransceiver = null;
                    _audioTransceiver = null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VideoCall] Cleanup error: {ex.Message}");
                }
                finally
                {
                    lock (_cleanupLock)
                    {
                        _isCleaningUp = false;
                    }
                }
            });
            
            // Clear frames on UI thread
            try
            {
                OnLocalVideoFrame?.Invoke(null);
                OnRemoteVideoFrame?.Invoke(null);
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            CleanupCall();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}

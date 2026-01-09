// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// Room Video Service - Multi-peer WebRTC for Chatroom Video
// Features: VP8 video encoding, Multi-peer mesh topology, Dynamic camera limits

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.AspNetCore.SignalR.Client;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace PaLX.Client.Services
{
    /// <summary>
    /// Service de gestion vidéo multi-peer pour les chatrooms
    /// Utilise une topologie mesh où chaque participant se connecte à tous les autres
    /// </summary>
    public class RoomVideoService : IDisposable
    {
        #region Constants

        private const int DEFAULT_MAX_CAMERAS = 6;
        private const int PREMIUM_MAX_CAMERAS = 8;
        private const int TARGET_FPS = 24;
        private const int TARGET_BITRATE = 800; // kbps pour performance

        #endregion

        #region Fields

        private readonly HubConnection _hubConnection;
        private readonly int _roomId;
        private readonly int _currentUserId;
        private readonly string _currentUsername;
        
        // Peer Connections - Un par participant distant
        private readonly ConcurrentDictionary<int, RTCPeerConnection> _peerConnections = new();
        private readonly ConcurrentDictionary<int, BitmapSource?> _remoteVideoFrames = new();
        private readonly ConcurrentDictionary<int, RemotePeerInfo> _remotePeers = new();
        
        // Local Video
        private VideoCapture? _camera;
        private Thread? _cameraThread;
        private volatile bool _isCameraRunning;
        private bool _isCameraEnabled = false;
        // Note: Video encoding disabled - using raw frames for now
        private readonly object _encoderLock = new();
        
        // ICE Servers
        private readonly List<RTCIceServer> _iceServers;
        
        // State
        private int _maxCameras = DEFAULT_MAX_CAMERAS;
        private bool _isDisposed = false;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

        #endregion

        #region Events

        /// <summary>Nouvelle frame vidéo locale disponible</summary>
        public event Action<BitmapSource?>? OnLocalVideoFrame;
        
        /// <summary>Nouvelle frame vidéo distante disponible (userId, frame)</summary>
        public event Action<int, BitmapSource?>? OnRemoteVideoFrame;
        
        /// <summary>Un participant a activé sa caméra</summary>
        public event Action<int, string>? OnPeerCameraStarted;
        
        /// <summary>Un participant a désactivé sa caméra</summary>
        public event Action<int>? OnPeerCameraStopped;
        
        /// <summary>Changement de statut</summary>
        public event Action<string>? OnStatusChanged;
        
        /// <summary>Erreur survenue</summary>
        public event Action<string>? OnError;
        
        /// <summary>Limite de caméras atteinte</summary>
        public event Action<int>? OnCameraLimitReached;
        
        /// <summary>Liste des caméras actives mise à jour</summary>
        public event Action<List<int>>? OnActiveCamerasChanged;

        #endregion

        #region Properties

        public bool IsCameraEnabled => _isCameraEnabled;
        public int ActiveCameraCount => _remotePeers.Count(p => p.Value.IsCameraActive) + (_isCameraEnabled ? 1 : 0);
        public int MaxCameras => _maxCameras;
        public IReadOnlyDictionary<int, RemotePeerInfo> RemotePeers => _remotePeers;

        #endregion

        #region Constructor

        public RoomVideoService(HubConnection hubConnection, int roomId, int userId, string username, bool isPremium = false)
        {
            _hubConnection = hubConnection;
            _roomId = roomId;
            _currentUserId = userId;
            _currentUsername = username;
            _maxCameras = isPremium ? PREMIUM_MAX_CAMERAS : DEFAULT_MAX_CAMERAS;
            
            _iceServers = new List<RTCIceServer>
            {
                new RTCIceServer { urls = "stun:stun.l.google.com:19302" },
                new RTCIceServer { urls = "stun:stun1.l.google.com:19302" },
                new RTCIceServer { urls = "stun:stun2.l.google.com:19302" }
            };
            
            InitializeSignalR();
        }

        #endregion

        #region SignalR Initialization

        private void InitializeSignalR()
        {
            // Un participant annonce qu'il active sa caméra
            _hubConnection.On<int, int, string>("RoomCameraStarted", async (roomId, userId, username) =>
            {
                if (roomId != _roomId || userId == _currentUserId) return;
                
                OnStatusChanged?.Invoke($"{username} a activé sa caméra");
                
                // Ajouter le peer
                _remotePeers.TryAdd(userId, new RemotePeerInfo 
                { 
                    UserId = userId, 
                    Username = username, 
                    IsCameraActive = true 
                });
                
                OnPeerCameraStarted?.Invoke(userId, username);
                NotifyActiveCamerasChanged();
                
                // Si ma caméra est active, initier la connexion WebRTC
                if (_isCameraEnabled)
                {
                    await InitiatePeerConnection(userId, true);
                }
            });

            // Un participant désactive sa caméra
            _hubConnection.On<int, int>("RoomCameraStopped", (roomId, userId) =>
            {
                if (roomId != _roomId || userId == _currentUserId) return;
                
                if (_remotePeers.TryGetValue(userId, out var peer))
                {
                    peer.IsCameraActive = false;
                    OnStatusChanged?.Invoke($"{peer.Username} a désactivé sa caméra");
                }
                
                // Fermer la connexion peer
                ClosePeerConnection(userId);
                _remoteVideoFrames.TryRemove(userId, out _);
                
                OnPeerCameraStopped?.Invoke(userId);
                NotifyActiveCamerasChanged();
            });

            // Recevoir une offre WebRTC
            _hubConnection.On<int, int, string>("RoomVideoOffer", async (roomId, fromUserId, sdp) =>
            {
                if (roomId != _roomId) return;
                
                await HandleVideoOffer(fromUserId, sdp);
            });

            // Recevoir une réponse WebRTC
            _hubConnection.On<int, int, string>("RoomVideoAnswer", async (roomId, fromUserId, sdp) =>
            {
                if (roomId != _roomId) return;
                
                await HandleVideoAnswer(fromUserId, sdp);
            });

            // Recevoir un candidat ICE
            _hubConnection.On<int, int, string, int, string>("RoomVideoIceCandidate", (roomId, fromUserId, candidate, sdpMLineIndex, sdpMid) =>
            {
                if (roomId != _roomId) return;
                
                HandleIceCandidate(fromUserId, candidate, sdpMLineIndex, sdpMid);
            });

            // Recevoir la liste des caméras actives en rejoignant
            _hubConnection.On<int, List<RoomCameraInfo>>("RoomActiveCameras", async (roomId, cameras) =>
            {
                if (roomId != _roomId) return;
                
                foreach (var cam in cameras.Where(c => c.UserId != _currentUserId))
                {
                    _remotePeers.TryAdd(cam.UserId, new RemotePeerInfo
                    {
                        UserId = cam.UserId,
                        Username = cam.Username,
                        IsCameraActive = true
                    });
                    
                    OnPeerCameraStarted?.Invoke(cam.UserId, cam.Username);
                }
                
                NotifyActiveCamerasChanged();
                
                // Si ma caméra est active, se connecter à tous
                if (_isCameraEnabled)
                {
                    foreach (var peer in _remotePeers.Values.Where(p => p.IsCameraActive))
                    {
                        await InitiatePeerConnection(peer.UserId, true);
                    }
                }
            });
        }

        #endregion

        #region Camera Control

        /// <summary>
        /// Active ou désactive la caméra locale
        /// </summary>
        public async Task<bool> ToggleCameraAsync()
        {
            if (_isCameraEnabled)
            {
                await StopCameraAsync();
                return false;
            }
            else
            {
                return await StartCameraAsync();
            }
        }

        /// <summary>
        /// Démarre la caméra locale
        /// </summary>
        public async Task<bool> StartCameraAsync()
        {
            if (_isCameraEnabled) return true;
            
            // Vérifier la limite
            if (ActiveCameraCount >= _maxCameras)
            {
                OnCameraLimitReached?.Invoke(_maxCameras);
                OnError?.Invoke($"Limite de {_maxCameras} caméras atteinte");
                return false;
            }

            try
            {
                await _connectionLock.WaitAsync();
                
                // Initialiser la caméra
                _camera = new VideoCapture(0);
                if (!_camera.IsOpened())
                {
                    OnError?.Invoke("Impossible d'ouvrir la caméra");
                    return false;
                }
                
                // Configurer
                _camera.Set(VideoCaptureProperties.FrameWidth, 640);
                _camera.Set(VideoCaptureProperties.FrameHeight, 480);
                _camera.Set(VideoCaptureProperties.Fps, TARGET_FPS);
                
                // Note: Video encoding disabled - using raw frames for now
                
                _isCameraEnabled = true;
                _isCameraRunning = true;
                
                // Démarrer le thread de capture
                _cameraThread = new Thread(CameraCaptureLoop)
                {
                    IsBackground = true,
                    Name = "RoomVideoCameraThread"
                };
                _cameraThread.Start();
                
                // Notifier le room
                await _hubConnection.SendAsync("StartRoomCamera", _roomId);
                
                OnStatusChanged?.Invoke("Caméra activée");
                NotifyActiveCamerasChanged();
                
                // Initier les connexions avec les peers actifs
                foreach (var peer in _remotePeers.Values.Where(p => p.IsCameraActive))
                {
                    await InitiatePeerConnection(peer.UserId, true);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur démarrage caméra: {ex.Message}");
                return false;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Arrête la caméra locale
        /// </summary>
        public async Task StopCameraAsync()
        {
            if (!_isCameraEnabled) return;

            try
            {
                await _connectionLock.WaitAsync();
                
                _isCameraRunning = false;
                _isCameraEnabled = false;
                
                // Attendre la fin du thread
                _cameraThread?.Join(1000);
                
                // Libérer les ressources
                _camera?.Release();
                _camera?.Dispose();
                _camera = null;
                
                // Note: Video encoder cleanup disabled
                
                // Fermer toutes les connexions peer
                foreach (var peerId in _peerConnections.Keys.ToList())
                {
                    ClosePeerConnection(peerId);
                }
                
                // Notifier le room
                await _hubConnection.SendAsync("StopRoomCamera", _roomId);
                
                OnStatusChanged?.Invoke("Caméra désactivée");
                NotifyActiveCamerasChanged();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void CameraCaptureLoop()
        {
            var frame = new Mat();
            var targetInterval = TimeSpan.FromMilliseconds(1000.0 / TARGET_FPS);
            
            while (_isCameraRunning && _camera != null)
            {
                var startTime = DateTime.Now;
                
                try
                {
                    if (!_camera.Read(frame) || frame.Empty())
                    {
                        Thread.Sleep(10);
                        continue;
                    }
                    
                    // Convertir en BitmapSource pour l'affichage local
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            var bitmap = frame.ToBitmapSource();
                            bitmap.Freeze();
                            OnLocalVideoFrame?.Invoke(bitmap);
                        }
                        catch { }
                    });
                    
                    // Note: Video encoding disabled - sending raw frames for now
                    // TODO: Re-implement with MixedReality.WebRTC for proper encoding
                    lock (_encoderLock)
                    {
                        // Raw frame sending disabled - need proper WebRTC implementation
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke($"Erreur capture: {ex.Message}");
                }
                
                // Maintenir le framerate
                var elapsed = DateTime.Now - startTime;
                if (elapsed < targetInterval)
                {
                    Thread.Sleep(targetInterval - elapsed);
                }
            }
            
            frame.Dispose();
        }

        private void SendVideoFrameToPeers(byte[] encodedFrame)
        {
            foreach (var pc in _peerConnections.Values)
            {
                try
                {
                    // Envoyer via le data channel ou video track
                    // Note: L'implémentation exacte dépend de SIPSorcery
                }
                catch { }
            }
        }

        #endregion

        #region WebRTC Peer Management

        private async Task InitiatePeerConnection(int peerId, bool isInitiator)
        {
            if (_peerConnections.ContainsKey(peerId)) return;
            
            try
            {
                var config = new RTCConfiguration
                {
                    iceServers = _iceServers
                };
                
                var pc = new RTCPeerConnection(config);
                
                // Events
                pc.onicecandidate += (candidate) =>
                {
                    if (candidate != null)
                    {
                        _hubConnection.SendAsync("SendRoomVideoIceCandidate", 
                            _roomId, peerId, candidate.candidate, 
                            (int)candidate.sdpMLineIndex, candidate.sdpMid ?? "");
                    }
                };
                
                pc.onconnectionstatechange += (state) =>
                {
                    OnStatusChanged?.Invoke($"Peer {peerId}: {state}");
                    
                    if (state == RTCPeerConnectionState.failed || 
                        state == RTCPeerConnectionState.disconnected)
                    {
                        ClosePeerConnection(peerId);
                    }
                };
                
                pc.OnVideoFrameReceived += (endpoint, timestamp, frame, format) =>
                {
                    ProcessRemoteVideoFrame(peerId, frame, format);
                };
                
                _peerConnections.TryAdd(peerId, pc);
                
                if (isInitiator)
                {
                    // Ajouter la track vidéo (VP8) - format dynamique avec ID 96
                    var videoFormat = new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000);
                    var videoTrack = new MediaStreamTrack(
                        SDPMediaTypesEnum.video,
                        false,
                        new List<SDPAudioVideoMediaFormat> { videoFormat },
                        MediaStreamStatusEnum.SendRecv);
                    
                    pc.addTrack(videoTrack);
                    
                    // Créer l'offre
                    var offer = pc.createOffer();
                    await pc.setLocalDescription(offer);
                    
                    await _hubConnection.SendAsync("SendRoomVideoOffer", _roomId, peerId, offer.sdp);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur connexion peer {peerId}: {ex.Message}");
            }
        }

        private async Task HandleVideoOffer(int fromUserId, string sdp)
        {
            try
            {
                if (!_peerConnections.ContainsKey(fromUserId))
                {
                    await InitiatePeerConnection(fromUserId, false);
                }
                
                if (_peerConnections.TryGetValue(fromUserId, out var pc))
                {
                    var offer = new RTCSessionDescriptionInit
                    {
                        type = RTCSdpType.offer,
                        sdp = sdp
                    };
                    
                    pc.setRemoteDescription(offer);
                    
                    // Créer la réponse
                    var answer = pc.createAnswer();
                    await pc.setLocalDescription(answer);
                    
                    await _hubConnection.SendAsync("SendRoomVideoAnswer", _roomId, fromUserId, answer.sdp);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur traitement offre: {ex.Message}");
            }
        }

        private async Task HandleVideoAnswer(int fromUserId, string sdp)
        {
            try
            {
                if (_peerConnections.TryGetValue(fromUserId, out var pc))
                {
                    var answer = new RTCSessionDescriptionInit
                    {
                        type = RTCSdpType.answer,
                        sdp = sdp
                    };
                    
                    pc.setRemoteDescription(answer);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur traitement réponse: {ex.Message}");
            }
        }

        private void HandleIceCandidate(int fromUserId, string candidate, int sdpMLineIndex, string sdpMid)
        {
            try
            {
                if (_peerConnections.TryGetValue(fromUserId, out var pc))
                {
                    var iceCandidate = new RTCIceCandidateInit
                    {
                        candidate = candidate,
                        sdpMLineIndex = (ushort)sdpMLineIndex,
                        sdpMid = sdpMid
                    };
                    
                    pc.addIceCandidate(iceCandidate);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur ICE candidate: {ex.Message}");
            }
        }

        private void ClosePeerConnection(int peerId)
        {
            if (_peerConnections.TryRemove(peerId, out var pc))
            {
                try
                {
                    pc.close();
                }
                catch { }
            }
            
            _remoteVideoFrames.TryRemove(peerId, out _);
        }

        private void ProcessRemoteVideoFrame(int peerId, byte[] encodedFrame, VideoFormat format)
        {
            // Note: Video decoding disabled - need WebRTC implementation
            // TODO: Re-implement with MixedReality.WebRTC for proper decoding
        }

        #endregion

        #region Helpers

        private void NotifyActiveCamerasChanged()
        {
            var activeIds = _remotePeers
                .Where(p => p.Value.IsCameraActive)
                .Select(p => p.Key)
                .ToList();
            
            if (_isCameraEnabled)
            {
                activeIds.Insert(0, _currentUserId);
            }
            
            OnActiveCamerasChanged?.Invoke(activeIds);
        }

        /// <summary>
        /// Met à jour la limite de caméras (pour changement d'abonnement)
        /// </summary>
        public void UpdateCameraLimit(bool isPremium)
        {
            _maxCameras = isPremium ? PREMIUM_MAX_CAMERAS : DEFAULT_MAX_CAMERAS;
        }

        /// <summary>
        /// Obtient la frame vidéo d'un peer
        /// </summary>
        public BitmapSource? GetPeerVideoFrame(int peerId)
        {
            _remoteVideoFrames.TryGetValue(peerId, out var frame);
            return frame;
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            
            _isCameraRunning = false;
            _cameraThread?.Join(1000);
            
            _camera?.Release();
            _camera?.Dispose();
            
            // Note: Video encoder cleanup disabled
            
            foreach (var pc in _peerConnections.Values)
            {
                try { pc.close(); } catch { }
            }
            _peerConnections.Clear();
            
            _connectionLock.Dispose();
        }

        #endregion
    }

    #region Models

    public class RemotePeerInfo
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public bool IsCameraActive { get; set; }
    }

    public class RoomCameraInfo
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
    }

    #endregion
}

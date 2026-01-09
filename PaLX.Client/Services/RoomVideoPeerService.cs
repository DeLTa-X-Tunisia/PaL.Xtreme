// Copyright (c) 2026 Azizi Mounir. All rights reserved.
// RoomVideoPeerService - Service WebRTC simplifié pour vidéo multi-peer en chatroom
// Basé sur VideoCallService optimisé pour un chargement instantané

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.AspNetCore.SignalR.Client;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace PaLX.Client.Services
{
    /// <summary>
    /// Service simplifié pour la gestion vidéo WebRTC dans les chatrooms
    /// Optimisé pour un démarrage rapide (~2-3 secondes)
    /// </summary>
    public class RoomVideoPeerService : IDisposable
    {
        #region Constants

        private const int DEFAULT_MAX_CAMERAS = 6;
        private const int PREMIUM_MAX_CAMERAS = 8;

        #endregion

        #region Fields

        private readonly HubConnection _hubConnection;
        private readonly int _roomId;
        private readonly int _currentUserId;
        private readonly string _currentUsername;
        
        // Camera capture (réutilise la logique VideoCallService)
        private VideoCapture? _camera;
        private Thread? _cameraThread;
        private volatile bool _isCameraRunning;
        // Note: Video encoding disabled - using raw frames for now
        private readonly object _encoderLock = new();
        
        // Remote peers
        private readonly ConcurrentDictionary<int, RemotePeerState> _remotePeers = new();
        
        // State
        private bool _isCameraEnabled = false;
        private bool _isDisposed = false;
        private int _maxCameras;

        #endregion

        #region Events

        /// <summary>Frame vidéo locale disponible</summary>
        public event Action<BitmapSource?>? OnLocalVideoFrame;
        
        /// <summary>Frame vidéo distante disponible (userId, frame)</summary>
        public event Action<int, BitmapSource?>? OnRemoteVideoFrame;
        
        /// <summary>Peer a activé sa caméra (userId, username)</summary>
        public event Action<int, string>? OnPeerCameraStarted;
        
        /// <summary>Peer a désactivé sa caméra</summary>
        public event Action<int>? OnPeerCameraStopped;
        
        /// <summary>Erreur</summary>
        public event Action<string>? OnError;
        
        /// <summary>Status changed</summary>
        public event Action<string>? OnStatusChanged;

        #endregion

        #region Properties

        public bool IsCameraEnabled => _isCameraEnabled;
        public int ActiveCameraCount => _remotePeers.Count + (_isCameraEnabled ? 1 : 0);
        public int MaxCameras => _maxCameras;

        #endregion

        #region Constructor

        public RoomVideoPeerService(HubConnection hubConnection, int roomId, int userId, string username, bool isPremium = false)
        {
            _hubConnection = hubConnection;
            _roomId = roomId;
            _currentUserId = userId;
            _currentUsername = username;
            _maxCameras = isPremium ? PREMIUM_MAX_CAMERAS : DEFAULT_MAX_CAMERAS;
            
            InitializeSignalR();
        }

        #endregion

        #region SignalR Events

        private void InitializeSignalR()
        {
            // Peer a activé sa caméra
            _hubConnection.On<int, int, string>("RoomCameraStarted", (roomId, userId, username) =>
            {
                if (roomId != _roomId || userId == _currentUserId) return;
                
                _remotePeers.TryAdd(userId, new RemotePeerState { UserId = userId, Username = username });
                OnPeerCameraStarted?.Invoke(userId, username);
                OnStatusChanged?.Invoke($"{username} a activé sa caméra");
            });
            
            // Peer a désactivé sa caméra
            _hubConnection.On<int, int>("RoomCameraStopped", (roomId, userId) =>
            {
                if (roomId != _roomId) return;
                
                _remotePeers.TryRemove(userId, out _);
                OnPeerCameraStopped?.Invoke(userId);
            });
            
            // Frame vidéo reçue d'un peer (pour affichage)
            _hubConnection.On<int, int, byte[]>("RoomVideoFrame", (roomId, userId, frameData) =>
            {
                if (roomId != _roomId || userId == _currentUserId) return;
                
                // Décoder et afficher
                ProcessReceivedFrame(userId, frameData);
            });
        }

        #endregion

        #region Camera Control

        /// <summary>
        /// Démarre la caméra locale - Optimisé pour démarrage rapide
        /// </summary>
        public async Task StartCameraAsync()
        {
            if (_isCameraEnabled) return;
            
            try
            {
                // Vérifier limite
                if (ActiveCameraCount >= _maxCameras)
                {
                    OnError?.Invoke($"Limite de {_maxCameras} caméras atteinte");
                    return;
                }
                
                // Ouvrir la caméra - Configuration optimisée pour démarrage rapide
                int cameraIndex = SettingsService.SelectedCameraIndex;
                _camera = new VideoCapture(cameraIndex);
                
                if (!_camera.IsOpened())
                {
                    OnError?.Invoke("Impossible d'accéder à la caméra");
                    return;
                }
                
                // Configuration minimale pour démarrage rapide
                var quality = SettingsService.CurrentVideoQuality;
                _camera.Set(VideoCaptureProperties.FrameWidth, quality.Width);
                _camera.Set(VideoCaptureProperties.FrameHeight, quality.Height);
                _camera.Set(VideoCaptureProperties.Fps, quality.Fps);
                _camera.Set(VideoCaptureProperties.BufferSize, 1); // Réduire le lag
                
                // Note: Video encoding disabled for now - using raw frames
                
                _isCameraEnabled = true;
                _isCameraRunning = true;
                
                // Démarrer le thread de capture
                _cameraThread = new Thread(CameraCaptureLoop)
                {
                    IsBackground = true,
                    Name = "RoomVideoCameraThread",
                    Priority = ThreadPriority.AboveNormal
                };
                _cameraThread.Start();
                
                // Notifier le serveur
                await _hubConnection.SendAsync("StartRoomCamera", _roomId);
                
                OnStatusChanged?.Invoke("Caméra activée");
            }
            catch (Exception ex)
            {
                _isCameraEnabled = false;
                OnError?.Invoke($"Erreur démarrage caméra: {ex.Message}");
                throw;
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
                _isCameraRunning = false;
                _isCameraEnabled = false;
                
                // Attendre la fin du thread
                if (_cameraThread != null && _cameraThread.IsAlive)
                {
                    _cameraThread.Join(1000);
                }
                _cameraThread = null;
                
                // Libérer la caméra
                try
                {
                    _camera?.Release();
                    _camera?.Dispose();
                }
                catch { }
                _camera = null;
                
                // Note: Video encoder cleanup disabled
                
                // Notifier le serveur
                await _hubConnection.SendAsync("StopRoomCamera", _roomId);
                
                OnStatusChanged?.Invoke("Caméra désactivée");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur arrêt caméra: {ex.Message}");
            }
        }

        #endregion

        #region Camera Capture Loop

        private void CameraCaptureLoop()
        {
            using var frame = new Mat();
            var quality = SettingsService.CurrentVideoQuality;
            var frameInterval = TimeSpan.FromMilliseconds(1000.0 / quality.Fps);
            var lastFrameTime = DateTime.Now;

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

                    // Preview local (UI thread)
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

                    // Encoder en JPEG et envoyer aux autres participants
                    SendJpegFrame(frame);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RoomVideo] Capture error: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }

        private void SendJpegFrame(Mat frame)
        {
            try
            {
                // Réduire la qualité pour la transmission
                var quality = SettingsService.CurrentVideoQuality;
                int jpegQuality = quality.Bitrate > 1000000 ? 70 : 50; // Qualité adaptée au bitrate
                
                // Encoder en JPEG
                var encodeParams = new ImageEncodingParam[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, jpegQuality) };
                Cv2.ImEncode(".jpg", frame, out var jpegData, encodeParams);
                
                if (jpegData != null && jpegData.Length > 0)
                {
                    // Envoyer via SignalR (limiter à ~100KB max)
                    if (jpegData.Length < 100000)
                    {
                        _ = _hubConnection.SendAsync("SendRoomVideoFrame", _roomId, jpegData);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RoomVideo] SendJpeg error: {ex.Message}");
            }
        }

        private void SendEncodedFrame(Mat frame)
        {
            // Note: Video encoding disabled - using JPEG compression via SendJpegFrame instead
            // TODO: Re-implement with MixedReality.WebRTC for proper encoding
        }

        #endregion

        #region Frame Processing

        private void ProcessReceivedFrame(int userId, byte[] frameData)
        {
            try
            {
                // Décoder le JPEG reçu
                using var mat = Cv2.ImDecode(frameData, ImreadModes.Color);
                
                if (mat != null && !mat.Empty())
                {
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            var bitmap = mat.ToBitmapSource();
                            bitmap.Freeze();
                            OnRemoteVideoFrame?.Invoke(userId, bitmap);
                        }
                        catch { }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RoomVideo] Decode error: {ex.Message}");
            }
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            
            _ = StopCameraAsync();
            
            _remotePeers.Clear();
            
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Nested Types

        private class RemotePeerState
        {
            public int UserId { get; set; }
            public string Username { get; set; } = string.Empty;
        }

        #endregion
    }
}

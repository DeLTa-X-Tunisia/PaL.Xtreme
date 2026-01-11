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
    /// Utilise les événements centralisés d'ApiService pour éviter les problèmes SignalR
    /// </summary>
    public class RoomVideoPeerService : IDisposable
    {
        #region Constants

        private const int DEFAULT_MAX_CAMERAS = 6;
        private const int PREMIUM_MAX_CAMERAS = 8;
        private const int MAX_PENDING_FRAMES = 2; // Limite les frames en attente d'envoi

        #endregion

        #region Fields

        private readonly ApiService _apiService;
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
        
        // Frame sending - évite saturation du ThreadPool
        private volatile int _pendingFrames = 0;
        private readonly SemaphoreSlim _frameSemaphore = new(MAX_PENDING_FRAMES, MAX_PENDING_FRAMES);
        
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

        public RoomVideoPeerService(ApiService apiService, int roomId, int userId, string username, bool isPremium = false)
        {
            _apiService = apiService;
            // Utiliser explicitement RoomHubConnection pour les opérations de chatroom
            _hubConnection = apiService.RoomHubConnection ?? throw new InvalidOperationException("RoomHubConnection is not available");
            _roomId = roomId;
            _currentUserId = userId;
            _currentUsername = username;
            _maxCameras = isPremium ? PREMIUM_MAX_CAMERAS : DEFAULT_MAX_CAMERAS;
            
            System.Diagnostics.Debug.WriteLine($"[RoomVideo] Service created: roomId={roomId}, userId={userId}, hubState={_hubConnection.State}");
            
            InitializeSignalR();
        }

        #endregion

        #region SignalR Events

        private void InitializeSignalR()
        {
            // S'abonner aux événements centralisés d'ApiService (pas directement à la connexion SignalR)
            // Cela évite les problèmes de handlers multiples sur la connexion
            _apiService.OnRoomCameraStarted += HandleCameraStarted;
            _apiService.OnRoomCameraStopped += HandleCameraStopped;
            _apiService.OnRoomVideoFrame += HandleVideoFrame;
        }
        
        private void HandleCameraStarted(int roomId, int userId, string username)
        {
            // Ignorer si disposed, mauvaise room, ou propre événement
            if (_isDisposed || roomId != _roomId || userId == _currentUserId) return;
            
            _remotePeers.TryAdd(userId, new RemotePeerState { UserId = userId, Username = username });
            OnPeerCameraStarted?.Invoke(userId, username);
            OnStatusChanged?.Invoke($"{username} a activé sa caméra");
        }
        
        private void HandleCameraStopped(int roomId, int userId)
        {
            // Ignorer si disposed ou mauvaise room
            if (_isDisposed || roomId != _roomId) return;
            
            _remotePeers.TryRemove(userId, out _);
            OnPeerCameraStopped?.Invoke(userId);
        }
        
        private void HandleVideoFrame(int roomId, int userId, byte[] frameData)
        {
            // Ignorer si disposed, mauvaise room, ou propre frame
            if (_isDisposed || roomId != _roomId || userId == _currentUserId) return;
            
            System.Diagnostics.Debug.WriteLine($"[RoomVideo] Received frame from user {userId}, size={frameData.Length}");
            
            // Décoder et afficher
            ProcessReceivedFrame(userId, frameData);
        }

        #endregion

        #region Camera Control

        /// <summary>
        /// Démarre la caméra locale - Optimisé pour démarrage rapide et non-bloquant
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
                
                _isCameraEnabled = true;
                _isCameraRunning = true;
                
                // Notifier le serveur AVANT de démarrer la capture
                if (_hubConnection.State == HubConnectionState.Connected)
                {
                    try
                    {
                        await _hubConnection.SendAsync("StartRoomCamera", _roomId).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RoomVideo] StartRoomCamera error: {ex.Message}");
                    }
                }
                
                // Démarrer le thread de capture qui initialisera la caméra (évite blocage UI)
                _cameraThread = new Thread(CameraCaptureLoopWithInit)
                {
                    IsBackground = true,
                    Name = "RoomVideoCameraThread",
                    Priority = ThreadPriority.AboveNormal
                };
                _cameraThread.Start();
                
                OnStatusChanged?.Invoke("Caméra en cours d'activation...");
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
                
                // Notifier le serveur seulement si connecté
                if (_hubConnection.State == HubConnectionState.Connected)
                {
                    try
                    {
                        await _hubConnection.SendAsync("StopRoomCamera", _roomId).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RoomVideo] StopRoomCamera error: {ex.Message}");
                    }
                }
                
                OnStatusChanged?.Invoke("Caméra désactivée");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Erreur arrêt caméra: {ex.Message}");
            }
        }

        #endregion

        #region Camera Capture Loop

        /// <summary>
        /// Boucle de capture avec initialisation de la caméra dans le thread (non-bloquant pour UI)
        /// </summary>
        private void CameraCaptureLoopWithInit()
        {
            try
            {
                // Initialiser la caméra DANS le thread de capture (évite blocage UI)
                int cameraIndex = SettingsService.SelectedCameraIndex;
                _camera = new VideoCapture(cameraIndex);
                
                if (!_camera.IsOpened())
                {
                    _isCameraEnabled = false;
                    _isCameraRunning = false;
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        OnError?.Invoke("Impossible d'accéder à la caméra");
                    });
                    return;
                }
                
                // Configuration minimale pour démarrage rapide
                var quality = SettingsService.CurrentVideoQuality;
                _camera.Set(VideoCaptureProperties.FrameWidth, quality.Width);
                _camera.Set(VideoCaptureProperties.FrameHeight, quality.Height);
                _camera.Set(VideoCaptureProperties.Fps, quality.Fps);
                _camera.Set(VideoCaptureProperties.BufferSize, 1);
                
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    OnStatusChanged?.Invoke("Caméra activée");
                });
                
                // Démarrer la boucle de capture
                CameraCaptureLoop();
            }
            catch (Exception ex)
            {
                _isCameraEnabled = false;
                _isCameraRunning = false;
                System.Diagnostics.Debug.WriteLine($"[RoomVideo] Camera init error: {ex.Message}");
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    OnError?.Invoke($"Erreur initialisation caméra: {ex.Message}");
                });
            }
        }
        
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

                    // Preview local (UI thread) - copier les données pour éviter race condition
                    try
                    {
                        using var frameCopy = frame.Clone();
                        var bitmap = frameCopy.ToBitmapSource();
                        bitmap.Freeze();
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            OnLocalVideoFrame?.Invoke(bitmap);
                        });
                    }
                    catch { }

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
            // Skip si trop de frames en attente (évite saturation du ThreadPool)
            if (_pendingFrames >= MAX_PENDING_FRAMES) return;
            
            // Skip si connexion non active
            if (_hubConnection.State != HubConnectionState.Connected) return;
            
            try
            {
                // Réduire la qualité pour la transmission
                var quality = SettingsService.CurrentVideoQuality;
                int jpegQuality = quality.Bitrate > 1000000 ? 60 : 40; // Qualité réduite pour performance
                
                // Encoder en JPEG
                var encodeParams = new ImageEncodingParam[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, jpegQuality) };
                Cv2.ImEncode(".jpg", frame, out var jpegData, encodeParams);
                
                if (jpegData != null && jpegData.Length > 0 && jpegData.Length < 80000)
                {
                    // Incrémenter le compteur de frames en attente
                    Interlocked.Increment(ref _pendingFrames);
                    
                    // Envoyer de manière asynchrone sans bloquer
                    _ = SendFrameAsync(jpegData);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RoomVideo] SendJpeg error: {ex.Message}");
            }
        }
        
        private async Task SendFrameAsync(byte[] frameData)
        {
            try
            {
                if (_hubConnection.State == HubConnectionState.Connected && !_isDisposed)
                {
                    System.Diagnostics.Debug.WriteLine($"[RoomVideo] Sending frame to room {_roomId}, size={frameData.Length}, hubState={_hubConnection.State}");
                    await _hubConnection.SendAsync("SendRoomVideoFrame", _roomId, frameData).ConfigureAwait(false);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[RoomVideo] Cannot send frame: hubState={_hubConnection.State}, disposed={_isDisposed}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RoomVideo] Frame send error: {ex.Message}");
            }
            finally
            {
                // Toujours décrémenter, même en cas d'erreur
                Interlocked.Decrement(ref _pendingFrames);
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
                    System.Diagnostics.Debug.WriteLine($"[RoomVideo] Decoded frame for user {userId}, size={mat.Width}x{mat.Height}");
                    
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            var bitmap = mat.ToBitmapSource();
                            bitmap.Freeze();
                            System.Diagnostics.Debug.WriteLine($"[RoomVideo] Invoking OnRemoteVideoFrame for user {userId}");
                            OnRemoteVideoFrame?.Invoke(userId, bitmap);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[RoomVideo] Bitmap conversion error: {ex.Message}");
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[RoomVideo] Failed to decode frame for user {userId}");
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
            
            // Désabonner proprement des événements ApiService (événements C#, pas SignalR)
            // Cela ne touche pas à la connexion SignalR elle-même
            try
            {
                _apiService.OnRoomCameraStarted -= HandleCameraStarted;
                _apiService.OnRoomCameraStopped -= HandleCameraStopped;
                _apiService.OnRoomVideoFrame -= HandleVideoFrame;
            }
            catch { }
            
            // Arrêter la caméra de manière synchrone si possible
            _isCameraRunning = false;
            _isCameraEnabled = false;
            
            try
            {
                _cameraThread?.Join(1000);
                _camera?.Release();
                _camera?.Dispose();
            }
            catch { }
            
            // Nettoyer le sémaphore
            try
            {
                _frameSemaphore.Dispose();
            }
            catch { }
            
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

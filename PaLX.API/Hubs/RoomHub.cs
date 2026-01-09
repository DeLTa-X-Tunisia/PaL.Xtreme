using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Concurrent;

namespace PaLX.API.Hubs
{
    [Authorize]
    public class RoomHub : Hub
    {
        // Track active cameras per room: RoomId -> { UserId -> Username }
        private static readonly ConcurrentDictionary<int, ConcurrentDictionary<int, string>> _roomCameras = new();
        
        // Track user connections: ConnectionId -> (UserId, RoomId)
        private static readonly ConcurrentDictionary<string, (int UserId, int RoomId)> _userConnections = new();

        public async Task JoinRoomGroup(int roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{roomId}");
            
            // Stocker la connexion
            var userId = GetUserId();
            _userConnections[Context.ConnectionId] = (userId, roomId);
            
            // Envoyer la liste des caméras actives au nouveau membre
            if (_roomCameras.TryGetValue(roomId, out var cameras))
            {
                var cameraList = cameras.Select(c => new { UserId = c.Key, Username = c.Value }).ToList();
                await Clients.Caller.SendAsync("RoomActiveCameras", roomId, cameraList);
            }
        }

        public async Task LeaveRoomGroup(int roomId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Room_{roomId}");
            
            // Nettoyer la connexion
            _userConnections.TryRemove(Context.ConnectionId, out _);
            
            // Si l'utilisateur avait sa caméra active, la retirer
            var userId = GetUserId();
            if (_roomCameras.TryGetValue(roomId, out var cameras))
            {
                if (cameras.TryRemove(userId, out _))
                {
                    await Clients.Group($"Room_{roomId}").SendAsync("RoomCameraStopped", roomId, userId);
                }
            }
        }

        #region Video Signaling

        /// <summary>
        /// Un utilisateur démarre sa caméra dans le room
        /// </summary>
        public async Task StartRoomCamera(int roomId)
        {
            var userId = GetUserId();
            var username = GetUsername();
            
            // Ajouter aux caméras actives
            var cameras = _roomCameras.GetOrAdd(roomId, _ => new ConcurrentDictionary<int, string>());
            cameras[userId] = username;
            
            // Notifier tous les membres du room
            await Clients.Group($"Room_{roomId}").SendAsync("RoomCameraStarted", roomId, userId, username);
        }

        /// <summary>
        /// Un utilisateur arrête sa caméra dans le room
        /// </summary>
        public async Task StopRoomCamera(int roomId)
        {
            var userId = GetUserId();
            
            // Retirer des caméras actives
            if (_roomCameras.TryGetValue(roomId, out var cameras))
            {
                cameras.TryRemove(userId, out _);
            }
            
            // Notifier tous les membres du room
            await Clients.Group($"Room_{roomId}").SendAsync("RoomCameraStopped", roomId, userId);
        }

        /// <summary>
        /// Envoyer une offre WebRTC à un peer spécifique
        /// </summary>
        public async Task SendRoomVideoOffer(int roomId, int targetUserId, string sdp)
        {
            var fromUserId = GetUserId();
            
            // Trouver la connexion du destinataire dans ce room
            var targetConnection = _userConnections
                .FirstOrDefault(c => c.Value.UserId == targetUserId && c.Value.RoomId == roomId);
            
            if (!string.IsNullOrEmpty(targetConnection.Key))
            {
                await Clients.Client(targetConnection.Key).SendAsync("RoomVideoOffer", roomId, fromUserId, sdp);
            }
        }

        /// <summary>
        /// Envoyer une réponse WebRTC à un peer spécifique
        /// </summary>
        public async Task SendRoomVideoAnswer(int roomId, int targetUserId, string sdp)
        {
            var fromUserId = GetUserId();
            
            var targetConnection = _userConnections
                .FirstOrDefault(c => c.Value.UserId == targetUserId && c.Value.RoomId == roomId);
            
            if (!string.IsNullOrEmpty(targetConnection.Key))
            {
                await Clients.Client(targetConnection.Key).SendAsync("RoomVideoAnswer", roomId, fromUserId, sdp);
            }
        }

        /// <summary>
        /// Envoyer un candidat ICE à un peer spécifique
        /// </summary>
        public async Task SendRoomVideoIceCandidate(int roomId, int targetUserId, string candidate, int sdpMLineIndex, string sdpMid)
        {
            var fromUserId = GetUserId();
            
            var targetConnection = _userConnections
                .FirstOrDefault(c => c.Value.UserId == targetUserId && c.Value.RoomId == roomId);
            
            if (!string.IsNullOrEmpty(targetConnection.Key))
            {
                await Clients.Client(targetConnection.Key).SendAsync("RoomVideoIceCandidate", roomId, fromUserId, candidate, sdpMLineIndex, sdpMid);
            }
        }

        /// <summary>
        /// Obtenir le nombre de caméras actives dans un room
        /// </summary>
        public Task<int> GetActiveCameraCount(int roomId)
        {
            if (_roomCameras.TryGetValue(roomId, out var cameras))
            {
                return Task.FromResult(cameras.Count);
            }
            return Task.FromResult(0);
        }

        /// <summary>
        /// Envoyer une frame vidéo encodée à tous les membres du room
        /// </summary>
        public async Task SendRoomVideoFrame(int roomId, byte[] frameData)
        {
            var userId = GetUserId();
            
            // Envoyer à tous les autres membres du room (sauf l'expéditeur)
            await Clients.OthersInGroup($"Room_{roomId}").SendAsync("RoomVideoFrame", roomId, userId, frameData);
        }

        /// <summary>
        /// Demander le flux vidéo d'un peer spécifique
        /// </summary>
        public async Task RequestPeerVideoStream(int roomId, int peerId)
        {
            var requesterId = GetUserId();
            
            // Notifier le peer qu'on veut voir sa vidéo
            var peerConnection = _userConnections
                .FirstOrDefault(c => c.Value.UserId == peerId && c.Value.RoomId == roomId);
            
            if (!string.IsNullOrEmpty(peerConnection.Key))
            {
                await Clients.Client(peerConnection.Key).SendAsync("VideoStreamRequested", roomId, requesterId);
            }
        }

        #endregion

        #region Helpers

        private int GetUserId()
        {
            var claim = Context.User?.FindFirst("userId") ?? Context.User?.FindFirst("sub");
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        private string GetUsername()
        {
            return Context.User?.Identity?.Name ?? "Unknown";
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Nettoyer quand un utilisateur se déconnecte
            if (_userConnections.TryRemove(Context.ConnectionId, out var info))
            {
                var (userId, roomId) = info;
                
                // Retirer la caméra si active
                if (_roomCameras.TryGetValue(roomId, out var cameras))
                {
                    if (cameras.TryRemove(userId, out _))
                    {
                        await Clients.Group($"Room_{roomId}").SendAsync("RoomCameraStopped", roomId, userId);
                    }
                }
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        #endregion
    }
}

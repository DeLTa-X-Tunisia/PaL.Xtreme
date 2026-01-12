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
        
        // Track user connections: ConnectionId -> (UserId, RoomId, RoleLevel)
        private static readonly ConcurrentDictionary<string, (int UserId, int RoomId, int RoleLevel)> _userConnections = new();
        
        // Rôles système qui peuvent voir tous les chuchotements (RoleLevel 1-6)
        // 1=ServerMaster, 2=ServerEditor, 3=ServerSuperAdmin, 4=ServerAdmin, 5=ServerModerator, 6=ServerHelp
        private const int MaxModeratorRoleLevel = 6;

        public async Task JoinRoomGroup(int roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{roomId}");
            
            // Stocker la connexion avec le RoleLevel
            var userId = GetUserId();
            var roleLevel = GetRoleLevel();
            _userConnections[Context.ConnectionId] = (userId, roomId, roleLevel);
            
            Console.WriteLine($"[RoomHub.JoinRoomGroup] User {userId} (RoleLevel={roleLevel}) joined room {roomId}, ConnectionId: {Context.ConnectionId.Substring(0, Math.Min(8, Context.ConnectionId.Length))}...");
            Console.WriteLine($"[RoomHub.JoinRoomGroup] Total connections now: {_userConnections.Count}");
            
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
            
            Console.WriteLine($"[RoomHub] SendRoomVideoFrame: roomId={roomId}, userId={userId}, size={frameData?.Length ?? 0}");
            
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

        #region Whisper (Private Messages in Room)

        /// <summary>
        /// Envoyer un chuchotement privé à un utilisateur dans le room
        /// </summary>
        public async Task SendWhisper(int roomId, int targetUserId, string message, string senderDisplayName)
        {
            var fromUserId = GetUserId();
            var fromUsername = GetUsername();
            var targetDisplayName = ""; // Pour informer les modérateurs
            
            Console.WriteLine($"[RoomHub.SendWhisper] From: {fromUserId} ({senderDisplayName}), To: {targetUserId}, Room: {roomId}");
            Console.WriteLine($"[RoomHub.SendWhisper] Total connections tracked: {_userConnections.Count}");
            
            // Trouver la connexion du destinataire dans ce room
            var targetConnection = _userConnections
                .FirstOrDefault(c => c.Value.UserId == targetUserId && c.Value.RoomId == roomId);
            
            Console.WriteLine($"[RoomHub.SendWhisper] Target connection found: {!string.IsNullOrEmpty(targetConnection.Key)}");
            
            if (!string.IsNullOrEmpty(targetConnection.Key))
            {
                Console.WriteLine($"[RoomHub.SendWhisper] Sending WhisperReceived to target user {targetUserId}");
                // Envoyer au destinataire
                await Clients.Client(targetConnection.Key).SendAsync("WhisperReceived", roomId, fromUserId, senderDisplayName, message);
            }
            else
            {
                Console.WriteLine($"[RoomHub.SendWhisper] WARNING: No connection found for target user {targetUserId} in room {roomId}");
            }
            
            // Envoyer aux modérateurs/admins système (RoleLevel 1-6) qui sont dans ce room
            // Ils voient tous les chuchotements pour la modération
            var moderatorConnections = _userConnections
                .Where(c => c.Value.RoomId == roomId 
                         && c.Value.RoleLevel >= 1 
                         && c.Value.RoleLevel <= MaxModeratorRoleLevel
                         && c.Value.UserId != fromUserId      // Pas l'expéditeur (il voit déjà via WhisperSent)
                         && c.Value.UserId != targetUserId)   // Pas le destinataire (il voit déjà via WhisperReceived)
                .ToList();
            
            Console.WriteLine($"[RoomHub.SendWhisper] Found {moderatorConnections.Count} moderator(s) to notify");
            
            foreach (var modConn in moderatorConnections)
            {
                Console.WriteLine($"[RoomHub.SendWhisper] Sending WhisperModView to moderator UserId={modConn.Value.UserId} (RoleLevel={modConn.Value.RoleLevel})");
                // Envoyer une version spéciale pour les modérateurs qui montre expéditeur ET destinataire
                await Clients.Client(modConn.Key).SendAsync("WhisperModView", roomId, fromUserId, senderDisplayName, targetUserId, message);
            }
            
            // Confirmer à l'expéditeur que le message a été envoyé
            await Clients.Caller.SendAsync("WhisperSent", roomId, targetUserId, message);
            Console.WriteLine($"[RoomHub.SendWhisper] WhisperSent confirmation sent to caller");
        }

        #endregion

        #region Helpers
        private int GetUserId()
        {
            // Le claim est "UserId" avec U majuscule (défini dans AuthService.cs)
            var claim = Context.User?.FindFirst("UserId");
            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }
            
            // Fallback: essayer d'autres claims
            claim = Context.User?.FindFirst("userId") ?? Context.User?.FindFirst("sub");
            if (claim != null && int.TryParse(claim.Value, out userId))
            {
                return userId;
            }
            
            Console.WriteLine($"[RoomHub.GetUserId] WARNING: Could not extract UserId from token. Claims: {string.Join(", ", Context.User?.Claims.Select(c => $"{c.Type}={c.Value}") ?? Array.Empty<string>())}");
            return 0;
        }

        private int GetRoleLevel()
        {
            var claim = Context.User?.FindFirst("RoleLevel");
            if (claim != null && int.TryParse(claim.Value, out int roleLevel))
            {
                return roleLevel;
            }
            return 7; // Default: User standard
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
                var (userId, roomId, _) = info;
                
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

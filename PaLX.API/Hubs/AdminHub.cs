using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace PaLX.API.Hubs
{
    [Authorize]
    public class AdminHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> _adminConnections = new();
        private readonly IHubContext<RoomHub> _roomHubContext;
        private readonly IHubContext<ChatHub> _chatHubContext;

        public AdminHub(IHubContext<RoomHub> roomHubContext, IHubContext<ChatHub> chatHubContext)
        {
            _roomHubContext = roomHubContext;
            _chatHubContext = chatHubContext;
        }

        public override async Task OnConnectedAsync()
        {
            var username = Context.User?.Identity?.Name ?? "Unknown";
            _adminConnections[Context.ConnectionId] = username;
            
            await Clients.All.SendAsync("AdminConnected", username);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (_adminConnections.TryRemove(Context.ConnectionId, out var username))
            {
                await Clients.All.SendAsync("AdminDisconnected", username);
            }
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Rejoindre le canal admin pour recevoir les mises à jour en temps réel
        /// </summary>
        public async Task JoinAdminChannel()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            await Clients.Group("Admins").SendAsync("AdminJoined", Context.User?.Identity?.Name);
        }

        /// <summary>
        /// Quitter le canal admin
        /// </summary>
        public async Task LeaveAdminChannel()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
        }

        /// <summary>
        /// Envoyer une notification à tous les admins connectés
        /// </summary>
        public async Task BroadcastToAdmins(string type, object data)
        {
            await Clients.Group("Admins").SendAsync("AdminNotification", type, data);
        }

        /// <summary>
        /// Obtenir le nombre d'admins connectés
        /// </summary>
        public Task<int> GetConnectedAdminsCount()
        {
            return Task.FromResult(_adminConnections.Count);
        }

        /// <summary>
        /// Obtenir la liste des admins connectés
        /// </summary>
        public Task<IEnumerable<string>> GetConnectedAdmins()
        {
            return Task.FromResult(_adminConnections.Values.AsEnumerable());
        }

        /// <summary>
        /// Envoyer une annonce globale à tous les salons et utilisateurs connectés
        /// </summary>
        /// <param name="type">Type d'annonce: "info", "warning", "alert", "success"</param>
        /// <param name="title">Titre de l'annonce</param>
        /// <param name="message">Contenu du message</param>
        public async Task BroadcastGlobalAnnouncement(string type, string title, string message)
        {
            var adminUsername = Context.User?.Identity?.Name ?? "Administrateur";
            var announcement = new
            {
                Type = type,
                Title = title,
                Message = message,
                SentBy = adminUsername,
                Timestamp = DateTime.UtcNow
            };

            // Envoyer à tous les clients connectés au ChatHub
            await _chatHubContext.Clients.All.SendAsync("ReceiveGlobalAnnouncement", announcement);
            
            // Envoyer à tous les clients connectés au RoomHub
            await _roomHubContext.Clients.All.SendAsync("ReceiveGlobalAnnouncement", announcement);
            
            // Notifier les autres admins
            await Clients.Group("Admins").SendAsync("AnnouncementSent", announcement);
        }
    }
}

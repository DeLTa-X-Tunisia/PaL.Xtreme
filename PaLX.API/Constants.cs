namespace PaLX.API
{
    /// <summary>
    /// Constantes centralisées pour l'application PaL.Xtreme API.
    /// Évite les chaînes magiques et facilite la maintenance.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Claims JWT personnalisés utilisés dans les tokens.
        /// </summary>
        public static class JwtClaims
        {
            /// <summary>Identifiant numérique unique de l'utilisateur</summary>
            public const string UserId = "UserId";
            
            /// <summary>Nom d'utilisateur (login)</summary>
            public const string Username = "Username";
            
            /// <summary>Niveau de rôle système (0-6)</summary>
            public const string RoleLevel = "RoleLevel";
            
            /// <summary>Nom du rôle système</summary>
            public const string Role = "Role";
            
            /// <summary>Adresse email</summary>
            public const string Email = "Email";
            
            /// <summary>ID de session pour gestion multi-connexion</summary>
            public const string SessionId = "SessionId";
        }

        /// <summary>
        /// Niveaux de rôles système.
        /// Plus le niveau est bas, plus les permissions sont élevées.
        /// </summary>
        public static class RoleLevels
        {
            /// <summary>Utilisateur standard (aucun privilège)</summary>
            public const int User = 0;
            
            /// <summary>Administrateur principal - tous les droits</summary>
            public const int ServerMaster = 1;
            
            /// <summary>Éditeur de contenu</summary>
            public const int ServerEditor = 2;
            
            /// <summary>Super administrateur</summary>
            public const int ServerSuperAdmin = 3;
            
            /// <summary>Administrateur</summary>
            public const int ServerAdmin = 4;
            
            /// <summary>Modérateur serveur</summary>
            public const int ServerModerator = 5;
            
            /// <summary>Support / Aide</summary>
            public const int ServerHelp = 6;

            /// <summary>
            /// Vérifie si le niveau de rôle est un administrateur système (1-6)
            /// </summary>
            public static bool IsSystemAdmin(int roleLevel) => roleLevel >= 1 && roleLevel <= 6;

            /// <summary>
            /// Retourne le nom du rôle correspondant au niveau
            /// </summary>
            public static string GetRoleName(int roleLevel) => roleLevel switch
            {
                ServerMaster => "Server Master",
                ServerEditor => "Server Editor",
                ServerSuperAdmin => "Super Admin",
                ServerAdmin => "Admin",
                ServerModerator => "Moderator",
                ServerHelp => "Support",
                _ => "User"
            };
        }

        /// <summary>
        /// Rôles dans les salons.
        /// </summary>
        public static class RoomRoles
        {
            public const int Owner = 1;
            public const int SuperAdmin = 2;
            public const int Admin = 3;
            public const int Moderator = 4;
            public const int Member = 5;

            /// <summary>
            /// Vérifie si le rôle peut modérer (kick/mute)
            /// </summary>
            public static bool CanModerate(int roleId) => roleId >= 1 && roleId <= 4;

            /// <summary>
            /// Vérifie si le rôle peut administrer (changer rôles, paramètres)
            /// </summary>
            public static bool CanAdminister(int roleId) => roleId >= 1 && roleId <= 3;
        }

        /// <summary>
        /// Statuts d'affichage des utilisateurs.
        /// </summary>
        public static class UserStatus
        {
            public const int Online = 1;
            public const int Away = 2;
            public const int Busy = 3;
            public const int DoNotDisturb = 4;
            public const int AppearOffline = 5;
            public const int Offline = 6;

            public static string GetDisplayName(int status) => status switch
            {
                Online => "En ligne",
                Away => "Absent",
                Busy => "Occupé",
                DoNotDisturb => "Ne pas déranger",
                AppearOffline => "Apparaître hors ligne",
                Offline => "Hors ligne",
                _ => "Inconnu"
            };
        }

        /// <summary>
        /// Statuts de demande d'ami.
        /// </summary>
        public static class FriendStatus
        {
            public const int Pending = 0;
            public const int Accepted = 1;
            public const int Blocked = 2;
        }

        /// <summary>
        /// Statuts de transfert de fichier.
        /// </summary>
        public static class FileTransferStatus
        {
            public const int Pending = 0;
            public const int Accepted = 1;
            public const int Declined = 2;
        }

        /// <summary>
        /// Types de messages dans les salons.
        /// </summary>
        public static class MessageTypes
        {
            public const string User = "User";
            public const string System = "System";
            public const string Bot = "Bot";
            public const string BotWelcome = "BotWelcome";
            public const string BotWarning = "BotWarning";
            public const string BotQuiz = "BotQuiz";
            public const string Kick = "Kick";
            public const string Ban = "Ban";
        }

        /// <summary>
        /// Sévérité des mots interdits.
        /// </summary>
        public static class BannedWordSeverity
        {
            public const string Warning = "Warning";
            public const string Kick = "Kick";
            public const string Ban = "Ban";
        }

        /// <summary>
        /// Noms des groupes SignalR.
        /// </summary>
        public static class SignalRGroups
        {
            /// <summary>
            /// Génère le nom du groupe SignalR pour un salon.
            /// Format: "Room_{roomId}"
            /// </summary>
            public static string Room(int roomId) => $"Room_{roomId}";

            /// <summary>
            /// Génère le nom du groupe SignalR pour un utilisateur.
            /// Format: "User_{userId}"
            /// </summary>
            public static string User(int userId) => $"User_{userId}";
        }

        /// <summary>
        /// Configuration par défaut du Bot.
        /// </summary>
        public static class BotDefaults
        {
            public const string Name = "Assistant";
            public const string AvatarUrl = "/images/bot-avatar.png";
            public const string WelcomeTemplate = "Bienvenue {username} dans le salon ! 👋";
            public const string WarningTemplate = "⚠️ {username}, merci de respecter les règles du salon.";
            public const string KickTemplate = "❌ {username} a été expulsé pour comportement inapproprié.";
            public const int WarningsBeforeKick = 3;
            public const int WarningResetMinutes = 60;
            public const int QuizIntervalMinutes = 30;
            public const int QuizTimeoutSeconds = 60;
        }

        /// <summary>
        /// Commandes du Bot.
        /// </summary>
        public static class BotCommands
        {
            public const string Help = "!aide";
            public const string Quiz = "!quiz";
            public const string Topic = "!sujet";
            public const string TopicAlias = "!topic";
            public const string Rules = "!regles";
        }

        /// <summary>
        /// Limites de l'application.
        /// </summary>
        public static class Limits
        {
            public const int MaxUsernameLength = 50;
            public const int MaxNicknameLength = 50;
            public const int MaxBioLength = 500;
            public const int MaxRoomNameLength = 100;
            public const int MaxRoomTopicLength = 500;
            public const int MaxMessageLength = 5000;
            public const int MaxFileSize = 50 * 1024 * 1024; // 50 MB
            public const int MaxRoomMembers = 500;
            public const int DefaultRoomMembers = 100;
        }

        /// <summary>
        /// Contextes de visite de profil.
        /// </summary>
        public static class ProfileViewContext
        {
            public const string Room = "room";
            public const string Friends = "friends";
            public const string Search = "search";
            public const string Direct = "direct";
        }
    }
}

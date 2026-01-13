namespace PaLX.API.Models
{
    /// <summary>
    /// Configuration du Bot IA pour un salon
    /// </summary>
    public class BotConfig
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        
        // Identité du Bot
        public string BotName { get; set; } = "PaLX Bot";
        public string BotAvatarUrl { get; set; } = "/images/bot-avatar.png";
        
        // Fonctionnalités activées
        public bool IsEnabled { get; set; } = true;
        public bool WelcomeMessageEnabled { get; set; } = true;
        public bool ModerationEnabled { get; set; } = true;
        public bool QuizEnabled { get; set; } = false;
        public bool MentionResponseEnabled { get; set; } = true;
        public bool TopicSuggestionEnabled { get; set; } = false;
        
        // Messages personnalisés
        public string WelcomeMessageTemplate { get; set; } = "Bienvenue {username} dans le salon ! 👋";
        public string WarningMessageTemplate { get; set; } = "⚠️ {username}, merci de respecter les règles du salon.";
        public string KickMessageTemplate { get; set; } = "❌ {username} a été expulsé pour comportement inapproprié.";
        
        // Paramètres de modération
        public int WarningsBeforeKick { get; set; } = 3;
        public int WarningResetMinutes { get; set; } = 60; // Reset après X minutes
        
        // Paramètres Quiz
        public int QuizIntervalMinutes { get; set; } = 30;
        public int QuizTimeoutSeconds { get; set; } = 60;
        
        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Avertissement donné par le bot à un utilisateur
    /// </summary>
    public class BotWarning
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int UserId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string TriggerWord { get; set; } = string.Empty;
        public string OriginalMessage { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true; // False après reset ou kick
    }

    /// <summary>
    /// Mot interdit dans un salon
    /// </summary>
    public class BannedWord
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public string Word { get; set; } = string.Empty;
        public string Severity { get; set; } = "Warning"; // Warning, Kick, Ban
        public int AddedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Question de quiz
    /// </summary>
    public class QuizQuestion
    {
        public int Id { get; set; }
        public int RoomId { get; set; } // 0 = question globale
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string[] Options { get; set; } = Array.Empty<string>(); // Pour QCM
        public string Category { get; set; } = "General";
        public int Points { get; set; } = 10;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Sujet de discussion proposé par le bot
    /// </summary>
    public class DiscussionTopic
    {
        public int Id { get; set; }
        public int RoomId { get; set; } // 0 = topic global
        public string Topic { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

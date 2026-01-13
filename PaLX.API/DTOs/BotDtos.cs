namespace PaLX.API.DTOs
{
    /// <summary>
    /// DTO pour la configuration du bot
    /// </summary>
    public class BotConfigDto
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public string BotName { get; set; } = "PaLX Bot";
        public string BotAvatarUrl { get; set; } = "/images/bot-avatar.png";
        
        public bool IsEnabled { get; set; } = true;
        public bool WelcomeMessageEnabled { get; set; } = true;
        public bool ModerationEnabled { get; set; } = true;
        public bool QuizEnabled { get; set; } = false;
        public bool MentionResponseEnabled { get; set; } = true;
        public bool TopicSuggestionEnabled { get; set; } = false;
        
        public string WelcomeMessageTemplate { get; set; } = "Bienvenue {username} dans le salon ! 👋";
        public string WarningMessageTemplate { get; set; } = "⚠️ {username}, merci de respecter les règles du salon.";
        public string KickMessageTemplate { get; set; } = "❌ {username} a été expulsé pour comportement inapproprié.";
        
        public int WarningsBeforeKick { get; set; } = 3;
        public int WarningResetMinutes { get; set; } = 60;
        
        public int QuizIntervalMinutes { get; set; } = 30;
        public int QuizTimeoutSeconds { get; set; } = 60;
    }

    /// <summary>
    /// DTO pour créer/modifier la config du bot
    /// </summary>
    public class UpdateBotConfigDto
    {
        public string? BotName { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? WelcomeMessageEnabled { get; set; }
        public bool? ModerationEnabled { get; set; }
        public bool? QuizEnabled { get; set; }
        public bool? MentionResponseEnabled { get; set; }
        public bool? TopicSuggestionEnabled { get; set; }
        
        public string? WelcomeMessageTemplate { get; set; }
        public string? WarningMessageTemplate { get; set; }
        public string? KickMessageTemplate { get; set; }
        
        public int? WarningsBeforeKick { get; set; }
        public int? WarningResetMinutes { get; set; }
        
        public int? QuizIntervalMinutes { get; set; }
        public int? QuizTimeoutSeconds { get; set; }
    }

    /// <summary>
    /// DTO pour ajouter un mot interdit
    /// </summary>
    public class AddBannedWordDto
    {
        public string Word { get; set; } = string.Empty;
        public string Severity { get; set; } = "Warning";
    }

    /// <summary>
    /// DTO pour un mot interdit
    /// </summary>
    public class BannedWordDto
    {
        public int Id { get; set; }
        public string Word { get; set; } = string.Empty;
        public string Severity { get; set; } = "Warning";
        public string AddedByUsername { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO pour un avertissement du bot
    /// </summary>
    public class BotWarningDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string TriggerWord { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO pour une question de quiz
    /// </summary>
    public class QuizQuestionDto
    {
        public int Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string[] Options { get; set; } = Array.Empty<string>();
        public string Category { get; set; } = "General";
        public int Points { get; set; } = 10;
    }

    /// <summary>
    /// DTO pour créer une question de quiz
    /// </summary>
    public class CreateQuizQuestionDto
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string[]? Options { get; set; }
        public string Category { get; set; } = "General";
        public int Points { get; set; } = 10;
    }

    /// <summary>
    /// DTO pour un message envoyé par le bot
    /// </summary>
    public class BotMessageDto
    {
        public string BotName { get; set; } = "PaLX Bot";
        public string BotAvatarUrl { get; set; } = "/images/bot-avatar.png";
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "Bot"; // Bot, BotWarning, BotQuiz, BotWelcome
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// DTO pour un sujet de discussion
    /// </summary>
    public class DiscussionTopicDto
    {
        public int Id { get; set; }
        public string Topic { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
    }
}

using Npgsql;
using PaLX.API.DTOs;
using PaLX.API.Models;
using Microsoft.AspNetCore.SignalR;
using PaLX.API.Hubs;
using System.Text.RegularExpressions;

namespace PaLX.API.Services
{
    public interface IBotService
    {
        // Configuration
        Task<BotConfigDto?> GetBotConfigAsync(int roomId);
        Task<BotConfigDto> CreateOrUpdateBotConfigAsync(int roomId, UpdateBotConfigDto dto, int actorId);
        Task<bool> IsBotEnabledAsync(int roomId);
        
        // Modération
        Task<List<BannedWordDto>> GetBannedWordsAsync(int roomId);
        Task<BannedWordDto> AddBannedWordAsync(int roomId, AddBannedWordDto dto, int actorId);
        Task<bool> RemoveBannedWordAsync(int roomId, int wordId, int actorId);
        Task<(bool isViolation, string? triggerWord, string severity)> CheckMessageForViolationsAsync(int roomId, string message);
        Task<int> GetActiveWarningsCountAsync(int roomId, int userId);
        Task<BotWarningDto> AddWarningAsync(int roomId, int userId, string reason, string triggerWord, string originalMessage);
        Task ResetWarningsAsync(int roomId, int userId);
        
        // Quiz
        Task<List<QuizQuestionDto>> GetQuizQuestionsAsync(int roomId, int count = 10);
        Task<QuizQuestionDto?> GetRandomQuizQuestionAsync(int roomId);
        Task<bool> CheckQuizAnswerAsync(int questionId, string answer);
        Task<QuizQuestionDto> AddQuizQuestionAsync(int roomId, CreateQuizQuestionDto dto);
        
        // Sujets de discussion
        Task<List<DiscussionTopicDto>> GetDiscussionTopicsAsync(int roomId, int count = 10);
        Task<DiscussionTopicDto?> GetRandomTopicAsync(int roomId);
        
        // Actions Bot
        Task SendBotMessageAsync(int roomId, string content, string messageType = "Bot");
        Task SendWelcomeMessageAsync(int roomId, int userId, string username);
        Task SendWarningMessageAsync(int roomId, int userId, string username, string reason);
        Task HandleMentionAsync(int roomId, int userId, string username, string message);
        Task<bool> ProcessUserMessageAsync(int roomId, int userId, string username, string message);
    }

    public class BotService : IBotService
    {
        private readonly string _connectionString;
        private readonly IHubContext<RoomHub> _roomHubContext;
        private readonly ILogger<BotService> _logger;
        private readonly IServiceProvider _serviceProvider;

        // Réponses du bot quand il est mentionné
        private static readonly string[] MentionResponses = new[]
        {
            "Oui {username} ? Je suis là pour vous aider ! 🤖",
            "Vous m'avez appelé {username} ? Comment puis-je vous aider ?",
            "Bonjour {username} ! Que puis-je faire pour vous ? 😊",
            "Hey {username} ! Je suis le bot assistant de ce salon. Besoin d'aide ?",
            "{username}, je suis à votre écoute ! 👂",
            "Présent {username} ! Une question, un quiz ? Dites-moi tout !",
            "Coucou {username} ! Tapez !aide pour voir ce que je peux faire.",
            "{username} a besoin de moi ? Me voilà ! 🙋"
        };

        public BotService(
            IConfiguration configuration, 
            IHubContext<RoomHub> roomHubContext, 
            ILogger<BotService> logger,
            IServiceProvider serviceProvider)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string not found.");
            _roomHubContext = roomHubContext;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }
        
        // Helper pour éviter la dépendance circulaire
        private IRoomService GetRoomService() => _serviceProvider.GetRequiredService<IRoomService>();

        #region Configuration

        public async Task<BotConfigDto?> GetBotConfigAsync(int roomId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT * FROM ""BotConfigs"" WHERE ""RoomId"" = @roomId";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("roomId", roomId);
            
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new BotConfigDto
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    RoomId = reader.GetInt32(reader.GetOrdinal("RoomId")),
                    BotName = reader.GetString(reader.GetOrdinal("BotName")),
                    BotAvatarUrl = reader.GetString(reader.GetOrdinal("BotAvatarUrl")),
                    IsEnabled = reader.GetBoolean(reader.GetOrdinal("IsEnabled")),
                    WelcomeMessageEnabled = reader.GetBoolean(reader.GetOrdinal("WelcomeMessageEnabled")),
                    ModerationEnabled = reader.GetBoolean(reader.GetOrdinal("ModerationEnabled")),
                    QuizEnabled = reader.GetBoolean(reader.GetOrdinal("QuizEnabled")),
                    MentionResponseEnabled = reader.GetBoolean(reader.GetOrdinal("MentionResponseEnabled")),
                    TopicSuggestionEnabled = reader.GetBoolean(reader.GetOrdinal("TopicSuggestionEnabled")),
                    WelcomeMessageTemplate = reader.GetString(reader.GetOrdinal("WelcomeMessageTemplate")),
                    WarningMessageTemplate = reader.GetString(reader.GetOrdinal("WarningMessageTemplate")),
                    KickMessageTemplate = reader.GetString(reader.GetOrdinal("KickMessageTemplate")),
                    WarningsBeforeKick = reader.GetInt32(reader.GetOrdinal("WarningsBeforeKick")),
                    WarningResetMinutes = reader.GetInt32(reader.GetOrdinal("WarningResetMinutes")),
                    QuizIntervalMinutes = reader.GetInt32(reader.GetOrdinal("QuizIntervalMinutes")),
                    QuizTimeoutSeconds = reader.GetInt32(reader.GetOrdinal("QuizTimeoutSeconds"))
                };
            }
            return null;
        }

        public async Task<BotConfigDto> CreateOrUpdateBotConfigAsync(int roomId, UpdateBotConfigDto dto, int actorId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var existingConfig = await GetBotConfigAsync(roomId);
            
            if (existingConfig == null)
            {
                // Créer une nouvelle config
                var insertSql = @"
                    INSERT INTO ""BotConfigs"" (
                        ""RoomId"", ""BotName"", ""IsEnabled"", ""WelcomeMessageEnabled"", 
                        ""ModerationEnabled"", ""QuizEnabled"", ""MentionResponseEnabled"", 
                        ""TopicSuggestionEnabled"", ""WelcomeMessageTemplate"", 
                        ""WarningMessageTemplate"", ""KickMessageTemplate"",
                        ""WarningsBeforeKick"", ""WarningResetMinutes"",
                        ""QuizIntervalMinutes"", ""QuizTimeoutSeconds""
                    ) VALUES (
                        @roomId, @botName, @isEnabled, @welcomeEnabled,
                        @moderationEnabled, @quizEnabled, @mentionEnabled,
                        @topicEnabled, @welcomeTemplate,
                        @warningTemplate, @kickTemplate,
                        @warningsBeforeKick, @warningResetMinutes,
                        @quizIntervalMinutes, @quizTimeoutSeconds
                    ) RETURNING ""Id""";

                using var insertCmd = new NpgsqlCommand(insertSql, conn);
                insertCmd.Parameters.AddWithValue("roomId", roomId);
                insertCmd.Parameters.AddWithValue("botName", dto.BotName ?? "PaLX Bot");
                insertCmd.Parameters.AddWithValue("isEnabled", dto.IsEnabled ?? true);
                insertCmd.Parameters.AddWithValue("welcomeEnabled", dto.WelcomeMessageEnabled ?? true);
                insertCmd.Parameters.AddWithValue("moderationEnabled", dto.ModerationEnabled ?? true);
                insertCmd.Parameters.AddWithValue("quizEnabled", dto.QuizEnabled ?? false);
                insertCmd.Parameters.AddWithValue("mentionEnabled", dto.MentionResponseEnabled ?? true);
                insertCmd.Parameters.AddWithValue("topicEnabled", dto.TopicSuggestionEnabled ?? false);
                insertCmd.Parameters.AddWithValue("welcomeTemplate", dto.WelcomeMessageTemplate ?? "Bienvenue {username} dans le salon ! 👋");
                insertCmd.Parameters.AddWithValue("warningTemplate", dto.WarningMessageTemplate ?? "⚠️ {username}, merci de respecter les règles du salon.");
                insertCmd.Parameters.AddWithValue("kickTemplate", dto.KickMessageTemplate ?? "❌ {username} a été expulsé pour comportement inapproprié.");
                insertCmd.Parameters.AddWithValue("warningsBeforeKick", dto.WarningsBeforeKick ?? 3);
                insertCmd.Parameters.AddWithValue("warningResetMinutes", dto.WarningResetMinutes ?? 60);
                insertCmd.Parameters.AddWithValue("quizIntervalMinutes", dto.QuizIntervalMinutes ?? 30);
                insertCmd.Parameters.AddWithValue("quizTimeoutSeconds", dto.QuizTimeoutSeconds ?? 60);

                await insertCmd.ExecuteScalarAsync();
            }
            else
            {
                // Mettre à jour la config existante
                var updateSql = @"
                    UPDATE ""BotConfigs"" SET
                        ""BotName"" = COALESCE(@botName, ""BotName""),
                        ""IsEnabled"" = COALESCE(@isEnabled, ""IsEnabled""),
                        ""WelcomeMessageEnabled"" = COALESCE(@welcomeEnabled, ""WelcomeMessageEnabled""),
                        ""ModerationEnabled"" = COALESCE(@moderationEnabled, ""ModerationEnabled""),
                        ""QuizEnabled"" = COALESCE(@quizEnabled, ""QuizEnabled""),
                        ""MentionResponseEnabled"" = COALESCE(@mentionEnabled, ""MentionResponseEnabled""),
                        ""TopicSuggestionEnabled"" = COALESCE(@topicEnabled, ""TopicSuggestionEnabled""),
                        ""WelcomeMessageTemplate"" = COALESCE(@welcomeTemplate, ""WelcomeMessageTemplate""),
                        ""WarningMessageTemplate"" = COALESCE(@warningTemplate, ""WarningMessageTemplate""),
                        ""KickMessageTemplate"" = COALESCE(@kickTemplate, ""KickMessageTemplate""),
                        ""WarningsBeforeKick"" = COALESCE(@warningsBeforeKick, ""WarningsBeforeKick""),
                        ""WarningResetMinutes"" = COALESCE(@warningResetMinutes, ""WarningResetMinutes""),
                        ""QuizIntervalMinutes"" = COALESCE(@quizIntervalMinutes, ""QuizIntervalMinutes""),
                        ""QuizTimeoutSeconds"" = COALESCE(@quizTimeoutSeconds, ""QuizTimeoutSeconds""),
                        ""UpdatedAt"" = CURRENT_TIMESTAMP
                    WHERE ""RoomId"" = @roomId";

                using var updateCmd = new NpgsqlCommand(updateSql, conn);
                updateCmd.Parameters.AddWithValue("roomId", roomId);
                updateCmd.Parameters.AddWithValue("botName", (object?)dto.BotName ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("isEnabled", (object?)dto.IsEnabled ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("welcomeEnabled", (object?)dto.WelcomeMessageEnabled ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("moderationEnabled", (object?)dto.ModerationEnabled ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("quizEnabled", (object?)dto.QuizEnabled ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("mentionEnabled", (object?)dto.MentionResponseEnabled ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("topicEnabled", (object?)dto.TopicSuggestionEnabled ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("welcomeTemplate", (object?)dto.WelcomeMessageTemplate ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("warningTemplate", (object?)dto.WarningMessageTemplate ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("kickTemplate", (object?)dto.KickMessageTemplate ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("warningsBeforeKick", (object?)dto.WarningsBeforeKick ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("warningResetMinutes", (object?)dto.WarningResetMinutes ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("quizIntervalMinutes", (object?)dto.QuizIntervalMinutes ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("quizTimeoutSeconds", (object?)dto.QuizTimeoutSeconds ?? DBNull.Value);

                await updateCmd.ExecuteNonQueryAsync();
            }

            return (await GetBotConfigAsync(roomId))!;
        }

        public async Task<bool> IsBotEnabledAsync(int roomId)
        {
            var config = await GetBotConfigAsync(roomId);
            return config?.IsEnabled ?? false;
        }

        #endregion

        #region Banned Words

        public async Task<List<BannedWordDto>> GetBannedWordsAsync(int roomId)
        {
            var words = new List<BannedWordDto>();
            
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT bw.*, u.""Username"" as AddedByUsername
                FROM ""BannedWords"" bw
                LEFT JOIN ""Users"" u ON bw.""AddedBy"" = u.""Id""
                WHERE bw.""RoomId"" = @roomId
                ORDER BY bw.""CreatedAt"" DESC";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("roomId", roomId);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                words.Add(new BannedWordDto
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Word = reader.GetString(reader.GetOrdinal("Word")),
                    Severity = reader.GetString(reader.GetOrdinal("Severity")),
                    AddedByUsername = reader.IsDBNull(reader.GetOrdinal("AddedByUsername")) 
                        ? "Inconnu" 
                        : reader.GetString(reader.GetOrdinal("AddedByUsername")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }
            
            return words;
        }

        public async Task<BannedWordDto> AddBannedWordAsync(int roomId, AddBannedWordDto dto, int actorId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO ""BannedWords"" (""RoomId"", ""Word"", ""Severity"", ""AddedBy"")
                VALUES (@roomId, @word, @severity, @addedBy)
                RETURNING ""Id"", ""CreatedAt""";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("roomId", roomId);
            cmd.Parameters.AddWithValue("word", dto.Word.ToLowerInvariant().Trim());
            cmd.Parameters.AddWithValue("severity", dto.Severity);
            cmd.Parameters.AddWithValue("addedBy", actorId);
            
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                // Récupérer le nom d'utilisateur
                var usernameSql = @"SELECT ""Username"" FROM ""Users"" WHERE ""Id"" = @userId";
                await reader.CloseAsync();
                
                using var userCmd = new NpgsqlCommand(usernameSql, conn);
                userCmd.Parameters.AddWithValue("userId", actorId);
                var username = await userCmd.ExecuteScalarAsync() as string ?? "Inconnu";

                return new BannedWordDto
                {
                    Id = reader.GetInt32(0),
                    Word = dto.Word.ToLowerInvariant().Trim(),
                    Severity = dto.Severity,
                    AddedByUsername = username,
                    CreatedAt = reader.GetDateTime(1)
                };
            }
            
            throw new Exception("Failed to add banned word");
        }

        public async Task<bool> RemoveBannedWordAsync(int roomId, int wordId, int actorId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"DELETE FROM ""BannedWords"" WHERE ""Id"" = @wordId AND ""RoomId"" = @roomId";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("wordId", wordId);
            cmd.Parameters.AddWithValue("roomId", roomId);
            
            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        public async Task<(bool isViolation, string? triggerWord, string severity)> CheckMessageForViolationsAsync(int roomId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return (false, null, "None");

            var bannedWords = await GetBannedWordsAsync(roomId);
            var messageLower = message.ToLowerInvariant();

            foreach (var word in bannedWords)
            {
                // Utiliser une regex pour détecter le mot (même avec caractères spéciaux autour)
                var pattern = $@"\b{Regex.Escape(word.Word)}\b";
                if (Regex.IsMatch(messageLower, pattern, RegexOptions.IgnoreCase))
                {
                    return (true, word.Word, word.Severity);
                }
            }

            return (false, null, "None");
        }

        #endregion

        #region Warnings

        public async Task<int> GetActiveWarningsCountAsync(int roomId, int userId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Récupérer la durée de reset des warnings
            var config = await GetBotConfigAsync(roomId);
            var resetMinutes = config?.WarningResetMinutes ?? 60;

            var sql = @"
                SELECT COUNT(*) FROM ""BotWarnings""
                WHERE ""RoomId"" = @roomId 
                AND ""UserId"" = @userId 
                AND ""IsActive"" = TRUE
                AND ""CreatedAt"" > NOW() - @resetMinutes * INTERVAL '1 minute'";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("roomId", roomId);
            cmd.Parameters.AddWithValue("userId", userId);
            cmd.Parameters.AddWithValue("resetMinutes", resetMinutes);
            
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<BotWarningDto> AddWarningAsync(int roomId, int userId, string reason, string triggerWord, string originalMessage)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO ""BotWarnings"" (""RoomId"", ""UserId"", ""Reason"", ""TriggerWord"", ""OriginalMessage"")
                VALUES (@roomId, @userId, @reason, @triggerWord, @originalMessage)
                RETURNING ""Id"", ""CreatedAt""";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("roomId", roomId);
            cmd.Parameters.AddWithValue("userId", userId);
            cmd.Parameters.AddWithValue("reason", reason);
            cmd.Parameters.AddWithValue("triggerWord", triggerWord);
            cmd.Parameters.AddWithValue("originalMessage", originalMessage);
            
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var createdAt = reader.GetDateTime(1);
                await reader.CloseAsync();

                // Récupérer le nom d'utilisateur
                var usernameSql = @"SELECT ""Username"" FROM ""Users"" WHERE ""Id"" = @userId";
                using var userCmd = new NpgsqlCommand(usernameSql, conn);
                userCmd.Parameters.AddWithValue("userId", userId);
                var username = await userCmd.ExecuteScalarAsync() as string ?? "Inconnu";

                return new BotWarningDto
                {
                    Id = id,
                    UserId = userId,
                    Username = username,
                    Reason = reason,
                    TriggerWord = triggerWord,
                    CreatedAt = createdAt
                };
            }
            
            throw new Exception("Failed to add warning");
        }

        public async Task ResetWarningsAsync(int roomId, int userId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                UPDATE ""BotWarnings"" 
                SET ""IsActive"" = FALSE 
                WHERE ""RoomId"" = @roomId AND ""UserId"" = @userId AND ""IsActive"" = TRUE";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("roomId", roomId);
            cmd.Parameters.AddWithValue("userId", userId);
            
            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Quiz

        public async Task<List<QuizQuestionDto>> GetQuizQuestionsAsync(int roomId, int count = 10)
        {
            var questions = new List<QuizQuestionDto>();
            
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Récupérer les questions du salon + les questions globales
            var sql = @"
                SELECT ""Id"", ""Question"", ""Options"", ""Category"", ""Points""
                FROM ""QuizQuestions""
                WHERE ""RoomId"" = @roomId OR ""RoomId"" = 0
                ORDER BY RANDOM()
                LIMIT @count";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("roomId", roomId);
            cmd.Parameters.AddWithValue("count", count);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                questions.Add(new QuizQuestionDto
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Question = reader.GetString(reader.GetOrdinal("Question")),
                    Options = reader.IsDBNull(reader.GetOrdinal("Options")) 
                        ? Array.Empty<string>() 
                        : (string[])reader.GetValue(reader.GetOrdinal("Options")),
                    Category = reader.GetString(reader.GetOrdinal("Category")),
                    Points = reader.GetInt32(reader.GetOrdinal("Points"))
                });
            }
            
            return questions;
        }

        public async Task<QuizQuestionDto?> GetRandomQuizQuestionAsync(int roomId)
        {
            var questions = await GetQuizQuestionsAsync(roomId, 1);
            return questions.FirstOrDefault();
        }

        public async Task<bool> CheckQuizAnswerAsync(int questionId, string answer)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT ""Answer"" FROM ""QuizQuestions"" WHERE ""Id"" = @questionId";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("questionId", questionId);
            
            var correctAnswer = await cmd.ExecuteScalarAsync() as string;
            if (correctAnswer == null) return false;

            return correctAnswer.Equals(answer.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public async Task<QuizQuestionDto> AddQuizQuestionAsync(int roomId, CreateQuizQuestionDto dto)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO ""QuizQuestions"" (""RoomId"", ""Question"", ""Answer"", ""Options"", ""Category"", ""Points"")
                VALUES (@roomId, @question, @answer, @options, @category, @points)
                RETURNING ""Id""";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("roomId", roomId);
            cmd.Parameters.AddWithValue("question", dto.Question);
            cmd.Parameters.AddWithValue("answer", dto.Answer);
            cmd.Parameters.AddWithValue("options", dto.Options ?? Array.Empty<string>());
            cmd.Parameters.AddWithValue("category", dto.Category);
            cmd.Parameters.AddWithValue("points", dto.Points);
            
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            
            return new QuizQuestionDto
            {
                Id = id,
                Question = dto.Question,
                Options = dto.Options ?? Array.Empty<string>(),
                Category = dto.Category,
                Points = dto.Points
            };
        }

        #endregion

        #region Discussion Topics

        public async Task<List<DiscussionTopicDto>> GetDiscussionTopicsAsync(int roomId, int count = 10)
        {
            var topics = new List<DiscussionTopicDto>();
            
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT ""Id"", ""Topic"", ""Category""
                FROM ""DiscussionTopics""
                WHERE ""RoomId"" = @roomId OR ""RoomId"" = 0
                ORDER BY RANDOM()
                LIMIT @count";
            
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("roomId", roomId);
            cmd.Parameters.AddWithValue("count", count);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                topics.Add(new DiscussionTopicDto
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Topic = reader.GetString(reader.GetOrdinal("Topic")),
                    Category = reader.GetString(reader.GetOrdinal("Category"))
                });
            }
            
            return topics;
        }

        public async Task<DiscussionTopicDto?> GetRandomTopicAsync(int roomId)
        {
            var topics = await GetDiscussionTopicsAsync(roomId, 1);
            return topics.FirstOrDefault();
        }

        #endregion

        #region Bot Actions

        public async Task SendBotMessageAsync(int roomId, string content, string messageType = "Bot")
        {
            var config = await GetBotConfigAsync(roomId);
            var botName = config?.BotName ?? "PaLX Bot";
            var botAvatar = config?.BotAvatarUrl ?? "/images/bot-avatar.png";

            var message = new
            {
                UserId = 0, // Bot n'a pas d'ID utilisateur
                Username = botName,
                AvatarUrl = botAvatar,
                Content = content,
                MessageType = messageType,
                Timestamp = DateTime.UtcNow
            };

            await _roomHubContext.Clients.Group($"room_{roomId}").SendAsync("ReceiveMessage", message);
            
            _logger.LogInformation("Bot message sent to room {RoomId}: {Content}", roomId, content);
        }

        public async Task SendWelcomeMessageAsync(int roomId, int userId, string username)
        {
            var config = await GetBotConfigAsync(roomId);
            if (config == null || !config.IsEnabled || !config.WelcomeMessageEnabled)
                return;

            var message = config.WelcomeMessageTemplate.Replace("{username}", username);
            await SendBotMessageAsync(roomId, message, "BotWelcome");
        }

        public async Task SendWarningMessageAsync(int roomId, int userId, string username, string reason)
        {
            var config = await GetBotConfigAsync(roomId);
            if (config == null || !config.IsEnabled)
                return;

            var warningCount = await GetActiveWarningsCountAsync(roomId, userId);
            var message = config.WarningMessageTemplate
                .Replace("{username}", username)
                .Replace("{reason}", reason)
                .Replace("{count}", warningCount.ToString())
                .Replace("{max}", config.WarningsBeforeKick.ToString());

            // Ajouter le compteur d'avertissements
            message += $" (Avertissement {warningCount}/{config.WarningsBeforeKick})";

            await SendBotMessageAsync(roomId, message, "BotWarning");
        }

        public async Task HandleMentionAsync(int roomId, int userId, string username, string message)
        {
            var config = await GetBotConfigAsync(roomId);
            if (config == null || !config.IsEnabled || !config.MentionResponseEnabled)
                return;

            // Choisir une réponse aléatoire
            var random = new Random();
            var response = MentionResponses[random.Next(MentionResponses.Length)];
            response = response.Replace("{username}", username);

            // Petite pause pour simuler le temps de réponse
            await Task.Delay(500);

            await SendBotMessageAsync(roomId, response, "Bot");
        }

        /// <summary>
        /// Traite un message utilisateur et effectue les actions nécessaires
        /// Retourne true si le message doit être bloqué (violation)
        /// </summary>
        public async Task<bool> ProcessUserMessageAsync(int roomId, int userId, string username, string message)
        {
            var config = await GetBotConfigAsync(roomId);
            if (config == null || !config.IsEnabled)
                return false;

            // 1. Vérifier les violations de modération
            if (config.ModerationEnabled)
            {
                var (isViolation, triggerWord, severity) = await CheckMessageForViolationsAsync(roomId, message);
                
                if (isViolation && triggerWord != null)
                {
                    _logger.LogWarning("Message violation in room {RoomId} by user {UserId}: {Word} ({Severity})", 
                        roomId, userId, triggerWord, severity);

                    switch (severity)
                    {
                        case "Warning":
                            // Ajouter un avertissement
                            await AddWarningAsync(roomId, userId, "Utilisation de langage inapproprié", triggerWord, message);
                            var warningCount = await GetActiveWarningsCountAsync(roomId, userId);
                            
                            // Vérifier si on doit kick
                            if (warningCount >= config.WarningsBeforeKick)
                            {
                                // Kick l'utilisateur (actorId=0 signifie action du Bot)
                                await GetRoomService().KickUserAsync(0, roomId, userId, "Bot: Trop d'avertissements");
                                var kickMessage = config.KickMessageTemplate.Replace("{username}", username);
                                await SendBotMessageAsync(roomId, kickMessage, "BotWarning");
                                await ResetWarningsAsync(roomId, userId);
                            }
                            else
                            {
                                await SendWarningMessageAsync(roomId, userId, username, "langage inapproprié");
                            }
                            return true; // Bloquer le message

                        case "Kick":
                            // Kick immédiat
                            await GetRoomService().KickUserAsync(0, roomId, userId, $"Bot: {triggerWord}");
                            var kickMsg = config.KickMessageTemplate.Replace("{username}", username);
                            await SendBotMessageAsync(roomId, kickMsg, "BotWarning");
                            return true;

                        case "Ban":
                            // Ban immédiat
                            await GetRoomService().BanUserAsync(0, roomId, userId, new BanUserDto { Reason = $"Bot: {triggerWord}", BanType = "Permanent" });
                            var banMsg = $"🚫 {username} a été banni pour violation grave des règles.";
                            await SendBotMessageAsync(roomId, banMsg, "BotWarning");
                            return true;
                    }
                }
            }

            // 2. Vérifier si le bot est mentionné
            if (config.MentionResponseEnabled)
            {
                var botNameLower = config.BotName.ToLowerInvariant();
                var messageLower = message.ToLowerInvariant();
                
                // Vérifier les mentions: @botname, botname, hey botname, etc.
                if (messageLower.Contains($"@{botNameLower}") || 
                    Regex.IsMatch(messageLower, $@"\b{Regex.Escape(botNameLower)}\b"))
                {
                    await HandleMentionAsync(roomId, userId, username, message);
                }
            }

            // 3. Commandes spéciales
            if (message.StartsWith("!"))
            {
                await HandleCommandAsync(roomId, userId, username, message, config);
            }

            return false; // Message OK, pas de blocage
        }

        private async Task HandleCommandAsync(int roomId, int userId, string username, string message, BotConfigDto config)
        {
            var command = message.ToLowerInvariant().Split(' ')[0];

            switch (command)
            {
                case "!aide":
                case "!help":
                    var helpMessage = @"🤖 **Commandes disponibles:**
• !aide - Afficher cette aide
• !quiz - Lancer une question de quiz
• !topic - Suggérer un sujet de discussion
• !regles - Afficher les règles du salon";
                    await SendBotMessageAsync(roomId, helpMessage, "Bot");
                    break;

                case "!quiz":
                    if (config.QuizEnabled)
                    {
                        var question = await GetRandomQuizQuestionAsync(roomId);
                        if (question != null)
                        {
                            var quizMessage = $"🎯 **Quiz!** ({question.Category} - {question.Points} pts)\n\n{question.Question}";
                            if (question.Options.Length > 0)
                            {
                                quizMessage += "\n\nOptions: " + string.Join(" | ", question.Options.Select((o, i) => $"{(char)('A' + i)}) {o}"));
                            }
                            await SendBotMessageAsync(roomId, quizMessage, "BotQuiz");
                        }
                    }
                    else
                    {
                        await SendBotMessageAsync(roomId, "❌ Le quiz n'est pas activé dans ce salon.", "Bot");
                    }
                    break;

                case "!topic":
                case "!sujet":
                    if (config.TopicSuggestionEnabled)
                    {
                        var topic = await GetRandomTopicAsync(roomId);
                        if (topic != null)
                        {
                            await SendBotMessageAsync(roomId, $"💬 **Sujet de discussion:**\n{topic.Topic}", "Bot");
                        }
                    }
                    else
                    {
                        await SendBotMessageAsync(roomId, "❌ Les suggestions de sujets ne sont pas activées dans ce salon.", "Bot");
                    }
                    break;

                case "!regles":
                case "!rules":
                    await SendBotMessageAsync(roomId, @"📋 **Règles du salon:**
1. Respectez tous les membres
2. Pas de spam ni de publicité
3. Pas de contenu inapproprié
4. Utilisez un langage correct
5. Suivez les instructions des modérateurs", "Bot");
                    break;
            }
        }

        #endregion
    }
}

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
        private static bool _tablesInitialized = false;
        private static readonly object _initLock = new object();

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

        /// <summary>
        /// Initialise les tables Bot si elles n'existent pas
        /// </summary>
        private async Task EnsureTablesExistAsync(NpgsqlConnection conn)
        {
            if (_tablesInitialized) return;
            
            lock (_initLock)
            {
                if (_tablesInitialized) return;
                _tablesInitialized = true;
            }

            try
            {
                var createTablesSql = @"
                    -- Table de configuration du Bot par salon
                    CREATE TABLE IF NOT EXISTS ""BotConfigs"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""RoomId"" INTEGER NOT NULL,
                        ""BotName"" VARCHAR(50) NOT NULL DEFAULT 'PaLX Bot',
                        ""BotAvatarUrl"" VARCHAR(255) DEFAULT '/images/bot-avatar.png',
                        ""IsEnabled"" BOOLEAN NOT NULL DEFAULT TRUE,
                        ""WelcomeMessageEnabled"" BOOLEAN NOT NULL DEFAULT TRUE,
                        ""ModerationEnabled"" BOOLEAN NOT NULL DEFAULT TRUE,
                        ""QuizEnabled"" BOOLEAN NOT NULL DEFAULT FALSE,
                        ""MentionResponseEnabled"" BOOLEAN NOT NULL DEFAULT TRUE,
                        ""TopicSuggestionEnabled"" BOOLEAN NOT NULL DEFAULT FALSE,
                        ""WelcomeMessageTemplate"" TEXT DEFAULT 'Bienvenue {username} dans le salon ! 👋',
                        ""WarningMessageTemplate"" TEXT DEFAULT '⚠️ {username}, merci de respecter les règles du salon.',
                        ""KickMessageTemplate"" TEXT DEFAULT '❌ {username} a été expulsé pour comportement inapproprié.',
                        ""WarningsBeforeKick"" INTEGER NOT NULL DEFAULT 3,
                        ""WarningResetMinutes"" INTEGER NOT NULL DEFAULT 60,
                        ""QuizIntervalMinutes"" INTEGER NOT NULL DEFAULT 30,
                        ""QuizTimeoutSeconds"" INTEGER NOT NULL DEFAULT 60,
                        ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        CONSTRAINT ""unique_room_bot"" UNIQUE (""RoomId"")
                    );

                    -- Table des avertissements donnés par le bot
                    CREATE TABLE IF NOT EXISTS ""BotWarnings"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""RoomId"" INTEGER NOT NULL,
                        ""UserId"" INTEGER NOT NULL,
                        ""Reason"" VARCHAR(500) NOT NULL DEFAULT '',
                        ""TriggerWord"" VARCHAR(100) NOT NULL DEFAULT '',
                        ""OriginalMessage"" TEXT NOT NULL DEFAULT '',
                        ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE
                    );

                    -- Table des mots interdits par salon
                    CREATE TABLE IF NOT EXISTS ""BannedWords"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""RoomId"" INTEGER NOT NULL,
                        ""Word"" VARCHAR(100) NOT NULL,
                        ""Severity"" VARCHAR(20) NOT NULL DEFAULT 'Warning',
                        ""AddedBy"" INTEGER NOT NULL,
                        ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        CONSTRAINT ""unique_room_word"" UNIQUE (""RoomId"", ""Word"")
                    );

                    -- Table des questions de quiz
                    CREATE TABLE IF NOT EXISTS ""QuizQuestions"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""RoomId"" INTEGER DEFAULT 0,
                        ""Question"" TEXT NOT NULL,
                        ""Answer"" VARCHAR(500) NOT NULL,
                        ""Options"" TEXT[],
                        ""Category"" VARCHAR(50) NOT NULL DEFAULT 'General',
                        ""Points"" INTEGER NOT NULL DEFAULT 10,
                        ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );

                    -- Table des sujets de discussion
                    CREATE TABLE IF NOT EXISTS ""DiscussionTopics"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""RoomId"" INTEGER DEFAULT 0,
                        ""Topic"" TEXT NOT NULL,
                        ""Category"" VARCHAR(50) NOT NULL DEFAULT 'General',
                        ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                ";

                using var cmd = new NpgsqlCommand(createTablesSql, conn);
                await cmd.ExecuteNonQueryAsync();
                
                // Ajouter des questions de quiz par défaut (si la table est vide)
                await InsertDefaultQuizQuestionsAsync(conn);
                
                // Ajouter des sujets de discussion par défaut (si la table est vide)
                await InsertDefaultTopicsAsync(conn);
                
                _logger.LogInformation("[BotService] Tables Bot créées/vérifiées avec succès");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BotService] Erreur lors de la création des tables Bot");
                _tablesInitialized = false; // Réessayer la prochaine fois
            }
        }
        
        private async Task InsertDefaultQuizQuestionsAsync(NpgsqlConnection conn)
        {
            // Vérifier si des questions existent déjà
            var countCmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""QuizQuestions"" WHERE ""RoomId"" = 0", conn);
            var count = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
            if (count > 0) return;
            
            var defaultQuestions = new[]
            {
                ("Quelle est la capitale de la France ?", "Paris", new[] { "Paris", "Lyon", "Marseille", "Bordeaux" }, "Géographie", 10),
                ("Combien font 7 × 8 ?", "56", new[] { "54", "56", "58", "64" }, "Mathématiques", 10),
                ("Qui a peint La Joconde ?", "Léonard de Vinci", new[] { "Michel-Ange", "Léonard de Vinci", "Raphaël", "Picasso" }, "Art", 15),
                ("Quel est le plus grand océan du monde ?", "Pacifique", new[] { "Atlantique", "Indien", "Pacifique", "Arctique" }, "Géographie", 10),
                ("En quelle année l'homme a-t-il marché sur la Lune pour la première fois ?", "1969", new[] { "1965", "1967", "1969", "1971" }, "Histoire", 15),
                ("Quel est le symbole chimique de l'or ?", "Au", new[] { "Or", "Au", "Ag", "Fe" }, "Sciences", 10),
                ("Combien y a-t-il de continents sur Terre ?", "7", new[] { "5", "6", "7", "8" }, "Géographie", 10),
                ("Quel animal est le plus rapide du monde ?", "Guépard", new[] { "Lion", "Guépard", "Tigre", "Léopard" }, "Nature", 10),
                ("Quelle est la langue la plus parlée dans le monde ?", "Mandarin", new[] { "Anglais", "Espagnol", "Mandarin", "Hindi" }, "Culture", 15),
                ("Quel est le plus long fleuve du monde ?", "Nil", new[] { "Amazone", "Nil", "Yangtsé", "Mississippi" }, "Géographie", 15)
            };
            
            foreach (var (question, answer, options, category, points) in defaultQuestions)
            {
                var sql = @"INSERT INTO ""QuizQuestions"" (""RoomId"", ""Question"", ""Answer"", ""Options"", ""Category"", ""Points"") 
                            VALUES (0, @q, @a, @o, @c, @p) ON CONFLICT DO NOTHING";
                using var insertCmd = new NpgsqlCommand(sql, conn);
                insertCmd.Parameters.AddWithValue("q", question);
                insertCmd.Parameters.AddWithValue("a", answer);
                insertCmd.Parameters.AddWithValue("o", options);
                insertCmd.Parameters.AddWithValue("c", category);
                insertCmd.Parameters.AddWithValue("p", points);
                await insertCmd.ExecuteNonQueryAsync();
            }
            
            _logger.LogInformation("[BotService] Questions de quiz par défaut ajoutées");
        }
        
        private async Task InsertDefaultTopicsAsync(NpgsqlConnection conn)
        {
            // Vérifier si des sujets existent déjà
            var countCmd = new NpgsqlCommand(@"SELECT COUNT(*) FROM ""DiscussionTopics"" WHERE ""RoomId"" = 0", conn);
            var count = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
            if (count > 0) return;
            
            var defaultTopics = new[]
            {
                ("Si tu pouvais voyager n'importe où dans le monde, où irais-tu et pourquoi ?", "Voyage"),
                ("Quel est ton film ou ta série préférée du moment ?", "Divertissement"),
                ("Si tu avais un super-pouvoir, lequel choisirais-tu ?", "Fun"),
                ("Quel est le meilleur conseil qu'on t'ait donné ?", "Vie"),
                ("Tu préfères le matin ou le soir ? Pourquoi ?", "Style de vie"),
                ("Quel est ton plat préféré ?", "Cuisine"),
                ("Si tu pouvais maîtriser un instrument de musique instantanément, lequel serait-ce ?", "Musique"),
                ("Quel est ton jeu vidéo préféré de tous les temps ?", "Gaming"),
                ("Si tu pouvais dîner avec une personnalité historique, qui serait-ce ?", "Histoire"),
                ("Quelle est la chose la plus folle sur ta bucket list ?", "Rêves")
            };
            
            foreach (var (topic, category) in defaultTopics)
            {
                var sql = @"INSERT INTO ""DiscussionTopics"" (""RoomId"", ""Topic"", ""Category"") 
                            VALUES (0, @t, @c) ON CONFLICT DO NOTHING";
                using var insertCmd = new NpgsqlCommand(sql, conn);
                insertCmd.Parameters.AddWithValue("t", topic);
                insertCmd.Parameters.AddWithValue("c", category);
                await insertCmd.ExecuteNonQueryAsync();
            }
            
            _logger.LogInformation("[BotService] Sujets de discussion par défaut ajoutés");
        }

        #region Configuration

        public async Task<BotConfigDto?> GetBotConfigAsync(int roomId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            
            // S'assurer que les tables existent
            await EnsureTablesExistAsync(conn);

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
            
            // S'assurer que les tables existent
            await EnsureTablesExistAsync(conn);

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
            
            int wordId;
            DateTime createdAt;
            
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                    throw new Exception("Failed to add banned word");
                    
                wordId = reader.GetInt32(0);
                createdAt = reader.GetDateTime(1);
            }
            
            // Récupérer le nom d'utilisateur (après avoir fermé le reader)
            var usernameSql = @"SELECT ""Username"" FROM ""Users"" WHERE ""Id"" = @userId";
            using var userCmd = new NpgsqlCommand(usernameSql, conn);
            userCmd.Parameters.AddWithValue("userId", actorId);
            var username = await userCmd.ExecuteScalarAsync() as string ?? "Inconnu";

            return new BannedWordDto
            {
                Id = wordId,
                Word = dto.Word.ToLowerInvariant().Trim(),
                Severity = dto.Severity,
                AddedByUsername = username,
                CreatedAt = createdAt
            };
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
            
            _logger.LogDebug("[BotService] Checking message for violations: '{Message}' against {Count} banned words", 
                messageLower, bannedWords.Count);

            foreach (var word in bannedWords)
            {
                var wordLower = word.Word.ToLowerInvariant();
                
                // Méthode 1: Correspondance exacte avec limites de mots
                var pattern = $@"\b{Regex.Escape(wordLower)}\b";
                if (Regex.IsMatch(messageLower, pattern, RegexOptions.IgnoreCase))
                {
                    _logger.LogInformation("[BotService] Violation detected! Word: '{Word}' in message: '{Message}'", 
                        word.Word, message);
                    return (true, word.Word, word.Severity);
                }
                
                // Méthode 2: Contient le mot (si le mot fait 4+ caractères)
                if (wordLower.Length >= 4 && messageLower.Contains(wordLower))
                {
                    _logger.LogInformation("[BotService] Violation detected (contains)! Word: '{Word}' in message: '{Message}'", 
                        word.Word, message);
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

            // Créer un RoomMessageDto complet pour que le client puisse le traiter
            var message = new RoomMessageDto
            {
                Id = 0,
                RoomId = roomId,
                UserId = 0, // Bot n'a pas d'ID utilisateur
                Username = botName,
                DisplayName = botName,
                AvatarPath = botAvatar,
                RoleName = "Bot IA",
                RoleColor = "#9B59B6", // Violet pour le bot
                Content = content,
                MessageType = messageType,
                Timestamp = DateTime.UtcNow
            };

            // Note: Le groupe SignalR est "Room_{roomId}" avec un R majuscule
            await _roomHubContext.Clients.Group($"Room_{roomId}").SendAsync("ReceiveMessage", message);
            
            _logger.LogInformation("[BotService] Bot message sent to room {RoomId}: {Content}", roomId, content);
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

            // Réponse personnalisée avec explication des commandes
            var response = $"Bonjour {username} ! 👋 Je suis **{config.BotName}**, l'assistant IA de ce salon.\n\n" +
                          "📝 **Voici ce que je peux faire :**\n" +
                          "• `!aide` - Voir toutes les commandes\n" +
                          "• `!quiz` - Lancer une question quiz 🎯\n" +
                          "• `!sujet` - Suggérer un sujet de discussion 💬\n" +
                          "• `!regles` - Afficher les règles du salon 📋\n\n" +
                          "Je surveille aussi le chat pour la modération. Bonne discussion ! 😊";

            // Petite pause pour simuler le temps de réponse
            await Task.Delay(300);

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
            
            _logger.LogDebug("[BotService] Processing message from user {UserId} ({Username}) in room {RoomId}: {Message}", 
                userId, username, roomId, message);

            // 1. Vérifier les violations de modération
            // Note: La modération s'applique à TOUS les utilisateurs, y compris le owner
            // Le owner peut se modérer lui-même pour tester, ou désactiver la modération s'il le souhaite
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

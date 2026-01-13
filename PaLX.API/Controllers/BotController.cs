using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaLX.API.DTOs;
using PaLX.API.Services;
using System.Security.Claims;

namespace PaLX.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BotController : ControllerBase
    {
        private readonly IBotService _botService;
        private readonly IRoomService _roomService;
        private readonly ILogger<BotController> _logger;

        public BotController(IBotService botService, IRoomService roomService, ILogger<BotController> logger)
        {
            _botService = botService;
            _roomService = roomService;
            _logger = logger;
        }

        private int GetUserId()
        {
            // Le claim "UserId" contient l'ID numérique (voir AuthService.GenerateJwtToken)
            var userIdClaim = User.FindFirst("UserId")?.Value;
            _logger.LogDebug("[BotController] GetUserId - Claim value: {ClaimValue}", userIdClaim);
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        #region Configuration

        /// <summary>
        /// Récupère la configuration du bot pour un salon
        /// </summary>
        [HttpGet("config/{roomId}")]
        public async Task<IActionResult> GetBotConfig(int roomId)
        {
            try
            {
                var config = await _botService.GetBotConfigAsync(roomId);
                if (config == null)
                {
                    // Retourner une config par défaut si elle n'existe pas
                    return Ok(new BotConfigDto { RoomId = roomId });
                }
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bot config for room {RoomId}", roomId);
                return StatusCode(500, "Error retrieving bot configuration");
            }
        }

        /// <summary>
        /// Met à jour la configuration du bot pour un salon
        /// Seuls le owner et les admins peuvent modifier
        /// </summary>
        [HttpPut("config/{roomId}")]
        public async Task<IActionResult> UpdateBotConfig(int roomId, [FromBody] UpdateBotConfigDto dto)
        {
            try
            {
                var userId = GetUserId();
                _logger.LogInformation("[BotController] UpdateBotConfig called - RoomId: {RoomId}, UserId: {UserId}", roomId, userId);
                
                // Vérifier les permissions (owner ou admin)
                var canManage = await _roomService.CanManageRoomAsync(roomId, userId);
                _logger.LogInformation("[BotController] CanManage result: {CanManage}", canManage);
                
                if (!canManage)
                {
                    _logger.LogWarning("[BotController] User {UserId} denied access to manage room {RoomId} bot", userId, roomId);
                    return Unauthorized(new { error = "Vous n'avez pas les permissions pour gérer le bot de ce salon" });
                }

                var config = await _botService.CreateOrUpdateBotConfigAsync(roomId, dto, userId);
                _logger.LogInformation("[BotController] Bot config saved successfully for room {RoomId}", roomId);
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BotController] Error updating bot config for room {RoomId}: {Error}", roomId, ex.Message);
                return StatusCode(500, new { error = $"Erreur lors de la mise à jour: {ex.Message}" });
            }
        }

        /// <summary>
        /// Active ou désactive le bot pour un salon
        /// </summary>
        [HttpPost("config/{roomId}/toggle")]
        public async Task<IActionResult> ToggleBot(int roomId, [FromQuery] bool enabled)
        {
            try
            {
                var userId = GetUserId();
                
                var canManage = await _roomService.CanManageRoomAsync(roomId, userId);
                if (!canManage)
                {
                    return Forbid("You don't have permission to manage this room's bot");
                }

                var config = await _botService.CreateOrUpdateBotConfigAsync(roomId, new UpdateBotConfigDto { IsEnabled = enabled }, userId);
                return Ok(new { enabled = config.IsEnabled });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling bot for room {RoomId}", roomId);
                return StatusCode(500, "Error toggling bot");
            }
        }

        #endregion

        #region Banned Words

        /// <summary>
        /// Liste les mots interdits dans un salon
        /// </summary>
        [HttpGet("words/{roomId}")]
        public async Task<IActionResult> GetBannedWords(int roomId)
        {
            try
            {
                var words = await _botService.GetBannedWordsAsync(roomId);
                return Ok(words);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting banned words for room {RoomId}", roomId);
                return StatusCode(500, "Error retrieving banned words");
            }
        }

        /// <summary>
        /// Ajoute un mot interdit dans un salon
        /// </summary>
        [HttpPost("words/{roomId}")]
        public async Task<IActionResult> AddBannedWord(int roomId, [FromBody] AddBannedWordDto dto)
        {
            try
            {
                var userId = GetUserId();
                
                var canManage = await _roomService.CanManageRoomAsync(roomId, userId);
                if (!canManage)
                {
                    return Forbid("You don't have permission to manage this room's bot");
                }

                if (string.IsNullOrWhiteSpace(dto.Word))
                {
                    return BadRequest("Word cannot be empty");
                }

                var word = await _botService.AddBannedWordAsync(roomId, dto, userId);
                return Ok(word);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding banned word for room {RoomId}", roomId);
                return StatusCode(500, "Error adding banned word");
            }
        }

        /// <summary>
        /// Supprime un mot interdit d'un salon
        /// </summary>
        [HttpDelete("words/{roomId}/{wordId}")]
        public async Task<IActionResult> RemoveBannedWord(int roomId, int wordId)
        {
            try
            {
                var userId = GetUserId();
                
                var canManage = await _roomService.CanManageRoomAsync(roomId, userId);
                if (!canManage)
                {
                    return Forbid("You don't have permission to manage this room's bot");
                }

                var result = await _botService.RemoveBannedWordAsync(roomId, wordId, userId);
                if (!result)
                {
                    return NotFound("Word not found");
                }
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing banned word {WordId} from room {RoomId}", wordId, roomId);
                return StatusCode(500, "Error removing banned word");
            }
        }

        #endregion

        #region Quiz

        /// <summary>
        /// Récupère les questions de quiz disponibles pour un salon
        /// </summary>
        [HttpGet("quiz/{roomId}")]
        public async Task<IActionResult> GetQuizQuestions(int roomId, [FromQuery] int count = 10)
        {
            try
            {
                var questions = await _botService.GetQuizQuestionsAsync(roomId, count);
                return Ok(questions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quiz questions for room {RoomId}", roomId);
                return StatusCode(500, "Error retrieving quiz questions");
            }
        }

        /// <summary>
        /// Récupère une question de quiz aléatoire
        /// </summary>
        [HttpGet("quiz/{roomId}/random")]
        public async Task<IActionResult> GetRandomQuizQuestion(int roomId)
        {
            try
            {
                var question = await _botService.GetRandomQuizQuestionAsync(roomId);
                if (question == null)
                {
                    return NotFound("No quiz questions available");
                }
                return Ok(question);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting random quiz question for room {RoomId}", roomId);
                return StatusCode(500, "Error retrieving quiz question");
            }
        }

        /// <summary>
        /// Ajoute une question de quiz personnalisée pour un salon
        /// </summary>
        [HttpPost("quiz/{roomId}")]
        public async Task<IActionResult> AddQuizQuestion(int roomId, [FromBody] CreateQuizQuestionDto dto)
        {
            try
            {
                var userId = GetUserId();
                
                var canManage = await _roomService.CanManageRoomAsync(roomId, userId);
                if (!canManage)
                {
                    return Forbid("You don't have permission to manage this room's bot");
                }

                if (string.IsNullOrWhiteSpace(dto.Question) || string.IsNullOrWhiteSpace(dto.Answer))
                {
                    return BadRequest("Question and answer are required");
                }

                var question = await _botService.AddQuizQuestionAsync(roomId, dto);
                return Ok(question);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding quiz question for room {RoomId}", roomId);
                return StatusCode(500, "Error adding quiz question");
            }
        }

        /// <summary>
        /// Vérifie une réponse de quiz
        /// </summary>
        [HttpPost("quiz/{questionId}/check")]
        public async Task<IActionResult> CheckQuizAnswer(int questionId, [FromBody] string answer)
        {
            try
            {
                var isCorrect = await _botService.CheckQuizAnswerAsync(questionId, answer);
                return Ok(new { correct = isCorrect });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking quiz answer for question {QuestionId}", questionId);
                return StatusCode(500, "Error checking answer");
            }
        }

        #endregion

        #region Discussion Topics

        /// <summary>
        /// Récupère les sujets de discussion disponibles pour un salon
        /// </summary>
        [HttpGet("topics/{roomId}")]
        public async Task<IActionResult> GetDiscussionTopics(int roomId, [FromQuery] int count = 10)
        {
            try
            {
                var topics = await _botService.GetDiscussionTopicsAsync(roomId, count);
                return Ok(topics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discussion topics for room {RoomId}", roomId);
                return StatusCode(500, "Error retrieving discussion topics");
            }
        }

        /// <summary>
        /// Récupère un sujet de discussion aléatoire
        /// </summary>
        [HttpGet("topics/{roomId}/random")]
        public async Task<IActionResult> GetRandomTopic(int roomId)
        {
            try
            {
                var topic = await _botService.GetRandomTopicAsync(roomId);
                if (topic == null)
                {
                    return NotFound("No discussion topics available");
                }
                return Ok(topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting random topic for room {RoomId}", roomId);
                return StatusCode(500, "Error retrieving topic");
            }
        }

        #endregion

        #region Manual Bot Actions

        /// <summary>
        /// Envoie un message du bot manuellement (admin only)
        /// </summary>
        [HttpPost("message/{roomId}")]
        public async Task<IActionResult> SendBotMessage(int roomId, [FromBody] string message)
        {
            try
            {
                var userId = GetUserId();
                
                var canManage = await _roomService.CanManageRoomAsync(roomId, userId);
                if (!canManage)
                {
                    return Forbid("You don't have permission to send bot messages");
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    return BadRequest("Message cannot be empty");
                }

                await _botService.SendBotMessageAsync(roomId, message, "Bot");
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending bot message to room {RoomId}", roomId);
                return StatusCode(500, "Error sending message");
            }
        }

        /// <summary>
        /// Lance une question de quiz dans le salon
        /// </summary>
        [HttpPost("quiz/{roomId}/start")]
        public async Task<IActionResult> StartQuiz(int roomId)
        {
            try
            {
                var question = await _botService.GetRandomQuizQuestionAsync(roomId);
                if (question == null)
                {
                    return NotFound("No quiz questions available");
                }

                var quizMessage = $"🎯 **Quiz!** ({question.Category} - {question.Points} pts)\n\n{question.Question}";
                if (question.Options.Length > 0)
                {
                    quizMessage += "\n\nOptions: " + string.Join(" | ", question.Options.Select((o, i) => $"{(char)('A' + i)}) {o}"));
                }

                await _botService.SendBotMessageAsync(roomId, quizMessage, "BotQuiz");
                return Ok(new { questionId = question.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting quiz in room {RoomId}", roomId);
                return StatusCode(500, "Error starting quiz");
            }
        }

        /// <summary>
        /// Suggère un sujet de discussion dans le salon
        /// </summary>
        [HttpPost("topics/{roomId}/suggest")]
        public async Task<IActionResult> SuggestTopic(int roomId)
        {
            try
            {
                var topic = await _botService.GetRandomTopicAsync(roomId);
                if (topic == null)
                {
                    return NotFound("No discussion topics available");
                }

                await _botService.SendBotMessageAsync(roomId, $"💬 **Sujet de discussion:**\n{topic.Topic}", "Bot");
                return Ok(new { topicId = topic.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error suggesting topic in room {RoomId}", roomId);
                return StatusCode(500, "Error suggesting topic");
            }
        }

        #endregion
    }
}

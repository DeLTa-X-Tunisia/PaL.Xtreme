using FluentAssertions;
using System.Text.RegularExpressions;
using Xunit;

namespace PaLX.API.Tests
{
    /// <summary>
    /// Tests pour la logique de modération du Bot IA.
    /// Vérifie la détection des mots interdits et la logique d'avertissements.
    /// </summary>
    public class ModerationTests
    {
        #region Banned Word Detection Tests

        /// <summary>
        /// Vérifie la détection exacte d'un mot interdit (word boundary).
        /// </summary>
        [Theory]
        [InlineData("spam", "Ce message contient du spam ici", true)]
        [InlineData("spam", "spammer n'est pas interdit", false)]  // "spammer" != "spam"
        [InlineData("test", "C'est un test de modération", true)]
        [InlineData("test", "Ceci est un testing", false)]
        public void DetectBannedWord_WithWordBoundary_ShouldDetectCorrectly(string bannedWord, string message, bool expected)
        {
            var detected = DetectWithWordBoundary(bannedWord, message);
            detected.Should().Be(expected, $"word '{bannedWord}' in message '{message}'");
        }

        /// <summary>
        /// Vérifie la détection simple (contains) d'un mot interdit.
        /// </summary>
        [Theory]
        [InlineData("spam", "Ce message contient du spam ici", true)]
        [InlineData("spam", "spammer est aussi détecté", true)]  // Contains matches substring
        [InlineData("test", "testing aussi", true)]
        [InlineData("xyz", "Pas de match ici", false)]
        public void DetectBannedWord_WithContains_ShouldDetectCorrectly(string bannedWord, string message, bool expected)
        {
            var detected = DetectWithContains(bannedWord, message);
            detected.Should().Be(expected);
        }

        /// <summary>
        /// Vérifie que la détection est insensible à la casse.
        /// </summary>
        [Theory]
        [InlineData("spam", "SPAM EN MAJUSCULES", true)]
        [InlineData("SPAM", "spam en minuscules", true)]
        [InlineData("SpAm", "sPaM mixte", true)]
        public void DetectBannedWord_ShouldBeCaseInsensitive(string bannedWord, string message, bool expected)
        {
            var detected = DetectWithContains(bannedWord, message);
            detected.Should().Be(expected);
        }

        #endregion

        #region Warning System Tests

        /// <summary>
        /// Vérifie la logique de comptage des avertissements.
        /// </summary>
        [Theory]
        [InlineData(0, 3, false)] // 0 warnings, 3 max -> no kick
        [InlineData(1, 3, false)] // 1 warning, 3 max -> no kick
        [InlineData(2, 3, false)] // 2 warnings, 3 max -> no kick
        [InlineData(3, 3, true)]  // 3 warnings, 3 max -> KICK
        [InlineData(4, 3, true)]  // 4 warnings, 3 max -> KICK
        public void ShouldKickAfterWarnings(int currentWarnings, int maxWarnings, bool shouldKick)
        {
            var result = currentWarnings >= maxWarnings;
            result.Should().Be(shouldKick);
        }

        /// <summary>
        /// Vérifie que les avertissements expirés ne comptent pas.
        /// </summary>
        [Fact]
        public void ExpiredWarnings_ShouldNotCount()
        {
            var warnings = new[]
            {
                new TestWarning { CreatedAt = DateTime.UtcNow.AddMinutes(-120), IsActive = true }, // Expired (>60min)
                new TestWarning { CreatedAt = DateTime.UtcNow.AddMinutes(-30), IsActive = true },  // Active
                new TestWarning { CreatedAt = DateTime.UtcNow.AddMinutes(-10), IsActive = true },  // Active
            };

            int resetMinutes = 60;
            var activeCount = CountActiveWarnings(warnings, resetMinutes);

            activeCount.Should().Be(2, "Only 2 warnings are within the reset period");
        }

        #endregion

        #region Severity Tests

        /// <summary>
        /// Vérifie l'action selon la sévérité du mot interdit.
        /// </summary>
        [Theory]
        [InlineData("Warning", ModerationAction.Warn)]
        [InlineData("Kick", ModerationAction.Kick)]
        [InlineData("Ban", ModerationAction.Ban)]
        [InlineData("Unknown", ModerationAction.Warn)] // Default
        public void Severity_ShouldDetermineAction(string severity, ModerationAction expectedAction)
        {
            var action = GetActionForSeverity(severity);
            action.Should().Be(expectedAction);
        }

        #endregion

        #region Bot Command Detection Tests

        /// <summary>
        /// Vérifie la détection des commandes du bot.
        /// </summary>
        [Theory]
        [InlineData("!aide", true)]
        [InlineData("!quiz", true)]
        [InlineData("!sujet", true)]
        [InlineData("!topic", true)]
        [InlineData("!regles", true)]
        [InlineData("!invalid", false)]
        [InlineData("aide", false)]
        [InlineData("", false)]
        public void IsValidBotCommand(string message, bool expected)
        {
            var isCommand = IsKnownBotCommand(message.Trim().ToLower());
            isCommand.Should().Be(expected);
        }

        /// <summary>
        /// Vérifie la détection de mention du bot.
        /// </summary>
        [Theory]
        [InlineData("@Assistant aide moi", "Assistant", true)]
        [InlineData("Bonjour @Assistant", "Assistant", true)]
        [InlineData("@Bot aide", "Assistant", false)]
        [InlineData("Pas de mention", "Assistant", false)]
        public void IsBotMentioned(string message, string botName, bool expected)
        {
            var mentioned = message.Contains($"@{botName}", StringComparison.OrdinalIgnoreCase);
            mentioned.Should().Be(expected);
        }

        #endregion

        #region Helper Methods

        private static bool DetectWithWordBoundary(string word, string message)
        {
            var pattern = $@"\b{Regex.Escape(word)}\b";
            return Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase);
        }

        private static bool DetectWithContains(string word, string message)
        {
            return message.Contains(word, StringComparison.OrdinalIgnoreCase);
        }

        private static int CountActiveWarnings(IEnumerable<TestWarning> warnings, int resetMinutes)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-resetMinutes);
            return warnings.Count(w => w.IsActive && w.CreatedAt >= cutoff);
        }

        private static ModerationAction GetActionForSeverity(string severity)
        {
            return severity.ToLower() switch
            {
                "warning" => ModerationAction.Warn,
                "kick" => ModerationAction.Kick,
                "ban" => ModerationAction.Ban,
                _ => ModerationAction.Warn
            };
        }

        private static bool IsKnownBotCommand(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            
            return message == Constants.BotCommands.Help.ToLower() ||
                   message == Constants.BotCommands.Quiz.ToLower() ||
                   message == Constants.BotCommands.Topic.ToLower() ||
                   message == Constants.BotCommands.TopicAlias.ToLower() ||
                   message == Constants.BotCommands.Rules.ToLower();
        }

        #endregion

        #region Test Models

        private class TestWarning
        {
            public DateTime CreatedAt { get; set; }
            public bool IsActive { get; set; }
        }

        public enum ModerationAction
        {
            Warn,
            Kick,
            Ban
        }

        #endregion
    }
}

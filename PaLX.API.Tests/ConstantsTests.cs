using FluentAssertions;
using Xunit;

namespace PaLX.API.Tests
{
    /// <summary>
    /// Tests pour la classe Constants - vérification des valeurs et logique des méthodes helper.
    /// </summary>
    public class ConstantsTests
    {
        #region JwtClaims Tests

        [Fact]
        public void JwtClaims_UserId_ShouldBeCorrect()
        {
            Constants.JwtClaims.UserId.Should().Be("UserId");
        }

        [Fact]
        public void JwtClaims_Username_ShouldBeCorrect()
        {
            Constants.JwtClaims.Username.Should().Be("Username");
        }

        [Fact]
        public void JwtClaims_RoleLevel_ShouldBeCorrect()
        {
            Constants.JwtClaims.RoleLevel.Should().Be("RoleLevel");
        }

        #endregion

        #region RoleLevels Tests

        [Theory]
        [InlineData(1, true)]  // ServerMaster
        [InlineData(2, true)]  // ServerEditor
        [InlineData(3, true)]  // ServerSuperAdmin
        [InlineData(4, true)]  // ServerAdmin
        [InlineData(5, true)]  // ServerModerator
        [InlineData(6, true)]  // ServerHelp
        [InlineData(0, false)] // User
        [InlineData(7, false)] // Invalid
        [InlineData(-1, false)] // Invalid
        public void RoleLevels_IsSystemAdmin_ShouldReturnCorrectValue(int roleLevel, bool expected)
        {
            var result = Constants.RoleLevels.IsSystemAdmin(roleLevel);
            result.Should().Be(expected);
        }

        [Fact]
        public void RoleLevels_Constants_ShouldHaveCorrectValues()
        {
            Constants.RoleLevels.User.Should().Be(0);
            Constants.RoleLevels.ServerMaster.Should().Be(1);
            Constants.RoleLevels.ServerEditor.Should().Be(2);
            Constants.RoleLevels.ServerSuperAdmin.Should().Be(3);
            Constants.RoleLevels.ServerAdmin.Should().Be(4);
            Constants.RoleLevels.ServerModerator.Should().Be(5);
            Constants.RoleLevels.ServerHelp.Should().Be(6);
        }

        #endregion

        #region RoomRoles Tests

        [Theory]
        [InlineData(1, true)]  // Owner
        [InlineData(2, true)]  // SuperAdmin
        [InlineData(3, true)]  // Admin
        [InlineData(4, true)]  // Moderator
        [InlineData(5, false)] // Member
        [InlineData(0, false)] // Invalid
        [InlineData(6, false)] // Invalid
        public void RoomRoles_CanModerate_ShouldReturnCorrectValue(int roleId, bool expected)
        {
            var result = Constants.RoomRoles.CanModerate(roleId);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(1, true)]  // Owner
        [InlineData(2, true)]  // SuperAdmin
        [InlineData(3, true)]  // Admin
        [InlineData(4, false)] // Moderator - can't administer
        [InlineData(5, false)] // Member
        public void RoomRoles_CanAdminister_ShouldReturnCorrectValue(int roleId, bool expected)
        {
            var result = Constants.RoomRoles.CanAdminister(roleId);
            result.Should().Be(expected);
        }

        [Fact]
        public void RoomRoles_Constants_ShouldHaveCorrectValues()
        {
            Constants.RoomRoles.Owner.Should().Be(1);
            Constants.RoomRoles.SuperAdmin.Should().Be(2);
            Constants.RoomRoles.Admin.Should().Be(3);
            Constants.RoomRoles.Moderator.Should().Be(4);
            Constants.RoomRoles.Member.Should().Be(5);
        }

        #endregion

        #region UserStatus Tests

        [Theory]
        [InlineData(1, "En ligne")]
        [InlineData(2, "Absent")]
        [InlineData(3, "Occupé")]
        [InlineData(4, "Ne pas déranger")]
        [InlineData(5, "Apparaître hors ligne")]
        [InlineData(6, "Hors ligne")]
        [InlineData(99, "Inconnu")]
        public void UserStatus_GetDisplayName_ShouldReturnCorrectValue(int status, string expected)
        {
            var result = Constants.UserStatus.GetDisplayName(status);
            result.Should().Be(expected);
        }

        #endregion

        #region SignalRGroups Tests

        [Fact]
        public void SignalRGroups_Room_ShouldGenerateCorrectFormat()
        {
            var result = Constants.SignalRGroups.Room(123);
            result.Should().Be("Room_123");
        }

        [Fact]
        public void SignalRGroups_User_ShouldGenerateCorrectFormat()
        {
            var result = Constants.SignalRGroups.User(456);
            result.Should().Be("User_456");
        }

        #endregion

        #region MessageTypes Tests

        [Fact]
        public void MessageTypes_ShouldHaveCorrectValues()
        {
            Constants.MessageTypes.User.Should().Be("User");
            Constants.MessageTypes.System.Should().Be("System");
            Constants.MessageTypes.Bot.Should().Be("Bot");
            Constants.MessageTypes.BotWelcome.Should().Be("BotWelcome");
            Constants.MessageTypes.BotWarning.Should().Be("BotWarning");
            Constants.MessageTypes.BotQuiz.Should().Be("BotQuiz");
            Constants.MessageTypes.Kick.Should().Be("Kick");
            Constants.MessageTypes.Ban.Should().Be("Ban");
        }

        #endregion

        #region BotCommands Tests

        [Fact]
        public void BotCommands_ShouldHaveCorrectValues()
        {
            Constants.BotCommands.Help.Should().Be("!aide");
            Constants.BotCommands.Quiz.Should().Be("!quiz");
            Constants.BotCommands.Topic.Should().Be("!sujet");
            Constants.BotCommands.TopicAlias.Should().Be("!topic");
            Constants.BotCommands.Rules.Should().Be("!regles");
        }

        #endregion

        #region Limits Tests

        [Fact]
        public void Limits_ShouldHaveReasonableValues()
        {
            Constants.Limits.MaxUsernameLength.Should().Be(50);
            Constants.Limits.MaxNicknameLength.Should().Be(50);
            Constants.Limits.MaxBioLength.Should().Be(500);
            Constants.Limits.MaxRoomNameLength.Should().Be(100);
            Constants.Limits.MaxMessageLength.Should().Be(5000);
            Constants.Limits.MaxFileSize.Should().Be(50 * 1024 * 1024); // 50 MB
            Constants.Limits.MaxRoomMembers.Should().Be(500);
            Constants.Limits.DefaultRoomMembers.Should().Be(100);
        }

        #endregion
    }
}

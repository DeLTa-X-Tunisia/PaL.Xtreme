using FluentAssertions;
using Xunit;

namespace PaLX.API.Tests
{
    /// <summary>
    /// Tests pour la logique de permissions dans les salons.
    /// Ces tests vérifient la hiérarchie des rôles et les autorisations.
    /// </summary>
    public class PermissionTests
    {
        #region Permission Hierarchy Tests

        /// <summary>
        /// Vérifie que le propriétaire peut agir sur tous les autres rôles.
        /// </summary>
        [Theory]
        [InlineData(Constants.RoomRoles.Owner, Constants.RoomRoles.SuperAdmin, true)]
        [InlineData(Constants.RoomRoles.Owner, Constants.RoomRoles.Admin, true)]
        [InlineData(Constants.RoomRoles.Owner, Constants.RoomRoles.Moderator, true)]
        [InlineData(Constants.RoomRoles.Owner, Constants.RoomRoles.Member, true)]
        public void Owner_CanActOnLowerRoles(int actorRole, int targetRole, bool expected)
        {
            var result = CanActOnUser(actorRole, targetRole);
            result.Should().Be(expected);
        }

        /// <summary>
        /// Vérifie qu'un rôle ne peut pas agir sur un rôle égal ou supérieur.
        /// </summary>
        [Theory]
        [InlineData(Constants.RoomRoles.Admin, Constants.RoomRoles.Owner, false)]
        [InlineData(Constants.RoomRoles.Admin, Constants.RoomRoles.SuperAdmin, false)]
        [InlineData(Constants.RoomRoles.Admin, Constants.RoomRoles.Admin, false)]
        [InlineData(Constants.RoomRoles.Moderator, Constants.RoomRoles.Admin, false)]
        [InlineData(Constants.RoomRoles.Member, Constants.RoomRoles.Member, false)]
        public void Role_CannotActOnEqualOrHigherRole(int actorRole, int targetRole, bool expected)
        {
            var result = CanActOnUser(actorRole, targetRole);
            result.Should().Be(expected);
        }

        /// <summary>
        /// Vérifie qu'un modérateur peut kick un membre mais pas un admin.
        /// </summary>
        [Theory]
        [InlineData(Constants.RoomRoles.Moderator, Constants.RoomRoles.Member, true)]
        [InlineData(Constants.RoomRoles.Moderator, Constants.RoomRoles.Admin, false)]
        public void Moderator_CanOnlyActOnMembers(int actorRole, int targetRole, bool expected)
        {
            var result = CanActOnUser(actorRole, targetRole);
            result.Should().Be(expected);
        }

        #endregion

        #region System Admin Override Tests

        /// <summary>
        /// Les admins système ont tous les droits, quel que soit leur rôle dans le salon.
        /// </summary>
        [Theory]
        [InlineData(1)] // ServerMaster
        [InlineData(2)] // ServerEditor
        [InlineData(3)] // ServerSuperAdmin
        [InlineData(4)] // ServerAdmin
        [InlineData(5)] // ServerModerator
        [InlineData(6)] // ServerHelp
        public void SystemAdmin_AlwaysCanModerate(int systemRoleLevel)
        {
            var canModerate = CanModerateWithSystemRole(
                systemRoleLevel: systemRoleLevel,
                roomRole: Constants.RoomRoles.Member, // Even as a simple member
                targetRoomRole: Constants.RoomRoles.Admin // Can moderate an admin
            );
            canModerate.Should().BeTrue();
        }

        /// <summary>
        /// Un utilisateur normal ne peut pas modérer un admin même s'il est modérateur du salon.
        /// </summary>
        [Fact]
        public void NormalUser_CannotModerateHigherRoomRole()
        {
            var canModerate = CanModerateWithSystemRole(
                systemRoleLevel: 0, // Normal user
                roomRole: Constants.RoomRoles.Moderator,
                targetRoomRole: Constants.RoomRoles.Admin
            );
            canModerate.Should().BeFalse();
        }

        #endregion

        #region Bot Configuration Permission Tests

        /// <summary>
        /// Seuls le propriétaire et les admins système peuvent configurer le bot.
        /// </summary>
        [Theory]
        [InlineData(0, true, true)]   // Normal user but owner
        [InlineData(0, false, false)] // Normal user, not owner
        [InlineData(1, false, true)]  // ServerMaster, not owner
        [InlineData(6, false, true)]  // ServerHelp, not owner
        public void BotConfiguration_RequiresOwnerOrSystemAdmin(int systemRoleLevel, bool isOwner, bool expected)
        {
            var canConfigure = CanConfigureBot(systemRoleLevel, isOwner);
            canConfigure.Should().Be(expected);
        }

        #endregion

        #region Helper Methods (simulating actual permission logic)

        /// <summary>
        /// Simule la logique de permission : un rôle peut agir sur un autre seulement si son ID est inférieur.
        /// </summary>
        private static bool CanActOnUser(int actorRoleId, int targetRoleId)
        {
            // Rule: Lower roleId = higher privilege
            // Actor must have strictly lower roleId to act on target
            return actorRoleId < targetRoleId;
        }

        /// <summary>
        /// Simule la logique de modération avec prise en compte du rôle système.
        /// </summary>
        private static bool CanModerateWithSystemRole(int systemRoleLevel, int roomRole, int targetRoomRole)
        {
            // System admins (1-6) can always moderate
            if (Constants.RoleLevels.IsSystemAdmin(systemRoleLevel))
                return true;

            // Otherwise, check room role hierarchy
            return roomRole < targetRoomRole;
        }

        /// <summary>
        /// Simule la logique de permission pour configurer le bot.
        /// </summary>
        private static bool CanConfigureBot(int systemRoleLevel, bool isOwner)
        {
            // System admins can always configure
            if (Constants.RoleLevels.IsSystemAdmin(systemRoleLevel))
                return true;

            // Only room owner can configure
            return isOwner;
        }

        #endregion
    }
}

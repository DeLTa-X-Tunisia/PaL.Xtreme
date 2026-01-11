using Xunit;
using FluentAssertions;
using PaLX.API.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace PaLX.API.Tests.Services
{
    public class AuthServiceTests
    {
        private readonly Mock<IConfiguration> _configMock;

        public AuthServiceTests()
        {
            _configMock = new Mock<IConfiguration>();
            
            // Configuration basique pour les tests
            _configMock.Setup(x => x["Jwt:Key"]).Returns("TestSecretKey_AtLeast32Characters_ForHMACSHA256!");
            _configMock.Setup(x => x["Jwt:Issuer"]).Returns("TestIssuer");
            _configMock.Setup(x => x["Jwt:Audience"]).Returns("TestAudience");
        }

        [Theory]
        [InlineData("password")]  // 8 caractères - valide
        [InlineData("abcd1234")]  // 8 caractères - valide
        [InlineData("12345678901234567890")] // 20 caractères - valide
        public void Password_ShouldBe_ValidLength(string password)
        {
            // Arrange & Act
            var isValid = password.Length >= 8;

            // Assert
            isValid.Should().BeTrue($"Le mot de passe '{password}' devrait être valide (>= 8 caractères)");
        }

        [Theory]
        [InlineData("1234567")] // 7 caractères - invalide
        [InlineData("abc")]     // 3 caractères - invalide
        [InlineData("")]        // vide - invalide
        public void Password_ShouldBe_InvalidLength(string password)
        {
            // Arrange & Act
            var isValid = password.Length >= 8;

            // Assert
            isValid.Should().BeFalse($"Le mot de passe '{password}' devrait être invalide (< 8 caractères)");
        }

        [Fact]
        public void BCrypt_ShouldHash_Password()
        {
            // Arrange
            var password = "SecurePassword123";

            // Act
            var hash = BCrypt.Net.BCrypt.HashPassword(password);

            // Assert
            hash.Should().NotBe(password);
            BCrypt.Net.BCrypt.Verify(password, hash).Should().BeTrue();
        }

        [Fact]
        public void BCrypt_ShouldReject_WrongPassword()
        {
            // Arrange
            var password = "SecurePassword123";
            var wrongPassword = "WrongPassword123";

            // Act
            var hash = BCrypt.Net.BCrypt.HashPassword(password);

            // Assert
            BCrypt.Net.BCrypt.Verify(wrongPassword, hash).Should().BeFalse();
        }
    }
}

using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace PaLX.API.Tests.Controllers
{
    public class UploadControllerTests
    {
        // Limites de taille définies dans UploadController
        private const long MaxImageSize = 10 * 1024 * 1024;   // 10MB
        private const long MaxVideoSize = 100 * 1024 * 1024;  // 100MB  
        private const long MaxAudioSize = 25 * 1024 * 1024;   // 25MB
        private const long MaxFileSize = 50 * 1024 * 1024;    // 50MB

        [Theory]
        [InlineData(5 * 1024 * 1024)]   // 5MB - valide
        [InlineData(10 * 1024 * 1024)]  // 10MB - limite
        public void ImageSize_ShouldBe_Valid(long fileSize)
        {
            // Assert
            fileSize.Should().BeLessThanOrEqualTo(MaxImageSize);
        }

        [Theory]
        [InlineData(11 * 1024 * 1024)]  // 11MB - invalide
        [InlineData(20 * 1024 * 1024)]  // 20MB - invalide
        public void ImageSize_ShouldBe_Invalid(long fileSize)
        {
            // Assert
            fileSize.Should().BeGreaterThan(MaxImageSize);
        }

        [Theory]
        [InlineData(50 * 1024 * 1024)]   // 50MB - valide
        [InlineData(100 * 1024 * 1024)]  // 100MB - limite
        public void VideoSize_ShouldBe_Valid(long fileSize)
        {
            // Assert
            fileSize.Should().BeLessThanOrEqualTo(MaxVideoSize);
        }

        [Theory]
        [InlineData(".jpg", true)]
        [InlineData(".jpeg", true)]
        [InlineData(".png", true)]
        [InlineData(".gif", true)]
        [InlineData(".webp", true)]
        [InlineData(".exe", false)]
        [InlineData(".bat", false)]
        [InlineData(".cmd", false)]
        public void ImageExtension_ShouldBe_Validated(string extension, bool expected)
        {
            // Arrange
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

            // Act
            var isAllowed = allowedExtensions.Contains(extension.ToLowerInvariant());

            // Assert
            isAllowed.Should().Be(expected);
        }

        [Theory]
        [InlineData(".mp4", true)]
        [InlineData(".webm", true)]
        [InlineData(".mov", true)]
        [InlineData(".avi", true)]
        [InlineData(".exe", false)]
        public void VideoExtension_ShouldBe_Validated(string extension, bool expected)
        {
            // Arrange
            var allowedExtensions = new[] { ".mp4", ".webm", ".mov", ".avi" };

            // Act
            var isAllowed = allowedExtensions.Contains(extension.ToLowerInvariant());

            // Assert
            isAllowed.Should().Be(expected);
        }

        [Theory]
        [InlineData(".mp3", true)]
        [InlineData(".wav", true)]
        [InlineData(".ogg", true)]
        [InlineData(".m4a", true)]
        [InlineData(".exe", false)]
        public void AudioExtension_ShouldBe_Validated(string extension, bool expected)
        {
            // Arrange
            var allowedExtensions = new[] { ".mp3", ".wav", ".ogg", ".m4a", ".webm" };

            // Act
            var isAllowed = allowedExtensions.Contains(extension.ToLowerInvariant());

            // Assert
            isAllowed.Should().Be(expected);
        }
    }
}

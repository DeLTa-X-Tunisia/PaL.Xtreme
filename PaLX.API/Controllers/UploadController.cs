using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PaLX.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        
        // ═══════════════════════════════════════════════════════════════════
        // LIMITES DE TAILLE DES FICHIERS (Protection DoS)
        // ═══════════════════════════════════════════════════════════════════
        private const long MaxImageSizeBytes = 10 * 1024 * 1024;      // 10 MB
        private const long MaxVideoSizeBytes = 100 * 1024 * 1024;     // 100 MB
        private const long MaxAudioSizeBytes = 25 * 1024 * 1024;      // 25 MB
        private const long MaxFileSizeBytes = 50 * 1024 * 1024;       // 50 MB

        public UploadController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpPost("image")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Aucun fichier fourni.");
            
            // Validation taille maximale
            if (file.Length > MaxImageSizeBytes)
                return BadRequest($"Fichier trop volumineux. Taille max: {MaxImageSizeBytes / (1024 * 1024)} MB.");

            // Validate extension
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Format de fichier non supporté.");

            // Ensure directory exists
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return URL
            var url = $"/uploads/{fileName}";
            return Ok(new { Url = url });
        }

        [HttpPost("video")]
        [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB
        public async Task<IActionResult> UploadVideo(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Aucun fichier fourni.");
            
            // Validation taille maximale
            if (file.Length > MaxVideoSizeBytes)
                return BadRequest($"Fichier trop volumineux. Taille max: {MaxVideoSizeBytes / (1024 * 1024)} MB.");

            // Validate extension
            var allowedExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".webm" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Format de fichier non supporté.");

            // Ensure directory exists
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return URL
            var url = $"/uploads/{fileName}";
            return Ok(new { Url = url });
        }

        [HttpPost("audio")]
        [RequestSizeLimit(25 * 1024 * 1024)] // 25 MB
        public async Task<IActionResult> UploadAudio(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Aucun fichier fourni.");
            
            // Validation taille maximale
            if (file.Length > MaxAudioSizeBytes)
                return BadRequest($"Fichier trop volumineux. Taille max: {MaxAudioSizeBytes / (1024 * 1024)} MB.");

            // Validate extension
            var allowedExtensions = new[] { ".mp3", ".wav", ".ogg", ".m4a", ".aac", ".wma", ".flac" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Format de fichier non supporté.");

            // Ensure directory exists
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return URL
            var url = $"/uploads/{fileName}";
            return Ok(new { Url = url });
        }

        [HttpPost("file")]
        [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Aucun fichier fourni.");
            
            // Validation taille maximale
            if (file.Length > MaxFileSizeBytes)
                return BadRequest($"Fichier trop volumineux. Taille max: {MaxFileSizeBytes / (1024 * 1024)} MB.");

            // Validate extension
            var allowedExtensions = new[] { 
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", 
                ".zip", ".rar", ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".mp4" 
            };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Format de fichier non supporté ou interdit.");

            // Ensure directory exists
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return URL
            var url = $"/uploads/{fileName}";
            return Ok(new { Url = url });
        }
    }
}

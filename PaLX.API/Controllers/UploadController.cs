using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

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
            try
            {
                Log.Information("[UPLOAD] UploadImage appelé - file: {FileName}, size: {Size}", file?.FileName, file?.Length);
                
                if (file == null || file.Length == 0)
                {
                    Log.Warning("[UPLOAD] Aucun fichier fourni");
                    return BadRequest("Aucun fichier fourni.");
                }
                
                // Validation taille maximale
                if (file.Length > MaxImageSizeBytes)
                    return BadRequest($"Fichier trop volumineux. Taille max: {MaxImageSizeBytes / (1024 * 1024)} MB.");

                // Validate extension
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    return BadRequest("Format de fichier non supporté.");

                // Ensure directory exists - use fallback if WebRootPath is null
                var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
                var uploadsPath = Path.Combine(webRootPath, "uploads");
                Log.Information("[UPLOAD] WebRootPath: {WebRootPath}, UploadsPath: {UploadsPath}", webRootPath, uploadsPath);
                
                if (!Directory.Exists(uploadsPath))
                    Directory.CreateDirectory(uploadsPath);

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                Log.Information("[UPLOAD] Fichier sauvegardé: {FilePath}", filePath);
                
                // Return URL
                var url = $"/uploads/{fileName}";
                return Ok(new { url = url });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UPLOAD] Erreur lors de l'upload de l'image");
                return StatusCode(500, new { error = ex.Message });
            }
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
            return Ok(new { url = url });
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
            return Ok(new { url = url });
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
            return Ok(new { url = url });
        }
    }
}

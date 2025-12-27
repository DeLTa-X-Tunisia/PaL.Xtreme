using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PaLX.API.Services;
using PaLX.API.Models;
using System.Security.Claims;

namespace PaLX.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IFileService _fileService;

        public ChatController(IWebHostEnvironment environment, IFileService fileService)
        {
            _environment = environment;
            _fileService = fileService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile file, [FromForm] string receiver)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Aucun fichier reçu.");

            if (string.IsNullOrEmpty(receiver))
                return BadRequest("Destinataire manquant.");

            // 1. Check Size (5MB = 5 * 1024 * 1024 bytes)
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest("L'image dépasse la taille limite de 5 MB.");

            // 2. Check Extension
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Format de fichier non supporté. Utilisez JPG, PNG ou GIF.");

            try
            {
                // 3. Prepare Path
                string webRootPath = _environment.WebRootPath;
                if (string.IsNullOrEmpty(webRootPath))
                {
                    webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
                }

                var uploadsFolder = Path.Combine(webRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // 4. Generate Unique Name
                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // 5. Save File
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 6. Generate URL
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var url = $"{baseUrl}/uploads/{uniqueFileName}";

                // 7. Save to DB
                var sender = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name;
                if (string.IsNullOrEmpty(sender)) return Unauthorized();

                var transfer = new FileTransfer
                {
                    SenderUsername = sender,
                    ReceiverUsername = receiver,
                    FileName = file.FileName,
                    FilePath = url,
                    FileSize = file.Length,
                    ContentType = file.ContentType,
                    SentAt = DateTime.Now,
                    Status = 0 // Pending
                };

                var id = await _fileService.CreateFileTransferAsync(transfer);

                return Ok(new { Id = id, Url = url, FileName = file.FileName, FileSize = file.Length });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur interne: {ex.Message}");
            }
        }
    }
}

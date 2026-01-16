using System;

namespace PaLX.API.DTOs
{
    public class BlockRequestModel
    {
        public string BlockedUsername { get; set; } = string.Empty;
        public int BlockType { get; set; } // 0: Indefinite, 1: 1 Week, 2: Custom
        public DateTime? EndDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class BlockedUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
        public int BlockType { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Reason { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    public class ChatMessageDto
    {
        public int Id { get; set; }
        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
    }

    /// <summary>
    /// Requête pour envoyer une annonce globale
    /// </summary>
    public class BroadcastRequest
    {
        /// <summary>
        /// Type d'annonce: "info", "warning", "alert", "success"
        /// </summary>
        public string? Type { get; set; }
        
        /// <summary>
        /// Titre de l'annonce
        /// </summary>
        public string? Title { get; set; }
        
        /// <summary>
        /// Contenu du message
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Historique d'une annonce
    /// </summary>
    public class BroadcastHistoryDto
    {
        public int Id { get; set; }
        public int SentByUserId { get; set; }
        public string SentByUsername { get; set; } = string.Empty;
        public string SentByDisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = "info";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // ROOM CATEGORIES DTOs (Admin Panel - Extended)
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// DTO étendu pour l'admin panel avec toutes les propriétés
    /// </summary>
    public class AdminRoomCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string Color { get; set; } = "#3498DB";
        public string TextColor { get; set; } = "#FFFFFF";
        public int Order { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int SubCategoriesCount { get; set; }
        public int RoomsCount { get; set; }
    }

    public class CreateCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string Color { get; set; } = "#3498DB";
        public string TextColor { get; set; } = "#FFFFFF";
        public int Order { get; set; } = 0;
        public bool IsVisible { get; set; } = true;
        public bool IsActive { get; set; } = true;
    }

    public class UpdateCategoryDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public string? TextColor { get; set; }
        public int? Order { get; set; }
        public bool? IsVisible { get; set; }
        public bool? IsActive { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // ROOM SUBCATEGORIES DTOs (Admin Panel - Extended)
    // ═══════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// DTO étendu pour l'admin panel avec toutes les propriétés
    /// </summary>
    public class AdminRoomSubCategoryDto
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string Color { get; set; } = "#6C757D";
        public string TextColor { get; set; } = "#FFFFFF";
        public int DisplayOrder { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int RoomsCount { get; set; }
    }

    public class CreateSubCategoryDto
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string Color { get; set; } = "#6C757D";
        public string TextColor { get; set; } = "#FFFFFF";
        public int DisplayOrder { get; set; } = 0;
        public bool IsVisible { get; set; } = true;
        public bool IsActive { get; set; } = true;
    }

    public class UpdateSubCategoryDto
    {
        public int? CategoryId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public string? TextColor { get; set; }
        public int? DisplayOrder { get; set; }
        public bool? IsVisible { get; set; }
        public bool? IsActive { get; set; }
    }
}

namespace PaLX.API.DTOs
{
    /// <summary>
    /// DTO pour une définition de rôle de salon
    /// </summary>
    public class RoomRoleDefinitionDto
    {
        public int Id { get; set; }
        public int RoleLevel { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Icon { get; set; } = "user";
        public string Color { get; set; } = "#95A5A6";
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<RoomPermissionDto> Permissions { get; set; } = new();
    }

    /// <summary>
    /// DTO pour une permission de salon
    /// </summary>
    public class RoomPermissionDto
    {
        public int Id { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = "general";
        public bool IsActive { get; set; }
        public bool IsEnabled { get; set; } // Si la permission est attribuée au rôle
    }

    /// <summary>
    /// DTO pour créer un nouveau rôle personnalisé
    /// </summary>
    public class CreateRoomRoleDto
    {
        public int RoleLevel { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Icon { get; set; } = "user";
        public string Color { get; set; } = "#95A5A6";
        public List<int> PermissionIds { get; set; } = new();
    }

    /// <summary>
    /// DTO pour modifier un rôle existant
    /// </summary>
    public class UpdateRoomRoleDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Icon { get; set; } = "user";
        public string Color { get; set; } = "#95A5A6";
        public bool IsActive { get; set; } = true;
        public List<int> PermissionIds { get; set; } = new();
    }

    /// <summary>
    /// DTO pour lister toutes les permissions disponibles
    /// </summary>
    public class PermissionListDto
    {
        public string Category { get; set; } = string.Empty;
        public string CategoryDisplayName { get; set; } = string.Empty;
        public List<RoomPermissionDto> Permissions { get; set; } = new();
    }

    /// <summary>
    /// Résultat d'opération CRUD sur les rôles
    /// </summary>
    public class RoomRoleOperationResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public RoomRoleDefinitionDto? Role { get; set; }
    }
}

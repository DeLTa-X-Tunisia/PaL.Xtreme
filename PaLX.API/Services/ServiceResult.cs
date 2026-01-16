namespace PaLX.API.Services;

/// <summary>
/// Represents the result of a service operation
/// </summary>
public class ServiceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool IsNotFound { get; set; }

    public static ServiceResult Ok(string? message = null) => new() { Success = true, Message = message };
    public static ServiceResult Error(string? message = null) => new() { Success = false, Message = message };
    public static ServiceResult NotFound(string? message = null) => new() { Success = false, Message = message, IsNotFound = true };
}

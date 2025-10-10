namespace Shared.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T? Data { get; set; }
    public ErrorDetail? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() 
        { 
            Success = true, 
            Message = message ?? "Request successful", 
            Data = data
        };
    
    public static ApiResponse<T> Fail(string message, ErrorDetail? error = null) =>
        new() 
        { 
            Success = false, 
            Message = message, 
            Error = error 
        };
    
    public static ApiResponse<T> Fail(string message, string errorCode) =>
        new() 
        { 
            Success = false, 
            Message = message, 
            Error = new ErrorDetail 
            { 
                ErrorCode = errorCode, 
                ErrorMessage = message 
            }
        };
}
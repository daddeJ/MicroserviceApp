namespace Shared.Models;

public class ErrorDetail
{
    public string ErrorCode  { get; set; }
    public string ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shared.Models;

namespace UserService.Helpers;

public static class ApiResponseFactory
{
    public static IActionResult Ok<T>(T data, string? message = null)
        => new OkObjectResult(ApiResponse<T>.Ok(data, message));

    public static IActionResult BadRequest<T>(
        string message,
        string errorCode = "VAL_001",
        Dictionary<string, string[]>? validationErrors = null)
        => new BadRequestObjectResult(ApiResponse<T>.Fail(message, new ErrorDetail
        {
            ErrorCode = errorCode,
            ErrorMessage = message,
            ValidationErrors = validationErrors
        }));

    public static IActionResult Unauthorized<T>(string message, string errorCode = "AUTH_001")
        => new UnauthorizedObjectResult(ApiResponse<T>.Fail(message, errorCode));

    public static IActionResult NotFound<T>(string message, string errorCode = "NOT_FOUND_001")
        => new NotFoundObjectResult(ApiResponse<T>.Fail(message, errorCode));

    public static IActionResult ServerError<T>(string message, string errorCode = "SRV_001")
        => new ObjectResult(ApiResponse<T>.Fail(message, errorCode))
        {
            StatusCode = 500
        };

    public static IActionResult ValidationFromModelState(ModelStateDictionary modelState)
    {
        var validationErrors = modelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors
                    .Select(e => e.ErrorMessage)
                    .ToArray()
            );

        return BadRequest<object>(
            "Model validation failed.",
            "VAL_002",
            validationErrors
        );
    }

    public static IActionResult ValidationError(string field, string message, string errorCode = "VAL_003")
    {
        var errors = new Dictionary<string, string[]>
        {
            { field, new[] { message } }
        };

        return BadRequest<object>(
            "Validation failed.",
            errorCode,
            errors
        );
    }

    public static IActionResult InvalidAllowedValues<T>(
        string field,
        T invalidValue,
        IEnumerable<T> allowedValues,
        string errorCode = "VAL_004")
    {
        var message = $"Invalid value '{invalidValue}' for {field}. Allowed values: {string.Join(", ", allowedValues)}";

        var errors = new Dictionary<string, string[]>
        {
            { field, new[] { message } }
        };

        return BadRequest<object>(
            "Validation failed.",
            errorCode,
            errors
        );
    }

    public static IActionResult ValidationCustom(
        string message,
        string errorCode = "VAL_005",
        Dictionary<string, string[]>? details = null)
        => BadRequest<object>(message, errorCode, details);
}

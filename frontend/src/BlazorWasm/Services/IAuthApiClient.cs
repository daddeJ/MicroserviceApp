using BlazorWasm.Models;

namespace BlazorWasm.Services;

public interface IAuthApiClient
{
    Task<ApiResponse<ValidateToken>?> ValidateTokenAsync(string userId, string token, string operation);
}
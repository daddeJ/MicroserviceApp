using BlazorWasm.Models;

namespace BlazorWasm.Services;

public interface IUserApiClient
{
    Task<ApiResponse<AuthData>?> LoginAsync(LoginRequest loginRequest);
    Task<ApiResponse<AuthData>?> RegisterAsync(RegisterRequest registerRequest);
}
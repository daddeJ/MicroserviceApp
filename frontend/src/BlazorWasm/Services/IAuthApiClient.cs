using BlazorWasm.Models;

namespace BlazorWasm.Services;

public interface IAuthApiClient
{
    Task<AuthResponse?> LoginAsync(LoginRequest loginRequest);
}
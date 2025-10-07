using BlazorWasm.Models;

namespace BlazorWasm.Services;

public interface IUserApiClient
{
    Task<RegisterResponse?> RegisterAsync(RegisterRequest registerRequest);
}
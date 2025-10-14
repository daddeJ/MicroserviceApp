using System.Net.Http.Json;
using BlazorWasm.Models;

namespace BlazorWasm.Services;

public class UserApiClient : IUserApiClient
{
    private readonly HttpClient _httpClient;

    public UserApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiResponse<AuthData>?> LoginAsync(LoginRequest loginRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/user/login", loginRequest);
        return await response.Content.ReadFromJsonAsync<ApiResponse<AuthData>>();
    }

    public async Task<ApiResponse<AuthData>?> RegisterAsync(RegisterRequest registerRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/user/register", registerRequest);
        return await response.Content.ReadFromJsonAsync<ApiResponse<AuthData>>();
    }
}
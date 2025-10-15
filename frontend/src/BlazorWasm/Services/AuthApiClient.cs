using System.Net.Http.Json;
using BlazorWasm.Models;

namespace BlazorWasm.Services;

public class AuthApiClient : IAuthApiClient
{
    private readonly HttpClient _httpClient;

    public AuthApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    public async Task<ApiResponse<ValidateToken>?> ValidateTokenAsync(string userId, string token, string operation)
    {
        var validationRequest = new
        {
            userId = userId,
            token = token,
            operation = operation
        };

        var response = await _httpClient.PostAsJsonAsync("api/auth/validate", validationRequest);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ValidateToken>?>();
    }
}
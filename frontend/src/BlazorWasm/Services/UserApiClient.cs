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
    
    public async Task<RegisterResponse?> RegisterAsync(RegisterRequest registerRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/user/register", registerRequest);
        if (!response.IsSuccessStatusCode)
            return null;
        
        return await response.Content.ReadFromJsonAsync<RegisterResponse>();
    }
}
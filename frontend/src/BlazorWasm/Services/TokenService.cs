using Blazored.LocalStorage;

namespace BlazorWasm.Services;

public class TokenService : ITokenService
{
    private const string TokenKey = "authToken";
    private readonly ILocalStorageService _localStorage;

    public TokenService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task SaveTokenAsync(string token)
    {
        await _localStorage.SetItemAsync(TokenKey, token);
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsync<string>(TokenKey);
    }
    
    public async Task RemoveTokenAsync()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
    }
}
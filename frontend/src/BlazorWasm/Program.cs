using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using BlazorWasm;
using BlazorWasm.Providers;
using BlazorWasm.Services;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

var baseUrl = builder.Configuration["ApiUrls:Microservice"]
              ?? throw new InvalidOperationException("Microservice URL not configured");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseUrl) });

// Reuse the same base HttpClient for all API clients
builder.Services.AddScoped<IAuthApiClient, AuthApiClient>();
builder.Services.AddScoped<IUserApiClient, UserApiClient>();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddMudServices();

await builder.Build().RunAsync();
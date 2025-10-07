using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorWasm;
using BlazorWasm.Providers;
using BlazorWasm.Services;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

var userServiceUrl = builder.Configuration["ApiUrls:UserService"] 
                     ?? throw new InvalidOperationException("UserService URL not configured");
var authServiceUrl = builder.Configuration["ApiUrls:AuthService"] 
                     ?? throw new InvalidOperationException("AuthService URL not configured");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(userServiceUrl) });
builder.Services.AddScoped<IAuthApiClient>(sp => 
    new AuthApiClient(new HttpClient { BaseAddress = new Uri(authServiceUrl) }));
builder.Services.AddScoped<IUserApiClient>(sp => 
    new UserApiClient(new HttpClient { BaseAddress = new Uri(userServiceUrl) }));

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddMudServices();

await builder.Build().RunAsync();
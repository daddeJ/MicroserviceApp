using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Shared.Extensions;
using Shared.Helpers;
using UserService.Data;
using UserService.Data.Transactions;
using UserService.Helpers;
using UserService.Messaging;
using UserService.Services;

var builder = WebApplication.CreateBuilder(args);

// ----------------------
// Configuration & Services
// ----------------------
builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddSharedFactories(builder.Configuration);

builder.Services.AddSingleton<IEventPublisher, EventPublisher>();

builder.Services.AddScoped<IUserRegistrationTransaction, UserRegistrationTransaction>();
builder.Services.AddScoped<IUserService, UserServiceImp>();
builder.Services.AddScoped<IPublisherService, PublishedService>();

builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("UserServiceConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<UserDbContext>()
    .AddDefaultTokenProviders();

// ----------------------
// Controllers, Swagger & Health Checks
// ----------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Optional: built-in health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Seed Roles
await DataSeeder.SeedRoles(app.Services);

// ----------------------
// Middleware
// ----------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// ----------------------
// Health Check Endpoint
// ----------------------
app.MapGet("/api/user/health", async (UserDbContext dbContext) =>
{
    try
    {
        var canConnect = await dbContext.Database.CanConnectAsync();
        return Results.Ok(new
        {
            status = canConnect ? "Healthy" : "Degraded",
            timestamp = DateTime.UtcNow,
            database = canConnect ? "Connected" : "Disconnected"
        });
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Health check database connection failed");
        return Results.Ok(new
        {
            status = "Degraded",
            timestamp = DateTime.UtcNow,
            database = "Error",
            error = ex.Message
        });
    }
});

// ----------------------
// Run App
// ----------------------
app.Run();

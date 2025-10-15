using AuthService.Data;
using AuthService.Messaging;
using AuthService.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Shared.Extensions;
using Shared.Helpers;

var builder = WebApplication.CreateBuilder(args);

// ========================
// Add configuration and shared infrastructure
// ========================
builder.Configuration.AddUserSecrets<Program>();
builder.Services.AddSharedInfrastructure(builder.Configuration);

// ========================
// Register DI services
// ========================
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();
builder.Services.AddSingleton<IAuthService, AuthServiceImp>();

// Event consumer
builder.Services.AddSingleton<EventConsumer>();

// ========================
// Configure DbContext with transient retry
// ========================
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AuthServiceConnection"),
        sql => sql.EnableRetryOnFailure() // retry transient DB errors
    )
);

// ========================
// Add Controllers & Swagger
// ========================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ========================
// Build app
// ========================
var app = builder.Build();

// ========================
// Ensure DB exists & migrations applied
// ========================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await dbContext.Database.MigrateAsync(); // create DB if missing & apply migrations
}

// ========================
// Swagger for development
// ========================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ========================
// Middleware
// ========================
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// ========================
// Health check endpoint
// ========================
app.MapGet("/api/auth/health", async (AuthDbContext dbContext) =>
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

// ========================
// Start message consumer
// ========================
var consumer = app.Services.GetRequiredService<EventConsumer>();
consumer.StartConsuming();

// ========================
// Run the app
// ========================
app.Run();

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

// ----------------------
// Configure DbContext
// ----------------------
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("UserServiceConnection"),
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)
    )
);

// ----------------------
// Identity
// ----------------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<UserDbContext>()
.AddDefaultTokenProviders();

// ----------------------
// Controllers, Swagger & Health Checks
// ----------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

// ----------------------
// Database Setup & Seeding
// ----------------------
await InitializeDatabaseAsync(app);

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
            database = canConnect ? "Connected" : "Disconnected",
            service = "UserService"
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            status = "Unhealthy",
            timestamp = DateTime.UtcNow,
            database = "Error",
            error = ex.Message,
            service = "UserService"
        });
    }
});

app.Run();

// ----------------------
// Database Initialization Method
// ----------------------
// In your Program.cs - Database Initialization Method
static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var dbContext = services.GetRequiredService<UserDbContext>();

    const int maxRetries = 3;
    var retryCount = 0;

    while (retryCount < maxRetries)
    {
        try
        {
            logger.LogInformation("Attempting database initialization (Attempt {RetryCount})...", retryCount + 1);

            // Strategy 1: Try to apply migrations
            try
            {
                logger.LogInformation("Checking for migrations...");
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully.");
            }
            catch (Exception migrateEx)
            {
                logger.LogWarning(migrateEx, "Migrations failed, attempting EnsureCreated...");
                
                // Strategy 2: Create database and tables from scratch
                await dbContext.Database.EnsureCreatedAsync();
                logger.LogInformation("Database created successfully using EnsureCreated.");
            }

            // Verify the database is working by trying to seed roles
            logger.LogInformation("Verifying database by seeding roles...");
            await DataSeeder.SeedRoles(services);
            
            logger.LogInformation("Database initialization completed successfully.");
            return; // Success - exit the retry loop
        }
        catch (Exception ex)
        {
            retryCount++;
            logger.LogWarning(ex, "Database initialization attempt {RetryCount} failed.", retryCount);
            
            if (retryCount >= maxRetries)
            {
                logger.LogError(ex, "All database initialization attempts failed. Application will start in degraded mode.");
                break;
            }
            
            // Wait before retrying
            await Task.Delay(TimeSpan.FromSeconds(5 * retryCount));
        }
    }
}
using AuthService.Data;
using AuthService.Messaging;
using AuthService.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Extensions;
using Shared.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins("http://localhost:5004")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddSharedInfrastructure(builder.Configuration);

builder.Services.AddSingleton<IEventPublisher, EventPublisher>();
builder.Services.AddSingleton<IAuthService, AuthServiceImp>();

builder.Services.AddSingleton<EventConsumer>();

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AuthServiceConnection")));

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

var consumer = app.Services.GetRequiredService<EventConsumer>();
consumer.StartConsuming();

app.Run();


using AuthService.Messaging;
using AuthService.Services;
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

builder.Services.AddSingleton<JwtHelper>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var publicKey = config["JwtSettings:PublicKey"]
        ?? throw new InvalidOperationException("Public key not found in JWT");
    var privateKey = config["JwtSettings:PrivateKey"]
        ?? throw new InvalidOperationException("Private key not found in JWT");
    
    return new JwtHelper(privateKey,  publicKey);
});
builder.Services.AddSingleton<RedisConnectionHelper>();
builder.Services.AddSingleton<RabbitMqConnectionHelper>();

builder.Services.AddSingleton<IEventPublisher, EventPublisher>();
builder.Services.AddSingleton<IAuthService, AuthServiceImp>();

builder.Services.AddSingleton<EventConsumer>();

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


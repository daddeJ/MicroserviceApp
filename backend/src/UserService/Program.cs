using Shared.Helpers;
using UserService.Messaging;
using UserService.Services;

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

builder.Services.AddSingleton<RedisConnectionHelper>();
builder.Services.AddSingleton<RabbitMqConnectionHelper>();

builder.Services.AddSingleton<IEventPublisher, EventPublisher>();
builder.Services.AddSingleton<IUserService, UserServiceImp>();

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


app.Run();

using Shared.Helpers;
using UserService.Messaging;
using UserService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddSingleton<RedisConnectionHelper>();
builder.Services.AddSingleton<RabbitMqConnectionHelper>();

builder.Services.AddSingleton<IEventPublisher, EventPublisher>();
builder.Services.AddSingleton<IUserService, UserServiceImp>();

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
app.UseAuthorization();
app.MapControllers();

var consumer = app.Services.GetRequiredService<EventConsumer>();
consumer.StartConsuming();

app.Run();

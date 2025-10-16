using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Caching;
using Shared.Factories;
using Shared.Helpers;
using Shared.Interfaces;
using Shared.Messaging;
using Shared.Security;

namespace Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddJwtAuthentication(configuration);
        services.AddSharedFactories(configuration);
        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, 
        IConfiguration configuration)
    {
        var audience = configuration["JwtSettings:Audience"]
                       ?? throw new InvalidOperationException("Audience not found in JWT");
        var issuer = configuration["JwtSettings:Issuer"]
                     ?? throw new InvalidOperationException("Issuer not found in JWT");
        var privateKeyPath = configuration["JwtSettings:PrivateKeyPath"]
                             ?? throw new InvalidOperationException("Private key path not found in JWT");
        var publicKeyPath = configuration["JwtSettings:PublicKeyPath"]
                            ?? throw new InvalidOperationException("Public key path not found in JWT");
        var expirationMinutes = int.Parse(configuration["JwtSettings:ExpirationMinutes"]
                                          ?? throw new InvalidOperationException("Expiration minutes not found in JWT"));

        // Read key contents from file
        if (!File.Exists(privateKeyPath))
            throw new FileNotFoundException($"Private key file not found at {privateKeyPath}");
        if (!File.Exists(publicKeyPath))
            throw new FileNotFoundException($"Public key file not found at {publicKeyPath}");

        var privateKey = File.ReadAllText(privateKeyPath);
        var publicKey = File.ReadAllText(publicKeyPath);

        var jwtOptions = new JwtOptions
        {
            Audience = audience,
            Issuer = issuer,
            PrivateKey = privateKey,
            PublicKey = publicKey,
            ExpirationMinutes = expirationMinutes
        };

        services.AddSingleton(jwtOptions);

        services.AddSingleton<JwtTokenGenerator>(sp =>
        {
            var options = sp.GetRequiredService<JwtOptions>();
            return new JwtTokenGenerator(options);
        });

        services.AddSingleton<TokenValidator>(sp =>
        {
            var options = sp.GetRequiredService<JwtOptions>();
            return new TokenValidator(options);
        });

        return services;
    }

    public static IServiceCollection AddRabbitMq(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<RabbitMqConnectionHelper>();
        services.AddSingleton<IMessagePublisher, EventBus>();
        return services;
    }

    public static IServiceCollection AddRedisCache(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<RedisConnectionHelper>();
        services.AddSingleton<RedisCacheHelper>();
        return services;
    }

    public static IServiceCollection AddSharedFactories(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRabbitMq(configuration);
        services.AddRedisCache(configuration);
        services.AddSingleton<IUserActionFactory, UserActionFactory>();
        return services;
    }
}

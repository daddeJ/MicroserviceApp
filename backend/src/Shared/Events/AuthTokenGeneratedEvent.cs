using System;

namespace Shared.Events;

public sealed class AuthTokenGeneratedEvent
{
    public Guid UserId { get; set; }
    public string Token { get; set; }
    public DateTime GeneratedAt { get; set; }

    public AuthTokenGeneratedEvent()
    {
    }

    public AuthTokenGeneratedEvent(Guid userId, string token, DateTime generatedAt)
    {
	    UserId = userId;
	    Token = token ?? throw new ArgumentNullException(nameof(token));
	    GeneratedAt = generatedAt;
    }
    
}

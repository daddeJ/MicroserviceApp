# Design Patterns for Microservices Architecture

## Overview
This document outlines essential design patterns applicable to microservices architecture, specifically for UserService, AuthService, LoggerService, and AdminService.

---

## 1. Architectural Patterns

### 1.1 Repository Pattern

**Purpose:** Abstract data access logic and provide a collection-like interface for accessing domain objects.

**Implementation Flow:**
```
-> Controller receives request
-> Calls Service Layer
-> Service Layer calls Repository
    -> Repository uses DbContext
    -> Executes query
    -> Returns entity or collection
-> Service Layer processes data
-> Returns DTO to Controller
```

**Code Example:**
```csharp
// Interface
public interface IUserRepository
{
    Task<User> GetByIdAsync(string id);
    Task<User> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllAsync();
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(string id);
}

// Implementation
public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;
    
    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<User> GetByIdAsync(string id)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
    }
    
    public async Task<User> GetByEmailAsync(string email)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);
    }
    
    public async Task<User> AddAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }
    
    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }
    
    public async Task DeleteAsync(string id)
    {
        var user = await GetByIdAsync(id);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }
}
```

**Benefits:**
- Centralized data access logic
- Easy to test with mock repositories
- Reduced code duplication
- Separation of concerns

---

### 1.2 Unit of Work Pattern

**Purpose:** Maintain a list of objects affected by a business transaction and coordinates writing changes.

**Implementation Flow:**
```
-> Start Unit of Work
-> Begin Transaction
-> Repository Operation 1
-> Repository Operation 2
-> Repository Operation 3
-> If all succeed:
    -> Commit Unit of Work
    -> Commit Transaction
-> If any fails:
    -> Rollback Transaction
    -> Dispose Unit of Work
```

**Code Example:**
```csharp
// Interface
public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    ISessionRepository Sessions { get; }
    ILogRepository Logs { get; }
    Task<int> CompleteAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}

// Implementation
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction _transaction;
    
    public IUserRepository Users { get; private set; }
    public ISessionRepository Sessions { get; private set; }
    public ILogRepository Logs { get; private set; }
    
    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        Users = new UserRepository(_context);
        Sessions = new SessionRepository(_context);
        Logs = new LogRepository(_context);
    }
    
    public async Task<int> CompleteAsync()
    {
        return await _context.SaveChangesAsync();
    }
    
    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }
    
    public async Task CommitTransactionAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
            await _transaction.CommitAsync();
        }
        catch
        {
            await RollbackTransactionAsync();
            throw;
        }
        finally
        {
            _transaction?.Dispose();
        }
    }
    
    public async Task RollbackTransactionAsync()
    {
        await _transaction?.RollbackAsync();
        _transaction?.Dispose();
    }
    
    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}

// Usage
public class UserService
{
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<bool> RegisterUserAsync(RegistrationDto dto)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var user = new User { ... };
            await _unitOfWork.Users.AddAsync(user);
            
            var session = new Session { ... };
            await _unitOfWork.Sessions.AddAsync(session);
            
            await _unitOfWork.CommitTransactionAsync();
            return true;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            return false;
        }
    }
}
```

---

### 1.3 CQRS (Command Query Responsibility Segregation)

**Purpose:** Separate read and write operations for better performance and scalability.

**Implementation Flow:**
```
Commands (Write Operations):
-> Controller receives command
-> Command Handler processes
-> Update database
-> Publish event
-> Return result

Queries (Read Operations):
-> Controller receives query
-> Query Handler processes
-> Read from cache (if available)
-> If cache miss, read from database
-> Return DTO
```

**Code Example:**
```csharp
// Commands
public class CreateUserCommand
{
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public string Role { get; set; }
    public int Tier { get; set; }
}

public interface ICommandHandler<TCommand>
{
    Task<ApiResponse<object>> HandleAsync(TCommand command);
}

public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand>
{
    private readonly IUserRepository _repository;
    private readonly IEventPublisher _eventPublisher;
    
    public async Task<ApiResponse<object>> HandleAsync(CreateUserCommand command)
    {
        var user = new User
        {
            UserName = command.UserName,
            Email = command.Email,
            // ... map properties
        };
        
        await _repository.AddAsync(user);
        
        // Publish event
        await _eventPublisher.PublishAsync(new UserCreatedEvent 
        { 
            UserId = user.Id 
        });
        
        return ApiResponse<object>.Ok(user.Id, "User created successfully");
    }
}

// Queries
public class GetUserByIdQuery
{
    public string UserId { get; set; }
}

public interface IQueryHandler<TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query);
}

public class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, UserDto>
{
    private readonly IUserRepository _repository;
    private readonly ICacheService _cache;
    
    public async Task<UserDto> HandleAsync(GetUserByIdQuery query)
    {
        // Try cache first
        var cacheKey = $"user:{query.UserId}";
        var cached = await _cache.GetAsync<UserDto>(cacheKey);
        if (cached != null)
            return cached;
        
        // Fallback to database
        var user = await _repository.GetByIdAsync(query.UserId);
        var dto = MapToDto(user);
        
        // Cache result
        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(30));
        
        return dto;
    }
}
```

---

## 2. Communication Patterns

### 2.1 Event-Driven Pattern

**Purpose:** Decouple services through asynchronous event publishing and consumption.

**Implementation Flow:**
```
Publisher Service:
-> Business operation completes
-> Create event object
-> Publish to message broker (RabbitMQ)
-> Continue processing

Message Broker (RabbitMQ):
-> Receive event
-> Route to appropriate queue
-> Store until consumed

Consumer Service:
-> Subscribe to queue
-> Receive event
-> Process event
-> Acknowledge message
```

**Code Example:**
```csharp
// Event Definition
public class UserRegisteredEvent
{
    public string UserId { get; set; }
    public string Email { get; set; }
    public DateTime RegisteredAt { get; set; }
}

// Publisher
public interface IEventPublisher
{
    Task PublishAsync<T>(T @event) where T : class;
}

public class RabbitMQEventPublisher : IEventPublisher
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    
    public async Task PublishAsync<T>(T @event) where T : class
    {
        var eventName = typeof(T).Name;
        var message = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(message);
        
        _channel.BasicPublish(
            exchange: "user.events",
            routingKey: eventName,
            basicProperties: null,
            body: body
        );
        
        await Task.CompletedTask;
    }
}

// Consumer
public class UserRegisteredEventConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IServiceProvider _serviceProvider;
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
        
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var @event = JsonSerializer.Deserialize<UserRegisteredEvent>(message);
            
            using var scope = _serviceProvider.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            
            await authService.HandleUserRegisteredAsync(@event.UserId);
            
            _channel.BasicAck(ea.DeliveryTag, false);
        };
        
        _channel.BasicConsume(
            queue: "user.registered",
            autoAck: false,
            consumer: consumer
        );
        
        return Task.CompletedTask;
    }
}
```

---

### 2.2 Saga Pattern

**Purpose:** Manage distributed transactions across multiple services.

**Implementation Flow:**
```
Orchestrator Pattern:
-> Start Saga
-> Execute Step 1 (Create User)
    -> If success: continue
    -> If fail: execute compensation
-> Execute Step 2 (Create Session)
    -> If success: continue
    -> If fail: compensate Step 1
-> Execute Step 3 (Send Email)
    -> If success: complete saga
    -> If fail: compensate Step 2 and 1
-> Mark Saga Complete/Failed
```

**Code Example:**
```csharp
// Saga State
public class UserRegistrationSaga
{
    public string SagaId { get; set; }
    public string UserId { get; set; }
    public SagaStatus Status { get; set; }
    public List<SagaStep> CompletedSteps { get; set; }
}

public enum SagaStatus
{
    Started,
    InProgress,
    Completed,
    Failed,
    Compensating,
    Compensated
}

// Saga Orchestrator
public class UserRegistrationSagaOrchestrator
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly IEmailService _emailService;
    private readonly ISagaRepository _sagaRepository;
    
    public async Task<(bool success, string error)> ExecuteAsync(RegistrationDto dto)
    {
        var saga = new UserRegistrationSaga
        {
            SagaId = Guid.NewGuid().ToString(),
            Status = SagaStatus.Started,
            CompletedSteps = new List<SagaStep>()
        };
        
        try
        {
            // Step 1: Create User
            var userId = await _userService.CreateUserAsync(dto);
            saga.UserId = userId;
            saga.CompletedSteps.Add(SagaStep.UserCreated);
            saga.Status = SagaStatus.InProgress;
            await _sagaRepository.UpdateAsync(saga);
            
            // Step 2: Create Session
            var session = await _authService.CreateSessionAsync(userId);
            saga.CompletedSteps.Add(SagaStep.SessionCreated);
            await _sagaRepository.UpdateAsync(saga);
            
            // Step 3: Send Welcome Email
            await _emailService.SendWelcomeEmailAsync(dto.Email);
            saga.CompletedSteps.Add(SagaStep.EmailSent);
            
            // Complete Saga
            saga.Status = SagaStatus.Completed;
            await _sagaRepository.UpdateAsync(saga);
            
            return (true, null);
        }
        catch (Exception ex)
        {
            // Compensate completed steps
            saga.Status = SagaStatus.Compensating;
            await _sagaRepository.UpdateAsync(saga);
            
            await CompensateAsync(saga);
            
            saga.Status = SagaStatus.Compensated;
            await _sagaRepository.UpdateAsync(saga);
            
            return (false, ex.Message);
        }
    }
    
    private async Task CompensateAsync(UserRegistrationSaga saga)
    {
        // Compensate in reverse order
        if (saga.CompletedSteps.Contains(SagaStep.SessionCreated))
        {
            await _authService.RevokeSessionAsync(saga.UserId);
        }
        
        if (saga.CompletedSteps.Contains(SagaStep.UserCreated))
        {
            await _userService.DeleteUserAsync(saga.UserId);
        }
    }
}
```

---

## 3. Resilience Patterns

### 3.1 Retry Pattern

**Purpose:** Automatically retry failed operations with exponential backoff.

**Implementation Flow:**
```
-> Execute operation
-> If fails:
    -> Wait (backoff delay)
    -> Retry attempt 1
    -> If fails:
        -> Wait (increased delay)
        -> Retry attempt 2
        -> If fails:
            -> Wait (increased delay)
            -> Retry attempt 3
            -> If fails:
                -> Return failure
```

**Code Example:**
```csharp
public class RetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    
    public RetryPolicy(int maxRetries = 3, int initialDelayMs = 100)
    {
        _maxRetries = maxRetries;
        _initialDelay = TimeSpan.FromMilliseconds(initialDelayMs);
    }
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        var attempt = 0;
        var delay = _initialDelay;
        
        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                attempt++;
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
        }
    }
}

// Usage
public class UserService
{
    private readonly IUserRepository _repository;
    private readonly RetryPolicy _retryPolicy;
    
    public async Task<User> GetUserAsync(string id)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            return await _repository.GetByIdAsync(id);
        });
    }
}
```

---

### 3.2 Circuit Breaker Pattern

**Purpose:** Prevent cascading failures by stopping requests to failing services.

**Implementation Flow:**
```
Closed State (Normal):
-> Allow all requests
-> Monitor failures
-> If failure threshold exceeded:
    -> Open circuit

Open State (Failing):
-> Reject all requests immediately
-> Return cached data or error
-> After timeout period:
    -> Move to Half-Open

Half-Open State (Testing):
-> Allow limited requests
-> If requests succeed:
    -> Close circuit
-> If requests fail:
    -> Open circuit again
```

**Code Example:**
```csharp
public class CircuitBreaker
{
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount = 0;
    private DateTime _lastFailureTime;
    private readonly int _failureThreshold;
    private readonly TimeSpan _timeout;
    
    public CircuitBreaker(int failureThreshold = 5, int timeoutSeconds = 60)
    {
        _failureThreshold = failureThreshold;
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (_state == CircuitState.Open)
        {
            if (DateTime.UtcNow - _lastFailureTime > _timeout)
            {
                _state = CircuitState.HalfOpen;
            }
            else
            {
                throw new CircuitBreakerOpenException("Circuit breaker is open");
            }
        }
        
        try
        {
            var result = await operation();
            
            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                _failureCount = 0;
            }
            
            return result;
        }
        catch (Exception)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
            
            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
            }
            
            throw;
        }
    }
}

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}
```

---

### 3.3 Bulkhead Pattern

**Purpose:** Isolate resources to prevent total system failure.

**Implementation Flow:**
```
-> Define resource pools
-> Pool 1: Critical operations (e.g., login)
    -> Max 100 concurrent requests
-> Pool 2: Normal operations (e.g., get user)
    -> Max 200 concurrent requests
-> Pool 3: Background tasks
    -> Max 50 concurrent requests
-> If pool exhausted:
    -> Queue or reject request
```

**Code Example:**
```csharp
public class BulkheadPolicy
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxParallelization;
    
    public BulkheadPolicy(int maxParallelization)
    {
        _maxParallelization = maxParallelization;
        _semaphore = new SemaphoreSlim(maxParallelization);
    }
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await operation();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

// Configuration
public static class BulkheadPolicies
{
    public static BulkheadPolicy CriticalOperations = new BulkheadPolicy(100);
    public static BulkheadPolicy NormalOperations = new BulkheadPolicy(200);
    public static BulkheadPolicy BackgroundTasks = new BulkheadPolicy(50);
}

// Usage
public class UserService
{
    public async Task<User> LoginAsync(LoginDto dto)
    {
        return await BulkheadPolicies.CriticalOperations.ExecuteAsync(async () =>
        {
            return await PerformLoginAsync(dto);
        });
    }
}
```

---

## 4. Caching Patterns

### 4.1 Cache-Aside Pattern

**Purpose:** Load data into cache on demand.

**Implementation Flow:**
```
-> Check cache for data
-> If found (cache hit):
    -> Return cached data
-> If not found (cache miss):
    -> Load from database
    -> Store in cache
    -> Return data
```

**Code Example:**
```csharp
public class CacheAsideService<T>
{
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _expiration;
    
    public async Task<T> GetOrCreateAsync(
        string key, 
        Func<Task<T>> factory)
    {
        // Try get from cache
        var cachedData = await _cache.GetStringAsync(key);
        if (cachedData != null)
        {
            return JsonSerializer.Deserialize<T>(cachedData);
        }
        
        // Load from source
        var data = await factory();
        
        // Store in cache
        var serialized = JsonSerializer.Serialize(data);
        await _cache.SetStringAsync(
            key, 
            serialized, 
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _expiration
            }
        );
        
        return data;
    }
}

// Usage
public class UserService
{
    private readonly CacheAsideService<UserDto> _cache;
    private readonly IUserRepository _repository;
    
    public async Task<UserDto> GetUserAsync(string userId)
    {
        return await _cache.GetOrCreateAsync(
            $"user:{userId}",
            async () =>
            {
                var user = await _repository.GetByIdAsync(userId);
                return MapToDto(user);
            }
        );
    }
}
```

---

### 4.2 Write-Through Cache Pattern

**Purpose:** Update cache and database simultaneously.

**Implementation Flow:**
```
-> Receive update request
-> Update database
-> If database update succeeds:
    -> Update cache
    -> Return success
-> If database update fails:
    -> Don't update cache
    -> Return error
```

**Code Example:**
```csharp
public class WriteThroughCacheService
{
    private readonly IDistributedCache _cache;
    private readonly IUserRepository _repository;
    
    public async Task<bool> UpdateUserAsync(string userId, UpdateUserDto dto)
    {
        // Update database first
        var user = await _repository.GetByIdAsync(userId);
        user.Email = dto.Email;
        user.UserName = dto.UserName;
        
        await _repository.UpdateAsync(user);
        
        // Update cache
        var cacheKey = $"user:{userId}";
        var serialized = JsonSerializer.Serialize(user);
        await _cache.SetStringAsync(cacheKey, serialized);
        
        return true;
    }
}
```

---

### 4.3 Cache Invalidation Pattern

**Purpose:** Remove stale data from cache when source data changes.

**Implementation Flow:**
```
-> Data updated in database
-> Identify affected cache keys
-> Remove keys from cache
-> Publish cache invalidation event
-> Other services receive event
-> Clear their local caches
```

**Code Example:**
```csharp
public class CacheInvalidationService
{
    private readonly IDistributedCache _cache;
    private readonly IEventPublisher _eventPublisher;
    
    public async Task InvalidateUserCacheAsync(string userId)
    {
        // Remove direct cache
        await _cache.RemoveAsync($"user:{userId}");
        
        // Remove related caches
        await _cache.RemoveAsync($"user:sessions:{userId}");
        await _cache.RemoveAsync($"user:roles:{userId}");
        
        // Publish event for other services
        await _eventPublisher.PublishAsync(new CacheInvalidatedEvent
        {
            EntityType = "User",
            EntityId = userId,
            Timestamp = DateTime.UtcNow
        });
    }
}
```

---

## 5. Security Patterns

### 5.1 API Gateway Pattern

**Purpose:** Single entry point for all client requests with authentication, rate limiting, and routing.

**Implementation Flow:**
```
Client Request:
-> API Gateway receives request
-> Validate authentication token
-> Check rate limiting
-> Apply CORS policy
-> Route to appropriate service
    -> UserService
    -> AuthService
    -> LoggerService
    -> AdminService
-> Aggregate responses (if needed)
-> Return to client
```

---

### 5.2 Token Validation Pattern

**Purpose:** Validate JWT tokens consistently across services.

**Implementation Flow:**
```
-> Extract token from Authorization header
-> Validate token signature
-> Check token expiration
-> Validate issuer and audience
-> Extract claims
-> Check user permissions
-> Allow or deny request
```

**Code Example:**
```csharp
public class JwtTokenValidator
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    
    public async Task<ClaimsPrincipal> ValidateTokenAsync(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_secretKey);
        
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            }, out SecurityToken validatedToken);
            
            return principal;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
```

---

## 6. Data Patterns

### 6.1 Data Transfer Object (DTO) Pattern

**Purpose:** Transfer data between layers without exposing domain models.

**Code Example:**
```csharp
// Domain Model (Internal)
public class User
{
    public string Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; } // Sensitive
    public string SecurityStamp { get; set; } // Sensitive
    public bool LockoutEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
}

// DTO (External)
public class UserDto
{
    public string Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool IsLocked { get; set; }
    // No sensitive fields exposed
}

// Mapping
public class UserMapper
{
    public static UserDto ToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            IsLocked = user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow
        };
    }
}
```

---

### 6.2 Specification Pattern

**Purpose:** Encapsulate business rules and query logic.

**Code Example:**
```csharp
public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
}

public class ActiveUsersSpecification : ISpecification<User>
{
    public Expression<Func<User, bool>> Criteria =>
        u => !u.LockoutEnabled || u.LockoutEnd <= DateTimeOffset.UtcNow;
    
    public List<Expression<Func<User, object>>> Includes { get; } = new();
}

public class UsersByRoleSpecification : ISpecification<User>
{
    private readonly string _role;
    
    public UsersByRoleSpecification(string role)
    {
        _role = role;
    }
    
    public Expression<Func<User, bool>> Criteria =>
        u => u.UserRoles.Any(ur => ur.Role.Name == _role);
    
    public List<Expression<Func<User, object>>> Includes { get; } = new()
    {
        u => u.UserRoles
    };
}

// Repository with Specification
public async Task<IEnumerable<User>> GetAsync(ISpecification<User> spec)
{
    var query = _context.Users.AsQueryable();
    
    if (spec.Criteria != null)
        query = query.Where(spec.Criteria);
    
    query = spec.Includes.Aggregate(query, (current, include) => 
        current.Include(include));
    
    return await query.ToListAsync();
}

// Usage
var activeUsers = await _repository.GetAsync(new ActiveUsersSpecification());
var admins = await _repository.GetAsync(new UsersByRoleSpecification("Admin"));
```

---

## 7. Best Practices Summary

### 7.1 When to Use Each Pattern

**Repository Pattern:**
 - ✅ Every data access layer
 - ✅ Provides abstraction over data source
 - ✅ Enables easy unit testing

**Unit of Work:**
 - ✅ Multi-step transactions
 - ✅ Cross-repository operations
 - ✅ Ensuring data consistency

**CQRS:**
 - ✅ High-read, low-write scenarios
 - ✅ Different read/write models needed
 - ✅ Performance optimization required

**Event-Driven:**
 - ✅ Loose coupling between services
 - ✅ Asynchronous processing
 - ✅ Audit trails and logging

**Saga:**
 - ✅ Distributed transactions
 - ✅ Long-running processes
 - ✅ Compensation logic needed

**Retry:**
 - ✅ Transient failures expected
 - ✅ Network operations
 - ✅ External API calls

**Circuit Breaker:**
 - ✅ Prevent cascading failures
 - ✅ Protect downstream services
 - ✅ Graceful degradation

**Bulkhead:**
 - ✅ Resource isolation
 - ✅ Prevent resource exhaustion
 - ✅ Critical vs non-critical operations

**Cache-Aside:**
 - ✅ Frequently accessed data
 - ✅ Expensive queries
 - ✅ Read-heavy operations

**API Gateway:**
 - ✅ Centralized authentication
 - ✅ Rate limiting
 - ✅ Request routing

---

### 7.2 Pattern Combinations

**User Registration Flow:**
- Repository Pattern (data access)
- Unit of Work (transaction management)
- Event-Driven (notify other services)
- Saga (distributed transaction)
- DTO Pattern (data transfer)

**Session Management:**
- Repository Pattern
- Cache-Aside Pattern
- Circuit Breaker (Redis failures)
- Event-Driven (session events)

**Log Retrieval:**
- CQRS (separate read model)
- Cache-Aside Pattern
- Specification Pattern (complex queries)
- Bulkhead (isolate log queries)

**Admin Operations:**
- API Gateway (authentication)
- Repository Pattern
- Unit of Work (bulk operations)
- Event-Driven (audit logging)
- Retry Pattern (transient failures)
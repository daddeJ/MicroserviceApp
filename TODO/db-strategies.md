# Data Optimization Strategies for Microservices

## Overview
This document outlines database optimization strategies including transactions, stored procedures, triggers, indexing, and best practices for microservices architecture.

---

## 1. Transaction Management

### 1.1 Transaction Patterns

#### Pattern 1: User Registration Transaction
```
-> BEGIN TRANSACTION
    -> Insert User into AspNetUsers table
        -> If fails: ROLLBACK and return error
    -> Insert UserRole into AspNetUserRoles table
        -> If fails: ROLLBACK and return error
    -> Insert UserClaim (Tier) into AspNetUserClaims table
        -> If fails: ROLLBACK and return error
    -> Create UserProfile record
        -> If fails: ROLLBACK and return error
    -> Log UserCreated event
        -> If fails: ROLLBACK and return error
-> COMMIT TRANSACTION
-> Return Success
```

**Implementation:**
```csharp
public async Task<(bool success, string error)> RegisterUserTransactionAsync(
    ApplicationUser user, 
    string role, 
    int tier)
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        // Create User
        var createResult = await _userManager.CreateAsync(user, user.PasswordHash);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync();
            return (false, "User creation failed");
        }
        
        // Add Role
        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync();
            return (false, "Role assignment failed");
        }
        
        // Add Tier Claim
        var claimResult = await _userManager.AddClaimAsync(
            user, 
            new Claim("Tier", tier.ToString())
        );
        if (!claimResult.Succeeded)
        {
            await transaction.RollbackAsync();
            return (false, "Tier assignment failed");
        }
        
        // Create Profile
        var profile = new UserProfile 
        { 
            UserId = user.Id, 
            CreatedAt = DateTime.UtcNow 
        };
        _context.UserProfiles.Add(profile);
        await _context.SaveChangesAsync();
        
        await transaction.CommitAsync();
        return (true, null);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return (false, ex.Message);
    }
}
```

#### Pattern 2: Session Management Transaction
```
-> BEGIN TRANSACTION
    -> Insert new Session record
        -> SessionId
        -> UserId
        -> AccessToken
        -> RefreshToken
        -> IssuedAt
        -> ExpiresAt
        -> DeviceInfo
        -> IpAddress
        -> Status = Active
    -> Update User LastLoginAt timestamp
    -> Invalidate old sessions (optional)
        -> UPDATE Sessions SET Status = 'Expired' 
           WHERE UserId = @UserId AND Status = 'Active'
    -> Log LoginEvent
-> COMMIT TRANSACTION
```

#### Pattern 3: Bulk Session Revocation Transaction
```
-> BEGIN TRANSACTION
    -> UPDATE Sessions 
       SET Status = 'Revoked', RevokedAt = GETUTCDATE()
       WHERE UserId = @UserId AND Status = 'Active'
    -> Get affected session count
    -> Log SessionsRevokedEvent with count
    -> Clear user tokens from cache
-> COMMIT TRANSACTION
```

#### Pattern 4: User Update Transaction
```
-> BEGIN TRANSACTION
    -> Get existing user record (with row lock)
        -> SELECT * FROM AspNetUsers WITH (UPDLOCK, ROWLOCK)
           WHERE Id = @UserId
    -> Validate update permissions
    -> Update user fields
    -> If email changed:
        -> Set EmailConfirmed = false
        -> Generate email confirmation token
    -> If role changed:
        -> Remove old roles
        -> Add new roles
    -> Update LastModifiedAt timestamp
    -> Log UserUpdatedEvent
-> COMMIT TRANSACTION
```

### 1.2 Transaction Isolation Levels

#### Read Committed (Default)
```sql
-- Prevents dirty reads
-- Good for most operations
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
```

#### Repeatable Read
```sql
-- Prevents dirty reads and non-repeatable reads
-- Use for critical reads that need consistency
SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
BEGIN TRANSACTION;
    SELECT * FROM Sessions WHERE UserId = @UserId;
    -- Additional operations
COMMIT;
```

#### Serializable
```sql
-- Highest isolation level
-- Use for financial transactions or critical data
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;
    -- Critical operations
COMMIT;
```

#### Snapshot
```sql
-- Prevents blocking, uses row versioning
-- Good for read-heavy operations
SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
BEGIN TRANSACTION;
    SELECT * FROM Logs WHERE UserId = @UserId;
COMMIT;
```

---

## 2. Stored Procedures

### 2.1 User Management Stored Procedures

#### SP: Create User with Role and Tier
```sql
CREATE PROCEDURE sp_CreateUserWithRoleAndTier
    @UserId NVARCHAR(450),
    @UserName NVARCHAR(256),
    @Email NVARCHAR(256),
    @PasswordHash NVARCHAR(MAX),
    @Role NVARCHAR(256),
    @Tier INT,
    @CreatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        -- Insert User
        INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, 
                                PasswordHash, EmailConfirmed, SecurityStamp, 
                                ConcurrencyStamp, LockoutEnabled, AccessFailedCount)
        VALUES (@UserId, @UserName, UPPER(@UserName), @Email, UPPER(@Email), 
                @PasswordHash, 0, NEWID(), NEWID(), 1, 0);
        
        -- Get RoleId
        DECLARE @RoleId NVARCHAR(450);
        SELECT @RoleId = Id FROM AspNetRoles WHERE Name = @Role;
        
        IF @RoleId IS NULL
        BEGIN
            THROW 50001, 'Role does not exist', 1;
        END
        
        -- Insert UserRole
        INSERT INTO AspNetUserRoles (UserId, RoleId)
        VALUES (@UserId, @RoleId);
        
        -- Insert Tier Claim
        INSERT INTO AspNetUserClaims (UserId, ClaimType, ClaimValue)
        VALUES (@UserId, 'Tier', CAST(@Tier AS NVARCHAR(10)));
        
        -- Create UserProfile
        INSERT INTO UserProfiles (UserId, CreatedAt, UpdatedAt)
        VALUES (@UserId, @CreatedAt, @CreatedAt);
        
        COMMIT TRANSACTION;
        
        SELECT 1 AS Success, 'User created successfully' AS Message;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        SELECT 0 AS Success, ERROR_MESSAGE() AS Message;
    END CATCH
END
GO
```

#### SP: Get User with Roles and Claims
```sql
CREATE PROCEDURE sp_GetUserWithRolesAndClaims
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Get User Info
    SELECT 
        u.Id,
        u.UserName,
        u.Email,
        u.EmailConfirmed,
        u.LockoutEnabled,
        u.LockoutEnd,
        u.AccessFailedCount,
        up.CreatedAt,
        up.UpdatedAt,
        up.LastLoginAt
    FROM AspNetUsers u
    LEFT JOIN UserProfiles up ON u.Id = up.UserId
    WHERE u.Id = @UserId;
    
    -- Get Roles
    SELECT r.Name
    FROM AspNetUserRoles ur
    INNER JOIN AspNetRoles r ON ur.RoleId = r.Id
    WHERE ur.UserId = @UserId;
    
    -- Get Claims
    SELECT ClaimType, ClaimValue
    FROM AspNetUserClaims
    WHERE UserId = @UserId;
END
GO
```

#### SP: Update User LastLogin
```sql
CREATE PROCEDURE sp_UpdateUserLastLogin
    @UserId NVARCHAR(450),
    @LastLoginAt DATETIME2,
    @IpAddress NVARCHAR(50),
    @DeviceInfo NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE UserProfiles
    SET 
        LastLoginAt = @LastLoginAt,
        LastLoginIp = @IpAddress,
        LastLoginDevice = @DeviceInfo,
        UpdatedAt = @LastLoginAt
    WHERE UserId = @UserId;
    
    IF @@ROWCOUNT = 0
    BEGIN
        -- Create profile if doesn't exist
        INSERT INTO UserProfiles (UserId, CreatedAt, UpdatedAt, LastLoginAt, 
                                 LastLoginIp, LastLoginDevice)
        VALUES (@UserId, @LastLoginAt, @LastLoginAt, @LastLoginAt, 
                @IpAddress, @DeviceInfo);
    END
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO
```

### 2.2 Session Management Stored Procedures

#### SP: Create Session
```sql
CREATE PROCEDURE sp_CreateSession
    @SessionId NVARCHAR(450),
    @UserId NVARCHAR(450),
    @AccessToken NVARCHAR(MAX),
    @RefreshToken NVARCHAR(MAX),
    @IssuedAt DATETIME2,
    @ExpiresAt DATETIME2,
    @DeviceInfo NVARCHAR(500),
    @IpAddress NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        -- Insert Session
        INSERT INTO Sessions (SessionId, UserId, AccessToken, RefreshToken, 
                            IssuedAt, ExpiresAt, DeviceInfo, IpAddress, Status)
        VALUES (@SessionId, @UserId, @AccessToken, @RefreshToken, 
                @IssuedAt, @ExpiresAt, @DeviceInfo, @IpAddress, 'Active');
        
        -- Update LastLogin
        EXEC sp_UpdateUserLastLogin @UserId, @IssuedAt, @IpAddress, @DeviceInfo;
        
        COMMIT TRANSACTION;
        SELECT 1 AS Success;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        SELECT 0 AS Success, ERROR_MESSAGE() AS ErrorMessage;
    END CATCH
END
GO
```

#### SP: Revoke User Sessions
```sql
CREATE PROCEDURE sp_RevokeUserSessions
    @UserId NVARCHAR(450),
    @RevokedBy NVARCHAR(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @RevokedCount INT;
    
    UPDATE Sessions
    SET 
        Status = 'Revoked',
        RevokedAt = GETUTCDATE(),
        RevokedBy = @RevokedBy
    WHERE UserId = @UserId AND Status = 'Active';
    
    SET @RevokedCount = @@ROWCOUNT;
    
    SELECT @RevokedCount AS RevokedSessions;
END
GO
```

#### SP: Cleanup Expired Sessions
```sql
CREATE PROCEDURE sp_CleanupExpiredSessions
    @BatchSize INT = 1000
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @DeletedCount INT = 0;
    
    WHILE 1 = 1
    BEGIN
        UPDATE TOP (@BatchSize) Sessions
        SET Status = 'Expired'
        WHERE Status = 'Active' 
          AND ExpiresAt < GETUTCDATE();
        
        IF @@ROWCOUNT = 0
            BREAK;
            
        SET @DeletedCount = @DeletedCount + @@ROWCOUNT;
    END
    
    SELECT @DeletedCount AS ExpiredSessions;
END
GO
```

### 2.3 Logging Stored Procedures

#### SP: Insert Log Entry
```sql
CREATE PROCEDURE sp_InsertLogEntry
    @LogId NVARCHAR(450),
    @UserId NVARCHAR(450) = NULL,
    @EventType NVARCHAR(100),
    @Service NVARCHAR(100),
    @Message NVARCHAR(MAX),
    @LogLevel NVARCHAR(50),
    @IpAddress NVARCHAR(50) = NULL,
    @DeviceInfo NVARCHAR(500) = NULL,
    @Exception NVARCHAR(MAX) = NULL,
    @Timestamp DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO Logs (LogId, UserId, EventType, Service, Message, 
                     LogLevel, IpAddress, DeviceInfo, Exception, Timestamp)
    VALUES (@LogId, @UserId, @EventType, @Service, @Message, 
            @LogLevel, @IpAddress, @DeviceInfo, @Exception, @Timestamp);
    
    SELECT @@ROWCOUNT AS Inserted;
END
GO
```

#### SP: Get Logs with Pagination
```sql
CREATE PROCEDURE sp_GetLogsWithPagination
    @PageNumber INT = 1,
    @PageSize INT = 50,
    @UserId NVARCHAR(450) = NULL,
    @EventType NVARCHAR(100) = NULL,
    @Service NVARCHAR(100) = NULL,
    @StartDate DATETIME2 = NULL,
    @EndDate DATETIME2 = NULL,
    @TotalCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Get total count
    SELECT @TotalCount = COUNT(*)
    FROM Logs
    WHERE (@UserId IS NULL OR UserId = @UserId)
      AND (@EventType IS NULL OR EventType = @EventType)
      AND (@Service IS NULL OR Service = @Service)
      AND (@StartDate IS NULL OR Timestamp >= @StartDate)
      AND (@EndDate IS NULL OR Timestamp <= @EndDate);
    
    -- Get paginated results
    SELECT 
        LogId,
        UserId,
        EventType,
        Service,
        Message,
        LogLevel,
        IpAddress,
        DeviceInfo,
        Exception,
        Timestamp
    FROM Logs
    WHERE (@UserId IS NULL OR UserId = @UserId)
      AND (@EventType IS NULL OR EventType = @EventType)
      AND (@Service IS NULL OR Service = @Service)
      AND (@StartDate IS NULL OR Timestamp >= @StartDate)
      AND (@EndDate IS NULL OR Timestamp <= @EndDate)
    ORDER BY Timestamp DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO
```

#### SP: Get Log Statistics
```sql
CREATE PROCEDURE sp_GetLogStatistics
    @StartDate DATETIME2,
    @EndDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Events per day
    SELECT 
        CAST(Timestamp AS DATE) AS Date,
        COUNT(*) AS EventCount
    FROM Logs
    WHERE Timestamp BETWEEN @StartDate AND @EndDate
    GROUP BY CAST(Timestamp AS DATE)
    ORDER BY Date DESC;
    
    -- Failed login attempts
    SELECT COUNT(*) AS FailedLoginAttempts
    FROM Logs
    WHERE EventType = 'FailedLogin'
      AND Timestamp BETWEEN @StartDate AND @EndDate;
    
    -- Token generation per service
    SELECT 
        Service,
        COUNT(*) AS TokenCount
    FROM Logs
    WHERE EventType = 'TokenGenerated'
      AND Timestamp BETWEEN @StartDate AND @EndDate
    GROUP BY Service;
    
    -- Errors per service
    SELECT 
        Service,
        COUNT(*) AS ErrorCount
    FROM Logs
    WHERE LogLevel = 'Error'
      AND Timestamp BETWEEN @StartDate AND @EndDate
    GROUP BY Service;
END
GO
```

### 2.4 Analytics Stored Procedures

#### SP: Get User Statistics
```sql
CREATE PROCEDURE sp_GetUserStatistics
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Total users
    SELECT COUNT(*) AS TotalUsers FROM AspNetUsers;
    
    -- Users by role
    SELECT 
        r.Name AS Role,
        COUNT(ur.UserId) AS UserCount
    FROM AspNetRoles r
    LEFT JOIN AspNetUserRoles ur ON r.Id = ur.RoleId
    GROUP BY r.Name;
    
    -- Users by tier
    SELECT 
        ClaimValue AS Tier,
        COUNT(*) AS UserCount
    FROM AspNetUserClaims
    WHERE ClaimType = 'Tier'
    GROUP BY ClaimValue
    ORDER BY ClaimValue;
    
    -- Locked users
    SELECT COUNT(*) AS LockedUsers
    FROM AspNetUsers
    WHERE LockoutEnabled = 1 AND LockoutEnd > GETUTCDATE();
    
    -- New users (last 30 days)
    SELECT COUNT(*) AS NewUsers
    FROM UserProfiles
    WHERE CreatedAt >= DATEADD(DAY, -30, GETUTCDATE());
END
GO
```

#### SP: Get Session Statistics
```sql
CREATE PROCEDURE sp_GetSessionStatistics
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Active sessions
    SELECT COUNT(*) AS ActiveSessions
    FROM Sessions
    WHERE Status = 'Active' AND ExpiresAt > GETUTCDATE();
    
    -- Logins per day (last 30 days)
    SELECT 
        CAST(IssuedAt AS DATE) AS Date,
        COUNT(*) AS LoginCount
    FROM Sessions
    WHERE IssuedAt >= DATEADD(DAY, -30, GETUTCDATE())
    GROUP BY CAST(IssuedAt AS DATE)
    ORDER BY Date DESC;
    
    -- Device distribution
    SELECT 
        CASE 
            WHEN DeviceInfo LIKE '%Chrome%' THEN 'Chrome'
            WHEN DeviceInfo LIKE '%Firefox%' THEN 'Firefox'
            WHEN DeviceInfo LIKE '%Safari%' THEN 'Safari'
            WHEN DeviceInfo LIKE '%Edge%' THEN 'Edge'
            ELSE 'Other'
        END AS Browser,
        COUNT(*) AS Count
    FROM Sessions
    WHERE IssuedAt >= DATEADD(DAY, -30, GETUTCDATE())
    GROUP BY 
        CASE 
            WHEN DeviceInfo LIKE '%Chrome%' THEN 'Chrome'
            WHEN DeviceInfo LIKE '%Firefox%' THEN 'Firefox'
            WHEN DeviceInfo LIKE '%Safari%' THEN 'Safari'
            WHEN DeviceInfo LIKE '%Edge%' THEN 'Edge'
            ELSE 'Other'
        END;
    
    -- Average session duration
    SELECT 
        AVG(DATEDIFF(MINUTE, IssuedAt, ISNULL(RevokedAt, ExpiresAt))) AS AvgDurationMinutes
    FROM Sessions
    WHERE Status IN ('Revoked', 'Expired');
END
GO
```

---

## 3. Database Triggers

### 3.1 Audit Triggers

#### Trigger: User Update Audit
```sql
CREATE TRIGGER trg_User_Update_Audit
ON AspNetUsers
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO UserAuditLog (
        UserId,
        Action,
        OldEmail,
        NewEmail,
        OldUserName,
        NewUserName,
        ChangedBy,
        ChangedAt
    )
    SELECT 
        i.Id,
        'UPDATE',
        d.Email,
        i.Email,
        d.UserName,
        i.UserName,
        SYSTEM_USER,
        GETUTCDATE()
    FROM inserted i
    INNER JOIN deleted d ON i.Id = d.Id
    WHERE i.Email != d.Email OR i.UserName != d.UserName;
END
GO
```

#### Trigger: Session Creation Audit
```sql
CREATE TRIGGER trg_Session_Create_Audit
ON Sessions
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO SessionAuditLog (
        SessionId,
        UserId,
        Action,
        IpAddress,
        DeviceInfo,
        CreatedAt
    )
    SELECT 
        SessionId,
        UserId,
        'CREATE',
        IpAddress,
        DeviceInfo,
        GETUTCDATE()
    FROM inserted;
END
GO
```

### 3.2 Automatic Timestamp Triggers

#### Trigger: Update Timestamp on User Modification
```sql
CREATE TRIGGER trg_User_UpdateTimestamp
ON AspNetUsers
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE up
    SET UpdatedAt = GETUTCDATE()
    FROM UserProfiles up
    INNER JOIN inserted i ON up.UserId = i.Id;
END
GO
```

### 3.3 Data Integrity Triggers

#### Trigger: Prevent SuperAdmin Deletion
```sql
CREATE TRIGGER trg_PreventSuperAdminDeletion
ON AspNetUsers
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Check if any deleted user is SuperAdmin
    IF EXISTS (
        SELECT 1
        FROM deleted d
        INNER JOIN AspNetUserRoles ur ON d.Id = ur.UserId
        INNER JOIN AspNetRoles r ON ur.RoleId = r.Id
        WHERE r.Name = 'SuperAdmin'
    )
    BEGIN
        RAISERROR('Cannot delete SuperAdmin users', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
    
    -- Perform soft delete instead
    UPDATE AspNetUsers
    SET 
        LockoutEnabled = 1,
        LockoutEnd = '9999-12-31 23:59:59.9999999'
    WHERE Id IN (SELECT Id FROM deleted);
END
GO
```

#### Trigger: Cascade Session Revocation
```sql
CREATE TRIGGER trg_CascadeSessionRevocation
ON AspNetUsers
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- If user is locked, revoke all active sessions
    UPDATE s
    SET 
        Status = 'Revoked',
        RevokedAt = GETUTCDATE(),
        RevokedBy = 'SYSTEM'
    FROM Sessions s
    INNER JOIN inserted i ON s.UserId = i.Id
    WHERE i.LockoutEnabled = 1 
      AND i.LockoutEnd > GETUTCDATE()
      AND s.Status = 'Active';
END
GO
```

### 3.4 Validation Triggers

#### Trigger: Validate Tier on Insert
```sql
CREATE TRIGGER trg_ValidateTierOnInsert
ON AspNetUserClaims
INSTEAD OF INSERT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Validate tier values
    IF EXISTS (
        SELECT 1 
        FROM inserted 
        WHERE ClaimType = 'Tier' 
          AND (CAST(ClaimValue AS INT) < 1 OR CAST(ClaimValue AS INT) > 5)
    )
    BEGIN
        RAISERROR('Tier must be between 1 and 5', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
    
    -- Insert valid claims
    INSERT INTO AspNetUserClaims (UserId, ClaimType, ClaimValue)
    SELECT UserId, ClaimType, ClaimValue
    FROM inserted;
END
GO
```

---

## 4. Indexing Strategy

### 4.1 Primary Indexes

#### Users Table
```sql
-- Clustered index on Id (Primary Key)
CREATE CLUSTERED INDEX IX_Users_Id ON AspNetUsers(Id);

-- Non-clustered indexes
CREATE NONCLUSTERED INDEX IX_Users_Email 
ON AspNetUsers(NormalizedEmail) 
INCLUDE (UserName, EmailConfirmed);

CREATE NONCLUSTERED INDEX IX_Users_UserName 
ON AspNetUsers(NormalizedUserName) 
INCLUDE (Email, EmailConfirmed);

CREATE NONCLUSTERED INDEX IX_Users_Lockout 
ON AspNetUsers(LockoutEnd) 
WHERE LockoutEnabled = 1;
```

#### Sessions Table
```sql
-- Clustered index on SessionId (Primary Key)
CREATE CLUSTERED INDEX IX_Sessions_SessionId ON Sessions(SessionId);

-- Non-clustered indexes
CREATE NONCLUSTERED INDEX IX_Sessions_UserId_Status 
ON Sessions(UserId, Status) 
INCLUDE (ExpiresAt, IssuedAt);

CREATE NONCLUSTERED INDEX IX_Sessions_ExpiresAt 
ON Sessions(ExpiresAt) 
WHERE Status = 'Active';

CREATE NONCLUSTERED INDEX IX_Sessions_AccessToken 
ON Sessions(AccessToken(255));

-- Composite index for cleanup operations
CREATE NONCLUSTERED INDEX IX_Sessions_Status_ExpiresAt 
ON Sessions(Status, ExpiresAt);
```

#### Logs Table
```sql
-- Clustered index on Timestamp (for time-series data)
CREATE CLUSTERED INDEX IX_Logs_Timestamp ON Logs(Timestamp DESC);

-- Non-clustered indexes
CREATE NONCLUSTERED INDEX IX_Logs_UserId_Timestamp 
ON Logs(UserId, Timestamp DESC) 
INCLUDE (EventType, Service);

CREATE NONCLUSTERED INDEX IX_Logs_EventType_Timestamp 
ON Logs(EventType, Timestamp DESC);

CREATE NONCLUSTERED INDEX IX_Logs_Service_Timestamp 
ON Logs(Service, Timestamp DESC);

CREATE NONCLUSTERED INDEX IX_Logs_LogLevel 
ON Logs(LogLevel) 
WHERE LogLevel IN ('Error', 'Critical');

-- Full-text index for message search
CREATE FULLTEXT INDEX ON Logs(Message)
KEY INDEX PK_Logs;
```

### 4.2 Filtered Indexes

```sql
-- Active sessions only
CREATE NONCLUSTERED INDEX IX_Sessions_Active 
ON Sessions(UserId, ExpiresAt)
WHERE Status = 'Active';

-- Failed login attempts
CREATE NONCLUSTERED INDEX IX_Logs_FailedLogins 
ON Logs(UserId, Timestamp DESC)
WHERE EventType = 'FailedLogin';

-- Locked users
CREATE NONCLUSTERED INDEX IX_Users_Locked 
ON AspNetUsers(Id, LockoutEnd)
WHERE LockoutEnabled = 1 AND LockoutEnd > GETUTCDATE();
```

### 4.3 Covering Indexes

```sql
-- Cover common user lookup queries
CREATE NONCLUSTERED INDEX IX_Users_Lookup 
ON AspNetUsers(Id) 
INCLUDE (UserName, Email, EmailConfirmed, LockoutEnabled, LockoutEnd);

-- Cover session statistics queries
CREATE NONCLUSTERED INDEX IX_Sessions_Statistics 
ON Sessions(IssuedAt, Status) 
INCLUDE (DeviceInfo, IpAddress);
```

---

## 5. Query Optimization Techniques

### 5.1 Parameterized Queries
```csharp
// Good: Parameterized query
var user = await _context.Users
    .Where(u => u.Email == email)
    .FirstOrDefaultAsync();

// Bad: String concatenation (SQL injection risk)
var query = $"SELECT * FROM Users WHERE Email = '{email}'";
```

### 5.2 Projection (Select Only Needed Columns)
```csharp
// Good: Project only needed fields
var userDto = await _context.Users
    .Where(u => u.Id == userId)
    .Select(u => new UserDto 
    { 
        Id = u.Id, 
        UserName = u.UserName, 
        Email = u.Email 
    })
    .FirstOrDefaultAsync();

// Bad: Select entire entity
var user = await _context.Users
    .FirstOrDefaultAsync(u => u.Id == userId);
```

### 5.3 AsNoTracking for Read-Only Queries
```csharp
// Good: Use AsNoTracking for read-only operations
var users = await _context.Users
    .AsNoTracking()
    .Where(u => u.EmailConfirmed)
    .ToListAsync();

// Use tracking only when updating
var user = await _context.Users
    .FirstOrDefaultAsync(u => u.Id == userId);
user.Email = newEmail;
await _context.SaveChangesAsync();
```

### 5.4 Batch Operations
```csharp
// Good: Batch insert
var sessions = new List<Session>();
foreach (var sessionData in sessionDataList)
{
    sessions.Add(new Session { ... });
}
_context.Sessions.AddRange(sessions);
await _context.SaveChangesAsync();

// Bad: Individual inserts
foreach (var sessionData in sessionDataList)
{
    _context.Sessions.Add(new Session { ... });
    await _context.SaveChangesAsync(); // Multiple DB round trips
}
```

### 5.5 Pagination with Offset
```csharp
var pageNumber = 1;
var pageSize = 20;

var users = await _context.Users
    .OrderBy(u => u.UserName)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .AsNoTracking()
    .ToListAsync();
```

### 5.6 Eager Loading vs Lazy Loading
```csharp
// Good: Eager loading with Include
var user = await _context.Users
    .Include(u => u.UserRoles)
        .ThenInclude(ur => ur.Role)
    .Include(u => u.UserClaims)
    .FirstOrDefaultAsync(u => u.Id == userId);

// Bad: Lazy loading (N+1 problem)
var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
var roles = user.UserRoles; // Separate query
var claims = user.UserClaims; // Another separate query
```

---

## 6. Caching Strategies

### 6.1 Cache-Aside Pattern
```
-> Check if data exists in Redis cache
-> If exists:
    -> Return cached data
-> If not exists:
    -> Query from database
    -> Store result in cache with expiration
    -> Return data
```

### 6.2 Write-Through Cache
```
-> When updating data:
    -> Update database
    -> If successful:
        -> Update cache
        -> Return success
    -> If failed:
        -> Return error
```

### 6.3 Cache Invalidation
```
-> On user update:
    -> Update database
    -> Remove user cache entry
    -> Remove related session cache entries
    -> Publish cache invalidation event

-> On session revocation:
    -> Update database
    -> Remove session cache
    -> Remove token cache
```

---

## 7. Database Partitioning

### 7.1 Horizontal Partitioning (Logs Table)
```sql
-- Partition by month
CREATE PARTITION FUNCTION pf_LogsByMonth (DATETIME2)
AS RANGE RIGHT FOR VALUES (
    '2025-01-01', '2025-02-01', '2025-03-01', 
    '2025-04-01', '2025-05-01', '2025-06-01',
    '2025-07-01', '2025-08-01', '2025-09-01',
    '2025-10-01', '2025-11-01', '2025-12-01'
);

CREATE PARTITION SCHEME ps_LogsByMonth
AS PARTITION pf_LogsByMonth
ALL TO ([PRIMARY]);

-- Create partitioned table
CREATE TABLE Logs (
    LogId NVARCHAR(450) NOT NULL,
    Timestamp DATETIME2 NOT NULL,
    -- other columns
    CONSTRAINT PK_Logs PRIMARY KEY (LogId, Timestamp)
) ON ps_LogsByMonth(Timestamp);
```

### 7.2 Table Archiving Strategy
```sql
-- Archive old sessions (older than 90 days)
CREATE PROCEDURE sp_ArchiveOldSessions
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        -- Move to archive table
        INSERT INTO SessionsArchive
        SELECT * FROM Sessions
        WHERE Status IN ('Expired', 'Revoked')
          AND IssuedAt < DATEADD(DAY, -90, GETUTCDATE());
        
        -- Delete from main table
        DELETE FROM Sessions
        WHERE Status IN ('Expired', 'Revoked')
          AND IssuedAt < DATEADD(DAY, -90, GETUTCDATE());
        
        COMMIT TRANSACTION;
        SELECT @@ROWCOUNT AS ArchivedSessions;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Archive old logs (older than 180 days)
CREATE PROCEDURE sp_ArchiveOldLogs
    @BatchSize INT = 10000
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @TotalArchived INT = 0;
    
    WHILE 1 = 1
    BEGIN
        BEGIN TRANSACTION;
        BEGIN TRY
            -- Move batch to archive
            INSERT INTO LogsArchive
            SELECT TOP (@BatchSize) *
            FROM Logs
            WHERE Timestamp < DATEADD(DAY, -180, GETUTCDATE());
            
            IF @@ROWCOUNT = 0
            BEGIN
                COMMIT TRANSACTION;
                BREAK;
            END
            
            -- Delete moved records
            DELETE TOP (@BatchSize) FROM Logs
            WHERE Timestamp < DATEADD(DAY, -180, GETUTCDATE());
            
            SET @TotalArchived = @TotalArchived + @@ROWCOUNT;
            
            COMMIT TRANSACTION;
            
            -- Small delay to prevent locking
            WAITFOR DELAY '00:00:01';
        END TRY
        BEGIN CATCH
            ROLLBACK TRANSACTION;
            THROW;
        END CATCH
    END
    
    SELECT @TotalArchived AS TotalArchivedLogs;
END
GO
```

---

## 8. Concurrency Control

### 8.1 Optimistic Concurrency (Using RowVersion)
```sql
-- Add RowVersion column to Users table
ALTER TABLE AspNetUsers
ADD RowVersion ROWVERSION;

-- Create index on RowVersion
CREATE NONCLUSTERED INDEX IX_Users_RowVersion 
ON AspNetUsers(RowVersion);
```

**Implementation in C#:**
```csharp
public async Task<(bool success, string error)> UpdateUserWithOptimisticLockAsync(
    string userId, 
    UpdateUserDto dto, 
    byte[] rowVersion)
{
    var user = await _context.Users
        .FirstOrDefaultAsync(u => u.Id == userId);
    
    if (user == null)
        return (false, "User not found");
    
    // Check row version for concurrency
    if (!user.RowVersion.SequenceEqual(rowVersion))
        return (false, "User has been modified by another process");
    
    // Update fields
    user.Email = dto.Email ?? user.Email;
    user.UserName = dto.UserName ?? user.UserName;
    
    try
    {
        await _context.SaveChangesAsync();
        return (true, null);
    }
    catch (DbUpdateConcurrencyException)
    {
        return (false, "Concurrent update detected");
    }
}
```

### 8.2 Pessimistic Concurrency (Row Locking)
```sql
-- Lock row for update
BEGIN TRANSACTION;

SELECT * FROM AspNetUsers WITH (UPDLOCK, ROWLOCK)
WHERE Id = @UserId;

-- Perform updates
UPDATE AspNetUsers
SET Email = @NewEmail
WHERE Id = @UserId;

COMMIT TRANSACTION;
```

### 8.3 Distributed Locking (Redis)
```csharp
public async Task<bool> AcquireDistributedLockAsync(
    string lockKey, 
    TimeSpan expiration)
{
    var lockValue = Guid.NewGuid().ToString();
    var acquired = await _redis.StringSetAsync(
        lockKey, 
        lockValue, 
        expiration, 
        When.NotExists
    );
    
    return acquired;
}

public async Task ReleaseLockAsync(string lockKey)
{
    await _redis.KeyDeleteAsync(lockKey);
}

// Usage
var lockKey = $"user:update:{userId}";
if (await AcquireDistributedLockAsync(lockKey, TimeSpan.FromSeconds(30)))
{
    try
    {
        // Perform update
        await UpdateUserAsync(userId, dto);
    }
    finally
    {
        await ReleaseLockAsync(lockKey);
    }
}
```

---

## 9. Bulk Operations

### 9.1 Bulk Insert Using Table-Valued Parameters
```sql
-- Create user-defined table type
CREATE TYPE UserListType AS TABLE (
    UserId NVARCHAR(450),
    UserName NVARCHAR(256),
    Email NVARCHAR(256),
    PasswordHash NVARCHAR(MAX)
);
GO

-- Bulk insert stored procedure
CREATE PROCEDURE sp_BulkInsertUsers
    @Users UserListType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, 
                            NormalizedEmail, PasswordHash, EmailConfirmed, 
                            SecurityStamp, ConcurrencyStamp, LockoutEnabled)
    SELECT 
        UserId,
        UserName,
        UPPER(UserName),
        Email,
        UPPER(Email),
        PasswordHash,
        0,
        NEWID(),
        NEWID(),
        1
    FROM @Users;
    
    SELECT @@ROWCOUNT AS InsertedUsers;
END
GO
```

**Usage in C#:**
```csharp
public async Task<int> BulkInsertUsersAsync(List<UserDto> users)
{
    var dataTable = new DataTable();
    dataTable.Columns.Add("UserId", typeof(string));
    dataTable.Columns.Add("UserName", typeof(string));
    dataTable.Columns.Add("Email", typeof(string));
    dataTable.Columns.Add("PasswordHash", typeof(string));
    
    foreach (var user in users)
    {
        dataTable.Rows.Add(user.Id, user.UserName, user.Email, user.PasswordHash);
    }
    
    using var connection = new SqlConnection(_connectionString);
    await connection.OpenAsync();
    
    using var command = new SqlCommand("sp_BulkInsertUsers", connection);
    command.CommandType = CommandType.StoredProcedure;
    
    var parameter = command.Parameters.AddWithValue("@Users", dataTable);
    parameter.SqlDbType = SqlDbType.Structured;
    parameter.TypeName = "dbo.UserListType";
    
    var result = await command.ExecuteScalarAsync();
    return Convert.ToInt32(result);
}
```

### 9.2 Bulk Update Pattern
```sql
CREATE PROCEDURE sp_BulkUpdateUserStatus
    @UserIds NVARCHAR(MAX), -- Comma-separated IDs
    @IsLocked BIT,
    @LockoutEnd DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Convert CSV to table
    DECLARE @UserIdTable TABLE (UserId NVARCHAR(450));
    
    INSERT INTO @UserIdTable
    SELECT value FROM STRING_SPLIT(@UserIds, ',');
    
    -- Bulk update
    UPDATE u
    SET 
        LockoutEnabled = @IsLocked,
        LockoutEnd = @LockoutEnd
    FROM AspNetUsers u
    INNER JOIN @UserIdTable ut ON u.Id = ut.UserId;
    
    SELECT @@ROWCOUNT AS UpdatedUsers;
END
GO
```

### 9.3 Bulk Delete with Soft Delete
```sql
CREATE PROCEDURE sp_BulkSoftDeleteUsers
    @UserIds NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @UserIdTable TABLE (UserId NVARCHAR(450));
        
        INSERT INTO @UserIdTable
        SELECT value FROM STRING_SPLIT(@UserIds, ',');
        
        -- Soft delete users
        UPDATE u
        SET 
            LockoutEnabled = 1,
            LockoutEnd = '9999-12-31 23:59:59.9999999'
        FROM AspNetUsers u
        INNER JOIN @UserIdTable ut ON u.Id = ut.UserId;
        
        DECLARE @DeletedCount INT = @@ROWCOUNT;
        
        -- Revoke all sessions
        UPDATE s
        SET 
            Status = 'Revoked',
            RevokedAt = GETUTCDATE(),
            RevokedBy = 'BULK_DELETE'
        FROM Sessions s
        INNER JOIN @UserIdTable ut ON s.UserId = ut.UserId
        WHERE s.Status = 'Active';
        
        COMMIT TRANSACTION;
        SELECT @DeletedCount AS DeletedUsers;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
```

---

## 10. Performance Monitoring Queries

### 10.1 Find Missing Indexes
```sql
SELECT 
    CONVERT(DECIMAL(18,2), user_seeks * avg_total_user_cost * (avg_user_impact * 0.01)) AS IndexAdvantage,
    migs.last_user_seek,
    mid.statement AS TableName,
    mid.equality_columns,
    mid.inequality_columns,
    mid.included_columns,
    'CREATE NONCLUSTERED INDEX IX_' + 
        REPLACE(REPLACE(REPLACE(mid.statement, '[', ''), ']', ''), '.', '_') + 
        '_' + REPLACE(ISNULL(mid.equality_columns, ''), ',', '_') +
    ' ON ' + mid.statement + 
    ' (' + ISNULL(mid.equality_columns, '') + 
    CASE WHEN mid.inequality_columns IS NOT NULL 
         THEN ',' + mid.inequality_columns ELSE '' END + ')' +
    CASE WHEN mid.included_columns IS NOT NULL 
         THEN ' INCLUDE (' + mid.included_columns + ')' ELSE '' END AS CreateIndexStatement
FROM sys.dm_db_missing_index_groups mig
INNER JOIN sys.dm_db_missing_index_group_stats migs ON migs.group_handle = mig.index_group_handle
INNER JOIN sys.dm_db_missing_index_details mid ON mig.index_handle = mid.index_handle
WHERE CONVERT(DECIMAL(18,2), user_seeks * avg_total_user_cost * (avg_user_impact * 0.01)) > 10
ORDER BY IndexAdvantage DESC;
```

### 10.2 Find Unused Indexes
```sql
SELECT 
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    us.user_seeks,
    us.user_scans,
    us.user_lookups,
    us.user_updates,
    'DROP INDEX ' + i.name + ' ON ' + OBJECT_NAME(i.object_id) AS DropIndexStatement
FROM sys.indexes i
LEFT JOIN sys.dm_db_index_usage_stats us 
    ON i.object_id = us.object_id 
    AND i.index_id = us.index_id
WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
    AND i.type_desc != 'CLUSTERED'
    AND us.user_seeks + us.user_scans + us.user_lookups = 0
    AND us.user_updates > 0
ORDER BY us.user_updates DESC;
```

### 10.3 Identify Slow Queries
```sql
SELECT TOP 20
    qs.execution_count,
    qs.total_elapsed_time / 1000000.0 AS TotalElapsedTimeSeconds,
    qs.total_elapsed_time / qs.execution_count / 1000.0 AS AvgElapsedTimeMs,
    qs.total_worker_time / 1000000.0 AS TotalWorkerTimeSeconds,
    qs.total_logical_reads,
    qs.total_logical_writes,
    SUBSTRING(qt.text, (qs.statement_start_offset/2)+1,
        ((CASE qs.statement_end_offset
            WHEN -1 THEN DATALENGTH(qt.text)
            ELSE qs.statement_end_offset
        END - qs.statement_start_offset)/2)+1) AS QueryText
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) qt
WHERE qt.text NOT LIKE '%sys.dm_exec%'
ORDER BY qs.total_elapsed_time DESC;
```

### 10.4 Check Index Fragmentation
```sql
SELECT 
    OBJECT_NAME(ips.object_id) AS TableName,
    i.name AS IndexName,
    ips.index_type_desc,
    ips.avg_fragmentation_in_percent,
    ips.page_count,
    CASE 
        WHEN ips.avg_fragmentation_in_percent > 30 THEN 'ALTER INDEX ' + i.name + ' ON ' + OBJECT_NAME(ips.object_id) + ' REBUILD;'
        WHEN ips.avg_fragmentation_in_percent > 10 THEN 'ALTER INDEX ' + i.name + ' ON ' + OBJECT_NAME(ips.object_id) + ' REORGANIZE;'
        ELSE 'No action needed'
    END AS RecommendedAction
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
INNER JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
WHERE ips.avg_fragmentation_in_percent > 10
    AND ips.page_count > 1000
ORDER BY ips.avg_fragmentation_in_percent DESC;
```

---

## 11. Maintenance Jobs

### 11.1 Index Maintenance Job
```sql
CREATE PROCEDURE sp_IndexMaintenance
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TableName NVARCHAR(256);
    DECLARE @IndexName NVARCHAR(256);
    DECLARE @Fragmentation FLOAT;
    DECLARE @SQL NVARCHAR(MAX);
    
    DECLARE IndexCursor CURSOR FOR
    SELECT 
        OBJECT_NAME(ips.object_id),
        i.name,
        ips.avg_fragmentation_in_percent
    FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
    INNER JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
    WHERE ips.avg_fragmentation_in_percent > 10
        AND ips.page_count > 1000
        AND i.name IS NOT NULL;
    
    OPEN IndexCursor;
    FETCH NEXT FROM IndexCursor INTO @TableName, @IndexName, @Fragmentation;
    
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @Fragmentation > 30
        BEGIN
            -- Rebuild index
            SET @SQL = 'ALTER INDEX ' + @IndexName + ' ON ' + @TableName + ' REBUILD WITH (ONLINE = ON);';
        END
        ELSE
        BEGIN
            -- Reorganize index
            SET @SQL = 'ALTER INDEX ' + @IndexName + ' ON ' + @TableName + ' REORGANIZE;';
        END
        
        EXEC sp_executesql @SQL;
        
        FETCH NEXT FROM IndexCursor INTO @TableName, @IndexName, @Fragmentation;
    END
    
    CLOSE IndexCursor;
    DEALLOCATE IndexCursor;
    
    -- Update statistics
    EXEC sp_updatestats;
END
GO
```

### 11.2 Statistics Update Job
```sql
CREATE PROCEDURE sp_UpdateAllStatistics
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TableName NVARCHAR(256);
    DECLARE @SQL NVARCHAR(MAX);
    
    DECLARE TableCursor CURSOR FOR
    SELECT name 
    FROM sys.tables 
    WHERE is_ms_shipped = 0;
    
    OPEN TableCursor;
    FETCH NEXT FROM TableCursor INTO @TableName;
    
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @SQL = 'UPDATE STATISTICS ' + @TableName + ' WITH FULLSCAN;';
        EXEC sp_executesql @SQL;
        
        FETCH NEXT FROM TableCursor INTO @TableName;
    END
    
    CLOSE TableCursor;
    DEALLOCATE TableCursor;
END
GO
```

### 11.3 Cleanup Job (Scheduled Daily)
```sql
CREATE PROCEDURE sp_DailyCleanupJob
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CleanupLog TABLE (
        Operation NVARCHAR(100),
        RecordsAffected INT,
        ExecutedAt DATETIME2
    );
    
    -- Cleanup expired sessions
    DECLARE @ExpiredSessions INT;
    EXEC sp_CleanupExpiredSessions @BatchSize = 1000;
    SELECT @ExpiredSessions = ExpiredSessions FROM #temp;
    
    INSERT INTO @CleanupLog VALUES ('Expired Sessions', @ExpiredSessions, GETUTCDATE());
    
    -- Delete old refresh tokens (older than 30 days)
    DELETE FROM RefreshTokens
    WHERE ExpiresAt < DATEADD(DAY, -30, GETUTCDATE());
    
    INSERT INTO @CleanupLog VALUES ('Old Refresh Tokens', @@ROWCOUNT, GETUTCDATE());
    
    -- Archive old logs (older than 180 days)
    EXEC sp_ArchiveOldLogs @BatchSize = 10000;
    
    -- Update statistics on frequently modified tables
    UPDATE STATISTICS Sessions;
    UPDATE STATISTICS Logs;
    
    -- Return cleanup summary
    SELECT * FROM @CleanupLog;
END
GO
```

---

## 12. Database Backup Strategy

### 12.1 Full Backup
```sql
-- Full database backup
BACKUP DATABASE [MicroservicesDB]
TO DISK = 'D:\Backups\MicroservicesDB_Full.bak'
WITH FORMAT, INIT, NAME = 'Full Database Backup', 
COMPRESSION, STATS = 10;
```

### 12.2 Differential Backup
```sql
-- Differential backup (daily)
BACKUP DATABASE [MicroservicesDB]
TO DISK = 'D:\Backups\MicroservicesDB_Diff.bak'
WITH DIFFERENTIAL, FORMAT, INIT, 
NAME = 'Differential Database Backup',
COMPRESSION, STATS = 10;
```

### 12.3 Transaction Log Backup
```sql
-- Transaction log backup (hourly)
BACKUP LOG [MicroservicesDB]
TO DISK = 'D:\Backups\MicroservicesDB_Log.trn'
WITH FORMAT, INIT, NAME = 'Transaction Log Backup',
COMPRESSION, STATS = 10;
```

---

## 13. Best Practices Summary

### 13.1 Transaction Best Practices
✅ Keep transactions short
✅ Avoid user interaction during transactions
✅ Use appropriate isolation levels
✅ Always use try-catch with rollback
✅ Don't nest transactions unnecessarily
✅ Release locks as soon as possible

### 13.2 Stored Procedure Best Practices
✅ Use SET NOCOUNT ON
✅ Always use error handling (TRY-CATCH)
✅ Use parameterized queries
✅ Return status codes or result sets
✅ Include proper documentation
✅ Test with various inputs

### 13.3 Trigger Best Practices
✅ Keep triggers lightweight
✅ Avoid complex logic in triggers
✅ Don't call stored procedures from triggers
✅ Use triggers for audit only
✅ Consider performance impact
✅ Test trigger behavior thoroughly

### 13.4 Indexing Best Practices
✅ Index foreign keys
✅ Index columns used in WHERE clauses
✅ Use covering indexes for frequent queries
✅ Monitor index usage
✅ Remove unused indexes
✅ Rebuild/reorganize fragmented indexes regularly

### 13.5 Query Optimization Best Practices
✅ Use AsNoTracking for read-only queries
✅ Select only needed columns
✅ Use pagination for large result sets
✅ Avoid N+1 queries with Include
✅ Use compiled queries for repeated operations
✅ Profile queries with execution plans

### 13.6 Caching Best Practices
✅ Cache frequently accessed data
✅ Set appropriate expiration times
✅ Invalidate cache on updates
✅ Use distributed cache for microservices
✅ Monitor cache hit rates
✅ Handle cache failures gracefully

### 13.7 Maintenance Best Practices
✅ Schedule regular index maintenance
✅ Update statistics regularly
✅ Archive old data
✅ Monitor database size
✅ Implement backup strategy
✅ Test restore procedures
✅ Monitor query performance
✅ Review execution plans periodically
```
-- Drop databases if they exist (clean start)
IF EXISTS(SELECT name FROM master.dbo.sysdatabases WHERE name = 'USER_SERVICE_DB')
    DROP DATABASE USER_SERVICE_DB;

IF EXISTS(SELECT name FROM master.dbo.sysdatabases WHERE name = 'AUTH_SERVICE_DB') 
    DROP DATABASE AUTH_SERVICE_DB;

IF EXISTS(SELECT name FROM master.dbo.sysdatabases WHERE name = 'LOG_SERVICE_DB')
    DROP DATABASE LOG_SERVICE_DB;
GO

-- Create fresh databases
CREATE DATABASE USER_SERVICE_DB;
CREATE DATABASE AUTH_SERVICE_DB;
CREATE DATABASE LOG_SERVICE_DB;
GO

PRINT '✅ Databases dropped and recreated successfully';
GO

-- Switch to UserService database and create Identity tables
USE USER_SERVICE_DB;
GO

-- AspNetRoles table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetRoles' AND xtype='U')
CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
GO

-- AspNetUsers table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUsers' AND xtype='U')
CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    [FirstName] nvarchar(max) NULL,
    [LastName] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
GO

-- AspNetUserRoles table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUserRoles' AND xtype='U')
CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
GO

-- Create Identity indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AspNetRoles_NormalizedName' AND object_id = OBJECT_ID('AspNetRoles'))
CREATE UNIQUE INDEX [IX_AspNetRoles_NormalizedName] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AspNetUsers_NormalizedUserName' AND object_id = OBJECT_ID('AspNetUsers'))
CREATE UNIQUE INDEX [IX_AspNetUsers_NormalizedUserName] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AspNetUsers_NormalizedEmail' AND object_id = OBJECT_ID('AspNetUsers'))
CREATE UNIQUE INDEX [IX_AspNetUsers_NormalizedEmail] ON [AspNetUsers] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL;
GO

PRINT '✅ UserService Identity tables created successfully';
GO

-- Switch to LoggerService database
USE LOG_SERVICE_DB;
GO

-- ApplicationLogs table - Aligned with your entity
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ApplicationLogs' AND xtype='U')
CREATE TABLE [ApplicationLogs] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [ApplicationLogId] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Message] nvarchar(max) NULL,
    [MessageTemplate] nvarchar(max) NULL,
    [Level] nvarchar(128) NULL,
    [Timestamp] datetime2 NOT NULL,
    [Exception] nvarchar(max) NULL,
    [Properties] nvarchar(max) NULL,
    CONSTRAINT [PK_ApplicationLogs] PRIMARY KEY ([Id])
    );
GO

-- Create index on ApplicationLogId
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ApplicationLogs_ApplicationLogId' AND object_id = OBJECT_ID('ApplicationLogs'))
CREATE UNIQUE INDEX [IX_ApplicationLogs_ApplicationLogId] ON [ApplicationLogs] ([ApplicationLogId]);
GO

PRINT '✅ LoggerService tables created successfully';
GO

-- Switch to AuthService database  
USE AUTH_SERVICE_DB;
GO

-- RefreshTokens table for AuthService
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RefreshTokens' AND xtype='U')
CREATE TABLE [RefreshTokens] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Token] nvarchar(450) NOT NULL,
    [JwtId] nvarchar(450) NULL,
    [UserId] nvarchar(450) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [Used] bit NOT NULL DEFAULT 0,
    [Invalidated] bit NOT NULL DEFAULT 0,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id])
    );
GO

PRINT '✅ AuthService tables created successfully';
GO

PRINT '🎉 All databases and tables created successfully!';
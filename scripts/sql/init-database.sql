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

-- Switch to UserService database and create ALL Identity tables
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
    [EmailConfirmed] bit NOT NULL DEFAULT 0,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL DEFAULT 0,
    [TwoFactorEnabled] bit NOT NULL DEFAULT 0,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL DEFAULT 0,
    [AccessFailedCount] int NOT NULL DEFAULT 0,
    [FirstName] nvarchar(max) NULL,
    [LastName] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
GO

-- AspNetRoleClaims table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetRoleClaims' AND xtype='U')
CREATE TABLE [AspNetRoleClaims] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
GO

-- AspNetUserClaims table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUserClaims' AND xtype='U')
CREATE TABLE [AspNetUserClaims] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
GO

-- AspNetUserLogins table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUserLogins' AND xtype='U')
CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
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

-- AspNetUserTokens table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUserTokens' AND xtype='U')
CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
GO

-- Create ALL Identity indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'RoleNameIndex' AND object_id = OBJECT_ID('AspNetRoles'))
CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AspNetRoleClaims_RoleId' AND object_id = OBJECT_ID('AspNetRoleClaims'))
CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'EmailIndex' AND object_id = OBJECT_ID('AspNetUsers'))
CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UserNameIndex' AND object_id = OBJECT_ID('AspNetUsers'))
CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AspNetUserClaims_UserId' AND object_id = OBJECT_ID('AspNetUserClaims'))
CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AspNetUserLogins_UserId' AND object_id = OBJECT_ID('AspNetUserLogins'))
CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AspNetUserRoles_RoleId' AND object_id = OBJECT_ID('AspNetUserRoles'))
CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO

PRINT '✅ UserService ALL Identity tables and indexes created successfully';
GO

-- Switch to LoggerService database
USE LOG_SERVICE_DB;
GO

-- ApplicationLogs table - Updated to match your EF migrations exactly
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ApplicationLogs' AND xtype='U')
CREATE TABLE [ApplicationLogs] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [ApplicationLogId] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Message] nvarchar(max) NOT NULL,
    [MessageTemplate] nvarchar(max) NOT NULL,
    [Level] nvarchar(max) NOT NULL,
    [Timestamp] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [Exception] nvarchar(max) NOT NULL,
    [Properties] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_ApplicationLogs] PRIMARY KEY ([Id])
    );
GO

-- Create index on ApplicationLogId
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ApplicationLogs_ApplicationLogId' AND object_id = OBJECT_ID('ApplicationLogs'))
CREATE UNIQUE INDEX [IX_ApplicationLogs_ApplicationLogId] ON [ApplicationLogs] ([ApplicationLogId]);
GO

PRINT '✅ LoggerService tables created successfully (matching EF migrations)';
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
    [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [ExpiresAt] datetime2 NOT NULL,
    [Used] bit NOT NULL DEFAULT 0,
    [Invalidated] bit NOT NULL DEFAULT 0,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id])
    );
GO

-- Create index on Token for faster lookups
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RefreshTokens_Token' AND object_id = OBJECT_ID('RefreshTokens'))
CREATE INDEX [IX_RefreshTokens_Token] ON [RefreshTokens] ([Token]);

-- Create index on UserId for user-specific token queries
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RefreshTokens_UserId' AND object_id = OBJECT_ID('RefreshTokens'))
CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
GO

PRINT '✅ AuthService tables created successfully';
GO

-- Verify all tables were created
PRINT '🔍 Verifying table creation...';
GO

USE USER_SERVICE_DB;
SELECT 'USER_SERVICE_DB Tables:' AS Info;
SELECT name FROM sysobjects WHERE xtype = 'U' ORDER BY name;
GO

USE AUTH_SERVICE_DB;
SELECT 'AUTH_SERVICE_DB Tables:' AS Info;
SELECT name FROM sysobjects WHERE xtype = 'U' ORDER BY name;
GO

USE LOG_SERVICE_DB;
SELECT 'LOG_SERVICE_DB Tables:' AS Info;
SELECT name FROM sysobjects WHERE xtype = 'U' ORDER BY name;
GO

PRINT '🎉 All databases, tables, and indexes created successfully!';
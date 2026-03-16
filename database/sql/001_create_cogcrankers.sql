IF DB_ID(N'CogCrankers') IS NULL
BEGIN
    CREATE DATABASE [CogCrankers];
END;
GO

USE [CogCrankers];
GO

IF OBJECT_ID(N'dbo.CogTransactions', N'U') IS NOT NULL DROP TABLE [dbo].[CogTransactions];
IF OBJECT_ID(N'dbo.UserInventories', N'U') IS NOT NULL DROP TABLE [dbo].[UserInventories];
IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NOT NULL DROP TABLE [dbo].[UserRoles];
IF OBJECT_ID(N'dbo.GearItems', N'U') IS NOT NULL DROP TABLE [dbo].[GearItems];
IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL DROP TABLE [dbo].[Roles];
IF OBJECT_ID(N'dbo.UserAccounts', N'U') IS NOT NULL DROP TABLE [dbo].[UserAccounts];
GO

CREATE TABLE [dbo].[UserAccounts]
(
    [UserAccountId] INT IDENTITY(1,1) NOT NULL,
    [GoogleSubject] NVARCHAR(256) NOT NULL,
    [Email] NVARCHAR(320) NOT NULL,
    [DisplayName] NVARCHAR(120) NOT NULL,
    [AvatarUrl] NVARCHAR(500) NULL,
    [CreatedAtUtc] DATETIME2(0) NOT NULL CONSTRAINT [DF_UserAccounts_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [LastLoginAtUtc] DATETIME2(0) NOT NULL CONSTRAINT [DF_UserAccounts_LastLoginAtUtc] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_UserAccounts] PRIMARY KEY CLUSTERED ([UserAccountId] ASC),
    CONSTRAINT [UQ_UserAccounts_GoogleSubject] UNIQUE ([GoogleSubject]),
    CONSTRAINT [UQ_UserAccounts_Email] UNIQUE ([Email])
);
GO

CREATE TABLE [dbo].[Roles]
(
    [RoleId] INT IDENTITY(1,1) NOT NULL,
    [Name] NVARCHAR(64) NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([RoleId] ASC),
    CONSTRAINT [UQ_Roles_Name] UNIQUE ([Name])
);
GO

CREATE TABLE [dbo].[UserRoles]
(
    [UserAccountId] INT NOT NULL,
    [RoleId] INT NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([UserAccountId] ASC, [RoleId] ASC),
    CONSTRAINT [FK_UserRoles_UserAccounts] FOREIGN KEY ([UserAccountId]) REFERENCES [dbo].[UserAccounts]([UserAccountId]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserRoles_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([RoleId]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[GearItems]
(
    [GearItemId] INT IDENTITY(1,1) NOT NULL,
    [Name] NVARCHAR(120) NOT NULL,
    [Description] NVARCHAR(600) NULL,
    [GearType] NVARCHAR(60) NOT NULL,
    [CostInCogs] INT NOT NULL,
    [StockQuantity] INT NULL,
    [IsActive] BIT NOT NULL CONSTRAINT [DF_GearItems_IsActive] DEFAULT (1),
    [FlavorText] NVARCHAR(300) NULL,
    [CreatedAtUtc] DATETIME2(0) NOT NULL CONSTRAINT [DF_GearItems_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    [UpdatedAtUtc] DATETIME2(0) NOT NULL CONSTRAINT [DF_GearItems_UpdatedAtUtc] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_GearItems] PRIMARY KEY CLUSTERED ([GearItemId] ASC),
    CONSTRAINT [CK_GearItems_Cost] CHECK ([CostInCogs] >= 0),
    CONSTRAINT [CK_GearItems_Stock] CHECK ([StockQuantity] IS NULL OR [StockQuantity] >= 0)
);
GO

CREATE TABLE [dbo].[UserInventories]
(
    [UserInventoryId] INT IDENTITY(1,1) NOT NULL,
    [UserAccountId] INT NOT NULL,
    [GearItemId] INT NOT NULL,
    [Quantity] INT NOT NULL,
    [LastGrantedAtUtc] DATETIME2(0) NOT NULL CONSTRAINT [DF_UserInventories_LastGrantedAtUtc] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_UserInventories] PRIMARY KEY CLUSTERED ([UserInventoryId] ASC),
    CONSTRAINT [UQ_UserInventories_UserGear] UNIQUE ([UserAccountId], [GearItemId]),
    CONSTRAINT [FK_UserInventories_UserAccounts] FOREIGN KEY ([UserAccountId]) REFERENCES [dbo].[UserAccounts]([UserAccountId]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserInventories_GearItems] FOREIGN KEY ([GearItemId]) REFERENCES [dbo].[GearItems]([GearItemId]) ON DELETE CASCADE,
    CONSTRAINT [CK_UserInventories_Quantity] CHECK ([Quantity] > 0)
);
GO

CREATE TABLE [dbo].[CogTransactions]
(
    [CogTransactionId] INT IDENTITY(1,1) NOT NULL,
    [UserAccountId] INT NOT NULL,
    [Amount] INT NOT NULL,
    [TransactionType] NVARCHAR(50) NOT NULL,
    [Description] NVARCHAR(300) NOT NULL,
    [GearItemId] INT NULL,
    [GrantedByUserAccountId] INT NULL,
    [CreatedAtUtc] DATETIME2(0) NOT NULL CONSTRAINT [DF_CogTransactions_CreatedAtUtc] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_CogTransactions] PRIMARY KEY CLUSTERED ([CogTransactionId] ASC),
    CONSTRAINT [FK_CogTransactions_UserAccounts] FOREIGN KEY ([UserAccountId]) REFERENCES [dbo].[UserAccounts]([UserAccountId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CogTransactions_GearItems] FOREIGN KEY ([GearItemId]) REFERENCES [dbo].[GearItems]([GearItemId]) ON DELETE SET NULL,
    CONSTRAINT [FK_CogTransactions_GrantedByUserAccounts] FOREIGN KEY ([GrantedByUserAccountId]) REFERENCES [dbo].[UserAccounts]([UserAccountId]) ON DELETE SET NULL
);
GO

INSERT INTO [dbo].[Roles] ([Name]) VALUES (N'CogAdmin');
INSERT INTO [dbo].[Roles] ([Name]) VALUES (N'CogUser');
GO

INSERT INTO [dbo].[GearItems] ([Name], [Description], [GearType], [CostInCogs], [StockQuantity], [IsActive], [FlavorText])
VALUES
    (N'Bronze Bolt Badge', N'A humble emblem proving you can keep the machine humming.', N'Badge', 15, NULL, 1, N'Entry-level swagger for new sprocket pilots.'),
    (N'Torque Tonic', N'Instant confidence in a bottle. Side effects include dramatic speech.', N'Boost', 35, 250, 1, N'One sip and your excuses lose all traction.'),
    (N'Clockwork Cape', N'A sweeping cape for those with perfectly timed heroics.', N'Cosmetic', 120, 40, 1, N'Flutters majestically in binary winds.'),
    (N'Titanium Teeth Upgrade', N'Sharper gears for faster deal-making and cleaner code merges.', N'Upgrade', 300, 20, 1, N'Your grindset just got precision-machined.');
GO

/*
After first login, grant admin role to a user by email:

DECLARE @TargetEmail NVARCHAR(320) = N'you@example.com';

INSERT INTO [dbo].[UserRoles] ([UserAccountId], [RoleId])
SELECT ua.[UserAccountId], r.[RoleId]
FROM [dbo].[UserAccounts] ua
CROSS JOIN [dbo].[Roles] r
WHERE ua.[Email] = @TargetEmail
  AND r.[Name] = N'CogAdmin'
  AND NOT EXISTS
  (
      SELECT 1
      FROM [dbo].[UserRoles] ur
      WHERE ur.[UserAccountId] = ua.[UserAccountId]
        AND ur.[RoleId] = r.[RoleId]
  );
*/
GO

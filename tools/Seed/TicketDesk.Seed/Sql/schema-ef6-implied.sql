-- TicketDesk 2.5 schema as implied by the EF6 model, for databases that were NOT created by the legacy app.
-- Source: TicketDesk.Domain migrations InitialCreate..Td2.5.022 (final model snapshot in the 022 .resx) and the
-- ASP.NET Identity 2.2.1 tables of TdIdentityContext (automatic migrations, table names from OnModelCreating).
-- The notifications.* tables of TdPushNotificationContext (automatic migrations, no snapshot in the repo) are at the end, as
-- the legacy app created them (read from the restored legacy database, td-sql TicketDesk, 2026-11-13: columns, keys,
-- indexes, foreign keys); the new app writes them too (new users' e-mail destination, push settings).
-- Not included: __MigrationHistory rows, so the legacy app itself will not accept this database as "ready".
-- When the real clean backup exists, prefer restoring it (ops/dev/td-sql.sh restore) and run `td-seed verify`.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE TABLE [dbo].[__MigrationHistory] (
    [MigrationId] nvarchar(150) NOT NULL,
    [ContextKey] nvarchar(300) NOT NULL,
    [Model] varbinary(max) NOT NULL,
    [ProductVersion] nvarchar(32) NOT NULL,
    CONSTRAINT [PK_dbo.__MigrationHistory] PRIMARY KEY ([MigrationId], [ContextKey])
);
CREATE TABLE [dbo].[Projects] (
    [ProjectId] int IDENTITY(1,1) NOT NULL,
    [ProjectName] nvarchar(100) NOT NULL,
    [ProjectDescription] nvarchar(500) NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_dbo.Projects] PRIMARY KEY ([ProjectId])
);
CREATE TABLE [dbo].[Tickets] (
    [TicketId] int IDENTITY(1,1) NOT NULL,
    [TicketType] nvarchar(50) NOT NULL,
    [Category] nvarchar(50) NOT NULL,
    [Title] nvarchar(500) NOT NULL,
    [Details] nvarchar(max) NOT NULL,
    [IsHtml] bit NOT NULL,
    [TagList] nvarchar(100) NULL,
    [CreatedBy] nvarchar(256) NOT NULL,
    [CreatedDate] datetimeoffset(7) NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
    [Owner] nvarchar(256) NOT NULL,
    [AssignedTo] nvarchar(256) NULL,
    [TicketStatus] int NOT NULL,
    [CurrentStatusDate] datetimeoffset(7) NOT NULL,
    [CurrentStatusSetBy] nvarchar(256) NOT NULL,
    [LastUpdateBy] nvarchar(256) NOT NULL,
    [LastUpdateDate] datetimeoffset(7) NOT NULL,
    [Priority] nvarchar(25) NULL,
    [AffectsCustomer] bit NOT NULL,
    [Version] rowversion NOT NULL,
    [ProjectId] int NOT NULL DEFAULT ((1)),
    [DueDate] datetimeoffset(7) NULL,
    [EstimatedDuration] decimal(18,2) NULL,
    [ActualDuration] decimal(18,2) NULL,
    [TargetDate] datetimeoffset(7) NULL,
    [ResolutionDate] datetimeoffset(7) NULL,
    CONSTRAINT [PK_dbo.Tickets] PRIMARY KEY ([TicketId])
);
CREATE INDEX [IX_ProjectId] ON [dbo].[Tickets]([ProjectId]);
-- Td2.5.017 created this FK with cascadeDelete: false although the model snapshot says Cascade.
ALTER TABLE [dbo].[Tickets] ADD CONSTRAINT [FK_dbo.Tickets_dbo.Projects_ProjectId]
    FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects] ([ProjectId]);
CREATE TABLE [dbo].[TicketEvents] (
    [TicketId] int NOT NULL,
    [EventId] int IDENTITY(1,1) NOT NULL,
    [EventDescription] nvarchar(500) NULL,
    [Comment] nvarchar(max) NULL,
    [IsHtml] bit NOT NULL,
    [EventBy] nvarchar(256) NOT NULL,
    [EventDate] datetimeoffset(7) NOT NULL DEFAULT (SYSDATETIMEOFFSET()),
    [Version] rowversion NOT NULL,
    [ForActivity] int NOT NULL DEFAULT ((16384)),
    CONSTRAINT [PK_dbo.TicketEvents] PRIMARY KEY ([TicketId], [EventId])
);
CREATE INDEX [IX_TicketId] ON [dbo].[TicketEvents]([TicketId]);
ALTER TABLE [dbo].[TicketEvents] ADD CONSTRAINT [FK_dbo.TicketComments_dbo.Tickets_TicketId]
    FOREIGN KEY ([TicketId]) REFERENCES [dbo].[Tickets] ([TicketId]) ON DELETE CASCADE;
CREATE TABLE [dbo].[TicketSubscribers] (
    [TicketId] int NOT NULL,
    [SubscriberId] nvarchar(256) NOT NULL,
    CONSTRAINT [PK_dbo.TicketSubscribers] PRIMARY KEY ([TicketId], [SubscriberId])
);
CREATE INDEX [IX_TicketId] ON [dbo].[TicketSubscribers]([TicketId]);
ALTER TABLE [dbo].[TicketSubscribers] ADD CONSTRAINT [FK_dbo.TicketSubscribers_dbo.Tickets_TicketId]
    FOREIGN KEY ([TicketId]) REFERENCES [dbo].[Tickets] ([TicketId]) ON DELETE CASCADE;
CREATE TABLE [dbo].[TicketEventNotifications] (
    [TicketId] int NOT NULL,
    [EventId] int NOT NULL,
    [SubscriberId] nvarchar(256) NOT NULL,
    [IsNew] bit NOT NULL,
    [IsRead] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_dbo.TicketEventNotifications] PRIMARY KEY ([TicketId], [EventId], [SubscriberId])
);
CREATE INDEX [IX_TicketId_EventId] ON [dbo].[TicketEventNotifications]([TicketId], [EventId]);
CREATE INDEX [IX_TicketId_SubscriberId] ON [dbo].[TicketEventNotifications]([TicketId], [SubscriberId]);
CREATE INDEX [IX_EventId] ON [dbo].[TicketEventNotifications]([EventId]);
CREATE INDEX [IX_SubscriberId] ON [dbo].[TicketEventNotifications]([SubscriberId]);
ALTER TABLE [dbo].[TicketEventNotifications] ADD CONSTRAINT [FK_dbo.TicketEventNotifications_dbo.TicketEvents_TicketId_EventId]
    FOREIGN KEY ([TicketId], [EventId]) REFERENCES [dbo].[TicketEvents] ([TicketId], [EventId]) ON DELETE CASCADE;
ALTER TABLE [dbo].[TicketEventNotifications] ADD CONSTRAINT [FK_dbo.TicketEventNotifications_dbo.TicketSubscribers_TicketId_SubscriberId]
    FOREIGN KEY ([TicketId], [SubscriberId]) REFERENCES [dbo].[TicketSubscribers] ([TicketId], [SubscriberId]);
CREATE TABLE [dbo].[TicketTags] (
    [TicketTagId] int IDENTITY(1,1) NOT NULL,
    [TagName] nvarchar(100) NOT NULL,
    [TicketId] int NOT NULL,
    CONSTRAINT [PK_dbo.TicketTags] PRIMARY KEY ([TicketTagId])
);
CREATE INDEX [IX_TicketId] ON [dbo].[TicketTags]([TicketId]);
ALTER TABLE [dbo].[TicketTags] ADD CONSTRAINT [FK_dbo.TicketTags_dbo.Tickets_TicketId]
    FOREIGN KEY ([TicketId]) REFERENCES [dbo].[Tickets] ([TicketId]) ON DELETE CASCADE;
CREATE TABLE [dbo].[UserSettings] (
    [UserId] nvarchar(256) NOT NULL,
    [ListSettingsJson] nvarchar(max) NULL,
    [SelectedProjectId] int NULL,
    CONSTRAINT [PK_dbo.UserSettings] PRIMARY KEY ([UserId])
);
CREATE TABLE [dbo].[ApplicationSettings] (
    [ApplicationName] nvarchar(128) NOT NULL,
    [PermissionsSettingsJson] nvarchar(max) NULL,
    [SelectListSettingsJson] nvarchar(max) NULL,
    [SecuritySettingsJson] nvarchar(max) NULL,
    [ClientSettingsJson] nvarchar(max) NULL,
    CONSTRAINT [PK_dbo.ApplicationSettings] PRIMARY KEY ([ApplicationName])
);
CREATE TABLE [dbo].[IdentityRoles] (
    [Id] nvarchar(128) NOT NULL,
    [Name] nvarchar(256) NOT NULL,
    [DisplayName] nvarchar(100) NULL,
    [Description] nvarchar(500) NULL,
    [Discriminator] nvarchar(128) NOT NULL,
    CONSTRAINT [PK_dbo.IdentityRoles] PRIMARY KEY ([Id])
);
CREATE UNIQUE INDEX [RoleNameIndex] ON [dbo].[IdentityRoles]([Name]);
CREATE TABLE [dbo].[IdentityUsers] (
    [Id] nvarchar(128) NOT NULL,
    [DisplayName] nvarchar(100) NOT NULL,
    [Email] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEndDateUtc] datetime NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    [UserName] nvarchar(256) NOT NULL,
    CONSTRAINT [PK_dbo.IdentityUsers] PRIMARY KEY ([Id])
);
CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[IdentityUsers]([UserName]);
CREATE TABLE [dbo].[IdentityUserRoles] (
    [UserId] nvarchar(128) NOT NULL,
    [RoleId] nvarchar(128) NOT NULL,
    CONSTRAINT [PK_dbo.IdentityUserRoles] PRIMARY KEY ([UserId], [RoleId])
);
CREATE INDEX [IX_UserId] ON [dbo].[IdentityUserRoles]([UserId]);
CREATE INDEX [IX_RoleId] ON [dbo].[IdentityUserRoles]([RoleId]);
ALTER TABLE [dbo].[IdentityUserRoles] ADD CONSTRAINT [FK_dbo.IdentityUserRoles_dbo.IdentityRoles_RoleId]
    FOREIGN KEY ([RoleId]) REFERENCES [dbo].[IdentityRoles] ([Id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[IdentityUserRoles] ADD CONSTRAINT [FK_dbo.IdentityUserRoles_dbo.IdentityUsers_UserId]
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[IdentityUsers] ([Id]) ON DELETE CASCADE;
CREATE TABLE [dbo].[IdentityUserClaims] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] nvarchar(128) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_dbo.IdentityUserClaims] PRIMARY KEY ([Id])
);
CREATE INDEX [IX_UserId] ON [dbo].[IdentityUserClaims]([UserId]);
ALTER TABLE [dbo].[IdentityUserClaims] ADD CONSTRAINT [FK_dbo.IdentityUserClaims_dbo.IdentityUsers_UserId]
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[IdentityUsers] ([Id]) ON DELETE CASCADE;
CREATE TABLE [dbo].[IdentityUserLogins] (
    [LoginProvider] nvarchar(128) NOT NULL,
    [ProviderKey] nvarchar(128) NOT NULL,
    [UserId] nvarchar(128) NOT NULL,
    CONSTRAINT [PK_dbo.IdentityUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey], [UserId])
);
CREATE INDEX [IX_UserId] ON [dbo].[IdentityUserLogins]([UserId]);
ALTER TABLE [dbo].[IdentityUserLogins] ADD CONSTRAINT [FK_dbo.IdentityUserLogins_dbo.IdentityUsers_UserId]
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[IdentityUsers] ([Id]) ON DELETE CASCADE;
GO
-- notifications.*: TdPushNotificationContext's tables (Round 5b: without them an empty installation's new-user path failed
-- with 500 when it added the user's e-mail destination)
CREATE SCHEMA [notifications];
GO
CREATE TABLE [notifications].[ApplicationPushNotificationSettings] (
    [ApplicationName] nvarchar(128) NOT NULL,
    [PushNotificationSettingsJson] nvarchar(max) NULL,
    CONSTRAINT [PK_notifications.ApplicationPushNotificationSettings] PRIMARY KEY ([ApplicationName])
);
CREATE TABLE [notifications].[SubscriberPushNotificationSettings] (
    [SubscriberId] nvarchar(256) NOT NULL,
    [IsEnabled] bit NOT NULL,
    CONSTRAINT [PK_notifications.SubscriberPushNotificationSettings] PRIMARY KEY ([SubscriberId])
);
CREATE TABLE [notifications].[PushNotificationDestinations] (
    [DestinationId] int IDENTITY(1,1) NOT NULL,
    [SubscriberName] nvarchar(100) NULL,
    [DestinationAddress] nvarchar(256) NULL,
    [DestinationType] nvarchar(50) NULL,
    [SubscriberId] nvarchar(256) NULL,
    CONSTRAINT [PK_notifications.PushNotificationDestinations] PRIMARY KEY ([DestinationId])
);
CREATE UNIQUE INDEX [IX_SubscriberDestination] ON [notifications].[PushNotificationDestinations]([SubscriberName], [DestinationAddress], [DestinationType], [SubscriberId]);
CREATE TABLE [notifications].[PushNotificationItems] (
    [ContentSourceId] int NOT NULL,
    [PushNotificationItemId] int IDENTITY(1,1) NOT NULL,
    [ContentSourceType] nvarchar(100) NOT NULL,
    [SubscriberId] nvarchar(256) NOT NULL,
    [DestinationId] int NOT NULL,
    [CreatedDate] datetimeoffset NOT NULL,
    [ScheduledSendDate] datetimeoffset NULL,
    [DeliveryStatus] int NOT NULL,
    [RetryCount] int NOT NULL,
    [MessageContent] nvarchar(max) NULL,
    CONSTRAINT [PK_notifications.PushNotificationItems] PRIMARY KEY ([PushNotificationItemId])
);
CREATE INDEX [IX_PushNotificationTarget] ON [notifications].[PushNotificationItems]([ContentSourceId], [ContentSourceType], [SubscriberId], [DestinationId]);
CREATE TABLE [notifications].[TicketPushNotificationItems] (
    [PushNotificationItemId] int NOT NULL,
    [TicketEventsList] nvarchar(max) NULL,
    [CanceledEventsList] nvarchar(max) NULL,
    CONSTRAINT [PK_notifications.TicketPushNotificationItems] PRIMARY KEY ([PushNotificationItemId])
);
CREATE INDEX [IX_PushNotificationItemId] ON [notifications].[TicketPushNotificationItems]([PushNotificationItemId]);
ALTER TABLE [notifications].[PushNotificationDestinations] ADD CONSTRAINT [FK_notifications.PushNotificationDestinations_notifications.SubscriberPushNotificationSettings_SubscriberId]
    FOREIGN KEY ([SubscriberId]) REFERENCES [notifications].[SubscriberPushNotificationSettings] ([SubscriberId]);
ALTER TABLE [notifications].[PushNotificationItems] ADD CONSTRAINT [FK_notifications.PushNotificationItems_notifications.PushNotificationDestinations_DestinationId]
    FOREIGN KEY ([DestinationId]) REFERENCES [notifications].[PushNotificationDestinations] ([DestinationId]) ON DELETE CASCADE;
ALTER TABLE [notifications].[TicketPushNotificationItems] ADD CONSTRAINT [FK_notifications.TicketPushNotificationItems_notifications.PushNotificationItems_PushNotificationItemId]
    FOREIGN KEY ([PushNotificationItemId]) REFERENCES [notifications].[PushNotificationItems] ([PushNotificationItemId]);
GO
-- Rows the migrations / first-run setup create.
INSERT INTO [dbo].[Projects] ([ProjectName], [ProjectDescription]) VALUES (N'Default', N'Default project');
INSERT INTO [dbo].[ApplicationSettings] ([ApplicationName], [PermissionsSettingsJson], [SelectListSettingsJson])
VALUES (N'TicketDesk',
        N'{"AllowInternalUsersToEditPriority": false, "AllowInternalUsersToEditTags": true}',
        N'{"CategoryList": ["Software","Hardware","Network"], "PriorityList": ["High","Medium","Low"], "TicketTypesList": ["Problem","Question","Request"]}');
INSERT INTO [dbo].[IdentityRoles] ([Id], [Name], [DisplayName], [Description], [Discriminator]) VALUES
    (CONVERT(nvarchar(128), NEWID()), N'TdAdministrators', N'Administrators', N'Administrators', N'TicketDeskRole'),
    (CONVERT(nvarchar(128), NEWID()), N'TdHelpDeskUsers', N'Help Desk Users', N'Help desk staff', N'TicketDeskRole'),
    (CONVERT(nvarchar(128), NEWID()), N'TdInternalUsers', N'Internal Users', N'Users who submit tickets', N'TicketDeskRole'),
    (CONVERT(nvarchar(128), NEWID()), N'TdPendingUsers', N'Pending Approval', N'Users awaiting approval', N'TicketDeskRole');
GO

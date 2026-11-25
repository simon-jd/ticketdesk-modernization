-- Shared sign-in (Round 2b identity): the ASP.NET Core Data Protection key ring both apps read.
-- The new app persists keys here with PersistKeysToDbContext (EF Core's DataProtectionKey: Id, FriendlyName, Xml);
-- the legacy app (Microsoft.Owin.Security.Interop) reads and adds keys through its own small repository
-- (App_Start/SharedAuthenticationConfig.cs). Expand-only: the EF6 model of the legacy app does not map this table.
-- Numbered 0010 to leave 0007-0009 to the rounds running in parallel.
IF OBJECT_ID(N'dbo.DataProtectionKeys', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DataProtectionKeys
    (
        Id           int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_DataProtectionKeys PRIMARY KEY,
        FriendlyName nvarchar(max) NULL,
        Xml          nvarchar(max) NULL
    );
END;

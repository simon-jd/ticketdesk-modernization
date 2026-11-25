-- Lab VM only (SQL Server with SQL logins): the server login of the new app. Run in master by td-db-app-login.sh with the
-- sqlcmd variables TD_APP_PW (from the environment, never on a command line) and TD_APP_DB. Azure SQL uses the app's
-- managed identity instead (no login, no password): see docs/dev/least-privilege-sql.md.
SET NOCOUNT ON;
DECLARE @pw nvarchar(256) = REPLACE(N'$(TD_APP_PW)', N'''', N'''''');
DECLARE @sql nvarchar(max);
IF SUSER_ID(N'td_modern_app') IS NULL
    SET @sql = N'CREATE LOGIN td_modern_app WITH PASSWORD = N''' + @pw + N''', DEFAULT_DATABASE = [$(TD_APP_DB)], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF';
ELSE
    SET @sql = N'ALTER LOGIN td_modern_app WITH PASSWORD = N''' + @pw + N'''';
EXEC (@sql);
PRINT 'login td_modern_app ready';

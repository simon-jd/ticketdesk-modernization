-- td_modern_app_role: what the new app (TicketDesk.Web) may do in the TicketDesk database, and nothing else.
-- Database-level only, so the same script serves the lab VM (member: SQL login td_modern_app) and Azure SQL (member: the
-- app's managed identity, CREATE USER [<identity>] FROM EXTERNAL PROVIDER; no password anywhere). Idempotent.
-- Schema changes are not the app's: td-db migrate runs as a separate, privileged principal at deploy time.
-- Applied by ops/dev/td-db-app-login.sh. Extend this list when a module starts writing a table (and say which one).
IF DATABASE_PRINCIPAL_ID(N'td_modern_app_role') IS NULL CREATE ROLE td_modern_app_role;

-- reads: the domain, identity, settings and notification-settings tables the modules query
GRANT SELECT ON dbo.Projects TO td_modern_app_role;
GRANT SELECT ON dbo.Tickets TO td_modern_app_role;
GRANT SELECT ON dbo.TicketEvents TO td_modern_app_role;
GRANT SELECT ON dbo.TicketEventNotifications TO td_modern_app_role;
GRANT SELECT ON dbo.TicketSubscribers TO td_modern_app_role;
GRANT SELECT ON dbo.TicketTags TO td_modern_app_role;
GRANT SELECT ON dbo.UserSettings TO td_modern_app_role;
GRANT SELECT ON dbo.ApplicationSettings TO td_modern_app_role;
GRANT SELECT ON dbo.IdentityUsers TO td_modern_app_role;
GRANT SELECT ON dbo.IdentityRoles TO td_modern_app_role;
GRANT SELECT ON dbo.IdentityUserRoles TO td_modern_app_role;
GRANT SELECT ON dbo.IdentityUserClaims TO td_modern_app_role;
GRANT SELECT ON dbo.IdentityUserLogins TO td_modern_app_role;
GRANT SELECT ON notifications.ApplicationPushNotificationSettings TO td_modern_app_role;
GRANT SELECT ON notifications.SubscriberPushNotificationSettings TO td_modern_app_role;
GRANT SELECT ON notifications.PushNotificationDestinations TO td_modern_app_role;

-- the shared Data Protection key ring (V0010): read keys, add new ones (keys are never updated or deleted by the app)
GRANT SELECT, INSERT ON dbo.DataProtectionKeys TO td_modern_app_role;

-- writes, waves 3 and 5
GRANT INSERT, UPDATE ON dbo.UserSettings TO td_modern_app_role;                          -- list settings, project selection
GRANT INSERT, DELETE ON dbo.TicketSubscribers TO td_modern_app_role;                     -- subscription toggle
GRANT INSERT, UPDATE, DELETE ON dbo.IdentityUsers TO td_modern_app_role;                 -- register, admin users, profile, password, lockout
GRANT INSERT, DELETE ON dbo.IdentityUserRoles TO td_modern_app_role;
GRANT INSERT, UPDATE, DELETE ON dbo.IdentityUserClaims TO td_modern_app_role;
GRANT INSERT, DELETE ON dbo.IdentityUserLogins TO td_modern_app_role;                    -- Entra ID links
GRANT INSERT, UPDATE, DELETE ON dbo.Projects TO td_modern_app_role;                      -- project settings
GRANT UPDATE ON dbo.Tickets TO td_modern_app_role;                                       -- project delete reassigns tickets
GRANT INSERT, UPDATE ON dbo.ApplicationSettings TO td_modern_app_role;
GRANT INSERT, UPDATE ON notifications.ApplicationPushNotificationSettings TO td_modern_app_role;
GRANT INSERT ON notifications.SubscriberPushNotificationSettings TO td_modern_app_role;
GRANT INSERT, UPDATE ON notifications.PushNotificationDestinations TO td_modern_app_role;

-- writes, wave 4 (ticket writes, tickets round): new tickets, activities, tags, event notifications
GRANT INSERT ON dbo.Tickets TO td_modern_app_role;
GRANT INSERT, UPDATE ON dbo.TicketEvents TO td_modern_app_role;
GRANT INSERT, DELETE ON dbo.TicketTags TO td_modern_app_role;
GRANT INSERT, UPDATE, DELETE ON dbo.TicketEventNotifications TO td_modern_app_role;
GRANT UPDATE ON dbo.TicketSubscribers TO td_modern_app_role;

-- procedures (td-db programmable scripts) the list and report endpoints call
IF OBJECT_ID(N'dbo.usp_TicketList', N'P') IS NOT NULL GRANT EXECUTE ON dbo.usp_TicketList TO td_modern_app_role;
IF OBJECT_ID(N'dbo.usp_Report_AssigneeWorkload', N'P') IS NOT NULL GRANT EXECUTE ON dbo.usp_Report_AssigneeWorkload TO td_modern_app_role;
IF OBJECT_ID(N'dbo.usp_Report_OpenTicketsAging', N'P') IS NOT NULL GRANT EXECUTE ON dbo.usp_Report_OpenTicketsAging TO td_modern_app_role;
IF OBJECT_ID(N'dbo.usp_Report_TimeToClose', N'P') IS NOT NULL GRANT EXECUTE ON dbo.usp_Report_TimeToClose TO td_modern_app_role;

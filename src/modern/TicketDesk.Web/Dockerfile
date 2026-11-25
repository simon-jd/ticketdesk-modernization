# TicketDesk.Web container. Build context is the repository root, because the new app compiles two inputs the legacy side
# owns during coexistence: docs/migration/routes.csv (route inventory of the migration registry) and the legacy .resx strings
# (TicketDesk/TicketDesk.Localization). Dockerfile.dockerignore next to this file limits the context to those and src/modern:
#   npm --prefix src/web ci && npm --prefix src/web run build    (the Angular front end, see below)
#   docker build -f src/modern/TicketDesk.Web/Dockerfile -t ticketdesk-web:dev .
# Runtime: chiseled Ubuntu 24.04 ASP.NET image ("extra" = with ICU and tzdata, which Microsoft.Data.SqlClient and the
# pt-BR/de-DE pages need); no shell, no package manager, runs as the non-root "app" user (UID 1654). Listens on 8080 (with
# Tls__CertificatePath also HTTPS on 8443, ops/k8s/ticketdesk). Configuration comes from the environment, e.g.
# ConnectionStrings__TicketDesk, DataProtection__KeyVaultKeyId, Tls__CertificatePath; the migration registry is
# /app/migration-registry.json (mount a ConfigMap over it to switch routes without a new image).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# project files first so the restore layer is reused while only sources change
COPY src/modern/Directory.Build.props src/modern/Directory.Packages.props src/modern/
COPY src/modern/TicketDesk.Data/TicketDesk.Data.csproj src/modern/TicketDesk.Data/
COPY src/modern/TicketDesk.Identity/TicketDesk.Identity.csproj src/modern/TicketDesk.Identity/
COPY src/modern/TicketDesk.Localization/TicketDesk.Localization.csproj src/modern/TicketDesk.Localization/
COPY src/modern/Modules/TicketDesk.Tickets/TicketDesk.Tickets.csproj src/modern/Modules/TicketDesk.Tickets/
COPY src/modern/Modules/TicketDesk.Projects/TicketDesk.Projects.csproj src/modern/Modules/TicketDesk.Projects/
COPY src/modern/Modules/TicketDesk.Reports/TicketDesk.Reports.csproj src/modern/Modules/TicketDesk.Reports/
COPY src/modern/Modules/TicketDesk.Users/TicketDesk.Users.csproj src/modern/Modules/TicketDesk.Users/
COPY src/modern/Modules/TicketDesk.Attachments/TicketDesk.Attachments.csproj src/modern/Modules/TicketDesk.Attachments/
COPY src/modern/Modules/TicketDesk.Search/TicketDesk.Search.csproj src/modern/Modules/TicketDesk.Search/
COPY src/modern/Modules/TicketDesk.Notifications/TicketDesk.Notifications.csproj src/modern/Modules/TicketDesk.Notifications/
COPY src/modern/Modules/TicketDesk.Settings/TicketDesk.Settings.csproj src/modern/Modules/TicketDesk.Settings/
COPY src/modern/TicketDesk.Web/TicketDesk.Web.csproj src/modern/TicketDesk.Web/
RUN dotnet restore src/modern/TicketDesk.Web/TicketDesk.Web.csproj
COPY docs/migration/routes.csv docs/migration/
COPY TicketDesk/TicketDesk.Localization/ TicketDesk/TicketDesk.Localization/
COPY src/modern/ src/modern/
# The Angular front end is built before the image (no Node.js in this SDK image): npm --prefix src/web ci && npm --prefix
# src/web run build writes src/modern/TicketDesk.Web/wwwroot/app (git-ignored), which the publish copies to /app/wwwroot/app.
RUN test -f src/modern/TicketDesk.Web/wwwroot/app/index.html || { echo "wwwroot/app missing: build src/web first (npm --prefix src/web run build)" >&2; exit 1; }
RUN dotnet publish src/modern/TicketDesk.Web/TicketDesk.Web.csproj -c Release -o /app --no-restore -p:UseAppHost=false -p:TdBuildWeb=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TicketDesk.Web.dll"]

# TicketDesk.Worker container (notification delivery). Build context is the repository root, like TicketDesk.Web: the worker
# compiles the legacy .resx strings (TicketDesk/TicketDesk.Localization) for the e-mail texts.
#   docker build -f src/modern/TicketDesk.Worker/Dockerfile -t ticketdesk-worker:dev .
# Runtime: the same chiseled Ubuntu 24.04 ASP.NET image as the web app (the Tickets module it shares for ticket text
# rendering targets the ASP.NET Core shared framework; "extra" = ICU + tzdata for SqlClient and the culture formats). No
# shell, no package manager, non-root user "app" (UID 1654), no listening port. Configuration from the environment:
# ConnectionStrings__TicketDesk, Notifications__ServiceBus__ConnectionString, Worker__Mail__SmtpHost / __PickupDirectory, ...

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/modern/Directory.Build.props src/modern/Directory.Packages.props src/modern/
COPY src/modern/TicketDesk.Data/TicketDesk.Data.csproj src/modern/TicketDesk.Data/
COPY src/modern/TicketDesk.Identity/TicketDesk.Identity.csproj src/modern/TicketDesk.Identity/
COPY src/modern/TicketDesk.Localization/TicketDesk.Localization.csproj src/modern/TicketDesk.Localization/
COPY src/modern/Modules/TicketDesk.Tickets/TicketDesk.Tickets.csproj src/modern/Modules/TicketDesk.Tickets/
COPY src/modern/Modules/TicketDesk.Users/TicketDesk.Users.csproj src/modern/Modules/TicketDesk.Users/
COPY src/modern/Modules/TicketDesk.Attachments/TicketDesk.Attachments.csproj src/modern/Modules/TicketDesk.Attachments/
COPY src/modern/Modules/TicketDesk.Notifications/TicketDesk.Notifications.csproj src/modern/Modules/TicketDesk.Notifications/
COPY src/modern/Modules/TicketDesk.Settings/TicketDesk.Settings.csproj src/modern/Modules/TicketDesk.Settings/
COPY src/modern/TicketDesk.Worker/TicketDesk.Worker.csproj src/modern/TicketDesk.Worker/
RUN dotnet restore src/modern/TicketDesk.Worker/TicketDesk.Worker.csproj
COPY TicketDesk/TicketDesk.Localization/ TicketDesk/TicketDesk.Localization/
COPY src/modern/ src/modern/
RUN dotnet publish src/modern/TicketDesk.Worker/TicketDesk.Worker.csproj -c Release -o /app --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV DOTNET_ENVIRONMENT=Production
USER $APP_UID
ENTRYPOINT ["dotnet", "TicketDesk.Worker.dll"]

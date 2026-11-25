# td-db container: the schema migrations as a deploy step (Kubernetes Job before every rollout, ops/k8s/ticketdesk).
#   docker build -f src/modern/TicketDesk.Database/Dockerfile -t ticketdesk-db:dev .
# Runtime: chiseled Ubuntu 24.04 .NET runtime image with ICU ("extra": Microsoft.Data.SqlClient needs it), no shell, non-root
# user "app" (UID 1654), no port. The connection string comes from TD_DB_CONN only (secret or, on AKS, "Authentication=
# Active Directory Workload Identity": the job's service account federates with the Azure SQL deploy identity).
#   docker run --rm -e TD_DB_CONN=... ticketdesk-db:dev migrate | status | fulltext --wait 600 | migrate --contract --legacy-retired

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/modern/Directory.Build.props src/modern/Directory.Packages.props src/modern/
COPY src/modern/TicketDesk.Database/TicketDesk.Database.csproj src/modern/TicketDesk.Database/
RUN dotnet restore src/modern/TicketDesk.Database/TicketDesk.Database.csproj
COPY src/modern/TicketDesk.Database/ src/modern/TicketDesk.Database/
RUN dotnet publish src/modern/TicketDesk.Database/TicketDesk.Database.csproj -c Release -o /app --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV DOTNET_ENVIRONMENT=Production
USER $APP_UID
ENTRYPOINT ["dotnet", "td-db.dll"]
CMD ["status"]

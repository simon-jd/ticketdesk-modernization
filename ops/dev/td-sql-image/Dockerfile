# td-sql with SQL Server Full-Text Search: the official SQL Server 2022 image plus the matching mssql-server-fts package
# from packages.microsoft.com (the only two sources; AGENT-RULES "Downloads").
#
#   ops/dev/td-sql.sh build-image      (= docker build -t ticketdesk/td-sql:2022-fts-<version> ops/dev/td-sql-image)
#
# The base image does not register mssql-server with dpkg, so `apt-get install mssql-server-fts` would also install the
# newest mssql-server package over /opt/mssql (an unplanned CU upgrade of every database on the volume). Instead the
# full-text package of exactly the base image's build is downloaded and unpacked (it adds /opt/mssql/lib/sqlservr.fts.sfp);
# FTS_VERSION must equal the base image's ProductVersion, which `td-sql.sh recreate` checks on the running container.
ARG BASE=mcr.microsoft.com/mssql/server@sha256:fc010e5c955453eabc4a24d2f304103e26176fe89796b564f039c45e77a8ebc1
FROM ${BASE}
ARG FTS_VERSION=16.0.4262.2-1
USER root
RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends curl ca-certificates gnupg; \
    curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg; \
    echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-prod.gpg] https://packages.microsoft.com/ubuntu/22.04/mssql-server-2022 jammy main" \
      > /etc/apt/sources.list.d/mssql-server-2022.list; \
    apt-get update; \
    cd /tmp; apt-get download "mssql-server-fts=${FTS_VERSION}"; \
    dpkg-deb -x mssql-server-fts_*.deb /; \
    rm -f mssql-server-fts_*.deb; \
    apt-get purge -y --auto-remove curl gnupg; \
    rm -rf /var/lib/apt/lists/* /etc/apt/sources.list.d/mssql-server-2022.list; \
    test -f /opt/mssql/lib/sqlservr.fts.sfp
USER mssql

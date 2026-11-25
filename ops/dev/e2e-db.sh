#!/usr/bin/env bash
# Fresh copy of td-sql's TicketDesk (baseline-s data, migrated with td-db) for the Playwright suite and the golden replay against
# the new app alone: they create tickets, and the tests that compare with golden recordings read TicketDesk itself. The copy is
# then migrated through the contract stage (td-db migrate --contract --legacy-retired): the new app has needed it since
# r4-retire, while the shared TicketDesk may still be at the expand stage (the coordinator contracts the shared databases).
#
#   ops/dev/e2e-db.sh [DB] [--no-contract]   (default TicketDesk_e2e; dropped and restored from a COPY_ONLY backup of
#                                             TicketDesk; --no-contract: leave the copy as restored, ops/dev/contract-check.sh)
#   TD_COPY_SOURCE=TicketDeskL ops/dev/e2e-db.sh DB ...  copy another database (default TicketDesk)
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
DB="${1:-TicketDesk_e2e}"; CONTRACT=1; [[ "${2:-}" == --no-contract ]] && CONTRACT=0
SRC="${TD_COPY_SOURCE:-TicketDesk}"
[[ "$DB" =~ ^[A-Za-z0-9_]+$ && "$SRC" =~ ^[A-Za-z0-9_]+$ && "$DB" != "$SRC" && "$DB" != TicketDesk && "$DB" != TicketDeskL ]] || { echo "bad database name: $DB" >&2; exit 2; }
"$HERE/td-sql.sh" sql -b -Q "
SET NOCOUNT ON;
DECLARE @bak nvarchar(4000) = CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000)) + N'/$DB-copy.bak';
DECLARE @data nvarchar(4000) = CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000));
IF DB_ID(N'$DB') IS NOT NULL BEGIN ALTER DATABASE [$DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DB]; END;
BACKUP DATABASE [$SRC] TO DISK = @bak WITH COPY_ONLY, INIT;
DECLARE @files TABLE (LogicalName nvarchar(128), PhysicalName nvarchar(260), Type char(1), FileGroupName nvarchar(128), Size numeric(20,0),
  MaxSize numeric(20,0), FileId bigint, CreateLSN numeric(25,0), DropLSN numeric(25,0), UniqueId uniqueidentifier, ReadOnlyLSN numeric(25,0),
  ReadWriteLSN numeric(25,0), BackupSizeInBytes bigint, SourceBlockSize int, FileGroupId int, LogGroupGUID uniqueidentifier,
  DifferentialBaseLSN numeric(25,0), DifferentialBaseGUID uniqueidentifier, IsReadOnly bit, IsPresent bit, TDEThumbprint varbinary(32), SnapshotUrl nvarchar(360));
INSERT @files EXEC ('RESTORE FILELISTONLY FROM DISK = ''' + @bak + '''');
DECLARE @move nvarchar(max) = (SELECT STRING_AGG('MOVE N''' + LogicalName + ''' TO N''' + @data + '/$DB' + '_' + CAST(FileId AS varchar(10)) + CASE Type WHEN 'L' THEN '.ldf' ELSE '.mdf' END + '''', ', ') FROM @files);
EXEC ('RESTORE DATABASE [$DB] FROM DISK = ''' + @bak + ''' WITH ' + @move);
SELECT '$DB restored: ' + CAST((SELECT COUNT(*) FROM [$DB].dbo.Tickets) AS varchar(10)) + ' tickets';
" | grep -v '^$' | tail -1
(( CONTRACT )) || exit 0
. "$TD_W/.tools/env.sh"
dotnet build "$REPO/src/modern/TicketDesk.Database" -c Release -v quiet -nologo >/dev/null
TD_DB_CONN="$("$HERE/td-sql.sh" conn "$DB")" "$REPO/src/modern/TicketDesk.Database/bin/Release/net10.0/td-db" migrate --contract --legacy-retired | tail -1

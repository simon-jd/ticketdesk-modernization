#!/usr/bin/env bash
# the three flaky tests' classes repeated after the fixes, the three loops at the same time (each other's load):
#   MigrationTests x50, RetiredRoutesTests together with ApiTests (the class whose fixture raced it) x50, SearchTests x20
S=$HOME/Desktop/ticketdesk-modernization/scratch-followups
$S/repeat.sh after-class-migration-x50 50 --project src/modern/tests/TicketDesk.Database.Tests --filter-class TicketDesk.Database.Tests.MigrationTests &
$S/repeat.sh after-class-retired-api-x50 50 --project src/modern/tests/TicketDesk.Web.Tests --filter-class TicketDesk.Web.Tests.RetiredRoutesTests --filter-class TicketDesk.Web.Tests.ApiTests &
$S/repeat.sh after-class-search-x20 20 --project src/modern/tests/TicketDesk.Web.Tests --filter-class TicketDesk.Web.Tests.SearchTests &
wait

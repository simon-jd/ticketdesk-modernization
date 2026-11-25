#!/usr/bin/env bash
# azurite.sh - the lab's shared attachment store: Azurite (Azure Blob emulator) on lab-host, used by the legacy app on the
# VM (through qemu's host alias 10.0.2.2) and by the new app on this host. Real Blob storage replaces it on Azure.
#
#   azurite.sh up        start container td-azurite (blob service only) on 127.0.0.1:10000, data in volume td-azurite-data
#   azurite.sh reset     back to the baseline-s state: delete the volume, start empty (baseline-s has no attachments yet)
#   azurite.sh down      stop and remove the container (the volume stays)
#   azurite.sh status    container, image, blob count per container
#   azurite.sh secrets   write $TD_W/.secrets/legacy-filestore.env (connection strings for the VM and for this host)
#   azurite.sh ls        list the blobs of container ticketdesk-attachments (name, size, MD5)
#
# The account is Azurite's built-in devstoreaccount1 (its key is published in the Azurite docs; still kept in .secrets so
# deploy steps treat the connection string like the real one). --skipApiVersionCheck: the legacy app's storage SDK
# (WindowsAzure.Storage 8.2.0) sends x-ms-version 2017-04-17.
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
NAME="${TD_AZURITE_NAME:-td-azurite}"
VOLUME="${TD_AZURITE_VOLUME:-td-azurite-data}"
PORT="${TD_AZURITE_PORT:-10000}"
IMAGE="${TD_AZURITE_IMAGE:-mcr.microsoft.com/azure-storage/azurite:3.37.0}"
CONTAINER_NAME="ticketdesk-attachments"
ACCOUNT=devstoreaccount1
KEY='Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=='
conn() { printf 'DefaultEndpointsProtocol=http;AccountName=%s;AccountKey=%s;BlobEndpoint=http://%s:%s/%s;' "$ACCOUNT" "$KEY" "$1" "$PORT" "$ACCOUNT"; }
log() { echo "$(date -u +%FT%TZ) $*"; }

wait_up() {
  local t0=$SECONDS
  until curl -s -o /dev/null "http://127.0.0.1:$PORT/$ACCOUNT?comp=list"; do
    (( SECONDS - t0 < 60 )) || { docker logs --tail 20 "$NAME" >&2; return 1; }; sleep 1
  done
}
cmd_up() {
  if docker ps --format '{{.Names}}' | grep -qx "$NAME"; then log "$NAME already running"; return; fi
  docker rm -f "$NAME" >/dev/null 2>&1 || true
  docker run -d --name "$NAME" --restart unless-stopped -p "127.0.0.1:$PORT:10000" -v "$VOLUME:/data" "$IMAGE" \
    azurite-blob --blobHost 0.0.0.0 --blobPort 10000 --location /data --skipApiVersionCheck --loose >/dev/null
  wait_up
  log "$NAME up: http://127.0.0.1:$PORT/$ACCOUNT (VM: http://10.0.2.2:$PORT/$ACCOUNT), image $IMAGE"
}
cmd_reset() {
  docker rm -f "$NAME" >/dev/null 2>&1 || true
  docker volume rm "$VOLUME" >/dev/null 2>&1 || true
  cmd_up
  log "reset: empty store (baseline-s state)"
}
cmd_ls() {
  az storage blob list --connection-string "$(conn 127.0.0.1)" -c "$CONTAINER_NAME" --num-results '*' \
    --query '[].[name, properties.contentLength, properties.contentSettings.contentMd5]' -o tsv 2>/dev/null || echo "(no container $CONTAINER_NAME)"
}
cmd_status() {
  docker ps -a --filter "name=^${NAME}$" --format '{{.Names}} {{.Status}} {{.Image}} {{.Ports}}'
  az storage container list --connection-string "$(conn 127.0.0.1)" --query '[].name' -o tsv 2>/dev/null | while read -r c; do
    echo "container $c: $(az storage blob list --connection-string "$(conn 127.0.0.1)" -c "$c" --num-results '*' --query 'length(@)' -o tsv 2>/dev/null) blob(s)"
  done
}
cmd_secrets() {
  local f="$TD_W/.secrets/legacy-filestore.env"
  ( umask 077
    { echo "# Lab attachment store (Azurite on lab-host, ops/onprem/filestore/azurite.sh). Container: $CONTAINER_NAME"
      echo "# legacy app on the VM (qemu host alias 10.0.2.2):"
      echo "TD_AZURE_STORAGE_CONNECTION=$(conn 10.0.2.2)"
      echo "# processes on lab-host (new app, tools):"
      echo "TD_AZURE_STORAGE_CONNECTION_HOST=$(conn 127.0.0.1)"; } > "$f" )
  chmod 600 "$f"; log "wrote $f"
}
case "${1:-}" in
  up) cmd_up ;; reset) cmd_reset ;; down) docker rm -f "$NAME" ;; status) cmd_status ;; secrets) cmd_secrets ;; ls) cmd_ls ;;
  *) sed -n '2,15p' "$0"; exit 2 ;;
esac

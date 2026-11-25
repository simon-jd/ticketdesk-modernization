#!/usr/bin/env bash
# new-lab-cert.sh - lab CA + TLS server certificate for the legacy TicketDesk host (openssl, no root).
#
#   new-lab-cert.sh [DNS names...]      default: localhost tdlegacy01 (plus IP 127.0.0.1)
#
# Writes into $TD_W/.secrets/legacy-lab-ca/ (0700):
#   ca.key ca.crt                 lab root CA (10 years); trust it with curl --cacert ca.crt
#   server.key server.crt         server certificate (825 days), SANs from the arguments
#   server.pfx                    server cert + key + CA chain for Import-PfxCertificate (Set-TdIisSite.ps1)
# and the PFX password into $TD_W/.secrets/legacy-lab-cert.env (TD_CERT_PFX_PASSWORD=...).
# Existing CA material is reused, so re-running only reissues the server certificate.
# TD_CERT_NAME=<name> writes <name>.key/.crt/.pfx instead of server.* (same CA), e.g. azure-lift for the
# Azure VM's certificate (ops/azure/lift.sh cert).
set -euo pipefail
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
D="${TD_CERT_DIR:-$TD_W/.secrets/legacy-lab-ca}"
N="${TD_CERT_NAME:-server}"
ENVF="$TD_W/.secrets/legacy-lab-cert.env"
names=("$@"); [[ ${#names[@]} -gt 0 ]] || names=(localhost tdlegacy01)
umask 077; mkdir -p "$D"; chmod 700 "$D"

if [[ ! -f "$D/ca.key" ]]; then
  openssl req -x509 -newkey rsa:3072 -nodes -keyout "$D/ca.key" -out "$D/ca.crt" -days 3650 -sha256 \
    -subj "/O=TicketDesk Lab/CN=TicketDesk Lab Root CA" \
    -addext "basicConstraints=critical,CA:TRUE" -addext "keyUsage=critical,keyCertSign,cRLSign" 2>/dev/null
  echo "created lab CA $D/ca.crt"
fi
san=""; for n in "${names[@]}"; do san+="DNS:$n,"; done; san+="IP:127.0.0.1"
openssl req -newkey rsa:2048 -nodes -keyout "$D/$N.key" -out "$D/$N.csr" -sha256 \
  -subj "/O=TicketDesk Lab/CN=${names[0]}" 2>/dev/null
openssl x509 -req -in "$D/$N.csr" -CA "$D/ca.crt" -CAkey "$D/ca.key" -CAcreateserial -out "$D/$N.crt" \
  -days 825 -sha256 -extfile <(printf 'subjectAltName=%s\nbasicConstraints=CA:FALSE\nkeyUsage=critical,digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\n' "$san") 2>/dev/null
rm -f "$D/$N.csr"
if [[ ! -f "$ENVF" ]]; then
  printf 'TD_CERT_PFX_PASSWORD=%s\n' "$(head -c 24 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 24)" > "$ENVF"
fi
chmod 600 "$ENVF"
# shellcheck disable=SC1090
. "$ENVF"; export TD_CERT_PFX_PASSWORD
openssl pkcs12 -export -inkey "$D/$N.key" -in "$D/$N.crt" -certfile "$D/ca.crt" -name "TicketDesk lab" \
  -out "$D/$N.pfx" -passout env:TD_CERT_PFX_PASSWORD
chmod 600 "$D"/*
openssl x509 -in "$D/$N.crt" -noout -subject -issuer -enddate -ext subjectAltName
echo "pfx: $D/$N.pfx (password in $ENVF)"

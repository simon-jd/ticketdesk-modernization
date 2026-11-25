#!/usr/bin/env bash
# vmctl.sh - the "on-prem" Windows Server 2025 host for legacy TicketDesk, on plain qemu (no libvirt).
#
#   vmctl.sh install        new disk + unattend media, boot the eval ISO (unattended), in tmux td-legacy-vm
#   vmctl.sh start          boot the installed disk in tmux td-legacy-vm
#   vmctl.sh stop [--force] guest shutdown (ssh, then ACPI); --force pulls the plug after the timeout
#   vmctl.sh status         qemu pid, accelerator, forwards, ssh reachability
#   vmctl.sh ssh [cmd...]   ssh to the guest as Administrator (key auth, PowerShell default shell)
#   vmctl.sh scp SRC DST    scp with the lab key; prefix guest paths with "vm:"
#   vmctl.sh ps < X.ps1     run a PowerShell script read from stdin on the guest (copied, run with -File)
#   vmctl.sh wait-ssh [s]   block until ssh answers (default 7200 s)
#   vmctl.sh screenshot F   PNG of the guest console via the qemu monitor
#   vmctl.sh snapshot NAME  internal qcow2 snapshot (VM must be stopped)
#   vmctl.sh reset NAME     revert the disk to snapshot NAME (VM must be stopped)
#   vmctl.sh snapshots      list snapshots
#   vmctl.sh media          (re)render autounattend.xml and the config ISO only
#
# Paths and sizes come from the environment (defaults below). Secrets are read from
# $TD_W/.secrets (legacy-vm.env, legacy-vm_ed25519) and never written into the repo.
# KVM is used when /dev/kvm can be opened; otherwise qemu falls back to TCG (much slower).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TD_W="${TD_W:-$HOME/Desktop/ticketdesk-modernization}"
VMDIR="${TD_VMDIR:-$TD_W/vm}"
SECRETS="${TD_SECRETS:-$TD_W/.secrets}"
SESSION="${TD_VM_SESSION:-td-legacy-vm}"
DISK="${TD_VM_DISK:-$VMDIR/ws2025-legacy.qcow2}"
DISK_SIZE="${TD_VM_DISK_SIZE:-80G}"
WIN_ISO="${TD_VM_WIN_ISO:-$VMDIR/iso/ws2025-eval-en-us.iso}"
VIRTIO_ISO="${TD_VM_VIRTIO_ISO:-$VMDIR/iso/virtio-win.iso}"
IMAGE_INDEX="${TD_VM_IMAGE_INDEX:-1}"          # 1 = Standard Evaluation, Server Core
COMPUTER_NAME="${TD_VM_NAME:-TDLEGACY01}"
CPUS="${TD_VM_CPUS:-4}"
MEM="${TD_VM_MEM:-8G}"
BIND="${TD_VM_BIND:-127.0.0.1}"
SSH_PORT="${TD_VM_SSH_PORT:-2222}"
HTTP_PORT="${TD_VM_HTTP_PORT:-8080}"
HTTPS_PORT="${TD_VM_HTTPS_PORT:-8443}"
RDP_PORT="${TD_VM_RDP_PORT:-13389}"
SQL_PORT="${TD_VM_SQL_PORT:-11433}"
STOP_TIMEOUT="${TD_VM_STOP_TIMEOUT:-900}"
PYTHON="${TD_PYTHON:-$TD_W/.tools/pyenv/bin/python}"

RUN="$VMDIR/run"
BUILD="$VMDIR/build"                            # rendered media (contains the admin password)
CFG_ISO="$BUILD/td-unattend.iso"
KEY="$SECRETS/legacy-vm_ed25519"
KNOWN="$RUN/known_hosts"

die() { echo "vmctl: $*" >&2; exit 1; }
pid_running() { [[ -f "$RUN/qemu.pid" ]] && kill -0 "$(cat "$RUN/qemu.pid")" 2>/dev/null; }
hmp() { printf '%s\n' "$*" | socat -t 5 - "UNIX-CONNECT:$RUN/monitor.sock" | tr -d '\r' | sed '/^QEMU .*monitor/d;/^(qemu)/d'; }
ssh_opts=(-i "$KEY" -p "$SSH_PORT" -o IdentitiesOnly=yes -o WarnWeakCrypto=no -o BatchMode=yes -o ConnectTimeout=10
          -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile="$KNOWN" -o ServerAliveInterval=30)

kvm_ok() { ( exec 3<>/dev/kvm ) 2>/dev/null; }   # can this user actually open /dev/kvm?
accel_args() {   # KVM by default; TCG (slow, but works without /dev/kvm) as the automatic fallback
  if kvm_ok; then
    echo "-accel kvm -cpu host,hv_relaxed,hv_vapic,hv_spinlocks=0x1fff,hv_time"
  else
    echo "-accel tcg,thread=multi,tb-size=2048 -cpu max"
  fi
}

cmd_media() {
  [[ -f "$SECRETS/legacy-vm.env" && -f "$KEY.pub" ]] || die "missing $SECRETS/legacy-vm.env or $KEY.pub"
  mkdir -p "$BUILD" && chmod 700 "$BUILD"
  local src="$BUILD/media"; rm -rf "$src"; mkdir -p "$src/td-setup"
  # shellcheck disable=SC1091
  ( set -a; . "$SECRETS/legacy-vm.env"; set +a
    TPL="$HERE/autounattend.template.xml" OUT="$src/autounattend.xml" \
    IMAGE_INDEX="$IMAGE_INDEX" COMPUTER_NAME="$COMPUTER_NAME" "$PYTHON" -I -c '
import os, xml.sax.saxutils as x
t = open(os.environ["TPL"], encoding="utf-8").read()
for k in ("ADMIN_PASSWORD", "COMPUTER_NAME", "IMAGE_INDEX"):
    v = os.environ["VM_" + k] if k == "ADMIN_PASSWORD" else os.environ[k]
    t = t.replace("@@%s@@" % k, x.escape(v))
assert "@@" not in t
open(os.environ["OUT"], "w", encoding="utf-8").write(t)' )
  cp "$HERE/firstboot.ps1" "$src/td-setup/firstboot.ps1"
  cp "$KEY.pub" "$src/td-setup/admin_key.pub"
  "$PYTHON" -I "$HERE/build_config_iso.py" "$CFG_ISO" "$src"
  chmod 600 "$CFG_ISO" "$src/autounattend.xml"
  echo "config ISO: $CFG_ISO"
}

cmd_run() {   # foreground qemu; used inside tmux
  local install=0; [[ "${1:-}" == "--install" ]] && install=1
  mkdir -p "$RUN"
  local fwd="hostfwd=tcp:$BIND:$SSH_PORT-:22,hostfwd=tcp:$BIND:$HTTP_PORT-:80,hostfwd=tcp:$BIND:$HTTPS_PORT-:443"
  fwd+=",hostfwd=tcp:$BIND:$RDP_PORT-:3389,hostfwd=tcp:$BIND:$SQL_PORT-:1433"
  # Keep the HPET: without it the Windows Server 2025 (26100) kernel spins forever at the boot logo
  # under TCG (seen 2026-11-07 with -machine q35,hpet=off; booted fine once the HPET was back).
  local args=(
    -name "$SESSION,process=$SESSION"
    -machine q35 $(accel_args)
    -smp "$CPUS,sockets=1,cores=$CPUS,threads=1" -m "$MEM"
    -rtc base=utc
    -drive "file=$DISK,if=none,id=sys,format=qcow2,cache=writeback,discard=unmap,detect-zeroes=unmap"
    -device virtio-blk-pci,drive=sys,bootindex=1
    -netdev "user,id=net0,hostname=tdlegacy01,$fwd"
    -device virtio-net-pci,netdev=net0
    -drive "file=$VIRTIO_ISO,if=none,id=cdvirtio,media=cdrom,readonly=on"
    -device ide-cd,drive=cdvirtio,bus=ide.1
    -usb -device usb-tablet
    -vga std -display none -vnc "unix:$RUN/vnc.sock"
    -monitor "unix:$RUN/monitor.sock,server,nowait"
    -qmp "unix:$RUN/qmp.sock,server,nowait"
    -pidfile "$RUN/qemu.pid"
  )
  if (( install )); then
    args+=(-drive "file=$WIN_ISO,if=none,id=cdwin,media=cdrom,readonly=on" -device ide-cd,drive=cdwin,bus=ide.0,bootindex=2
           -drive "file=$CFG_ISO,if=none,id=cdcfg,media=cdrom,readonly=on" -device ide-cd,drive=cdcfg,bus=ide.2)
  fi
  echo "$(date -u +%FT%TZ) qemu start install=$install accel=[$(accel_args)]" | tee -a "$RUN/qemu.log"
  qemu-system-x86_64 "${args[@]}" 2>&1 | tee -a "$RUN/qemu.log"
  echo "$(date -u +%FT%TZ) qemu exited" | tee -a "$RUN/qemu.log"
}

start_tmux() {
  pid_running && die "already running (pid $(cat "$RUN/qemu.pid"))"
  tmux has-session -t "$SESSION" 2>/dev/null && die "tmux session $SESSION exists"
  mkdir -p "$RUN"
  tmux new-session -d -s "$SESSION" "$HERE/vmctl.sh run $*"
  sleep 3; pid_running || { tail -20 "$RUN/qemu.log"; die "qemu did not start"; }
  echo "started: tmux $SESSION, pid $(cat "$RUN/qemu.pid")"
}

cmd_install() {
  [[ -e "$DISK" && "${1:-}" != "--force" ]] && die "$DISK exists (use --force to overwrite)"
  pid_running && die "VM is running"
  [[ -f "$WIN_ISO" && -f "$VIRTIO_ISO" ]] || die "ISOs missing under $VMDIR/iso"
  cmd_media
  rm -f "$KNOWN"
  qemu-img create -q -f qcow2 -o cluster_size=2M,lazy_refcounts=on "$DISK" "$DISK_SIZE"
  start_tmux --install
}

cmd_start() { [[ -f "$DISK" ]] || die "no disk; run install"; start_tmux; }

cmd_stop() {
  pid_running || { echo "not running"; return 0; }
  local pid; pid="$(cat "$RUN/qemu.pid")"
  if ssh -n "${ssh_opts[@]}" Administrator@127.0.0.1 'Stop-Computer -Force' 2>/dev/null; then
    echo "guest shutdown requested over ssh"
  else
    hmp system_powerdown >/dev/null; echo "ACPI power button sent"
  fi
  local t=0
  while kill -0 "$pid" 2>/dev/null && (( t < STOP_TIMEOUT )); do sleep 5; t=$((t+5)); done
  if kill -0 "$pid" 2>/dev/null; then
    [[ "${1:-}" == "--force" ]] || die "still running after ${STOP_TIMEOUT}s (rerun with --force)"
    hmp quit >/dev/null || kill "$pid"; echo "forced off"
  fi
  echo "stopped after ${t}s"
}

cmd_status() {
  if pid_running; then echo "running pid $(cat "$RUN/qemu.pid") (tmux $SESSION)"; else echo "stopped"; fi
  echo "accel: $(accel_args)"
  echo "forwards ($BIND): ssh $SSH_PORT->22, http $HTTP_PORT->80, https $HTTPS_PORT->443, rdp $RDP_PORT->3389, sql $SQL_PORT->1433"
  if pid_running && ssh -n "${ssh_opts[@]}" Administrator@127.0.0.1 'hostname' 2>/dev/null; then echo "ssh: ok"; else echo "ssh: no answer"; fi
}

cmd_ssh() { exec ssh "${ssh_opts[@]}" Administrator@127.0.0.1 "$@"; }

cmd_scp() {
  local a=() x
  for x in "$@"; do [[ "$x" == vm:* ]] && x="Administrator@127.0.0.1:${x#vm:}"; a+=("$x"); done
  exec scp -i "$KEY" -P "$SSH_PORT" -o IdentitiesOnly=yes -o WarnWeakCrypto=no -o BatchMode=yes \
    -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile="$KNOWN" "${a[@]}"
}

cmd_ps() {   # no quoting through ssh: the script travels as a file
  local tmp name; tmp="$(mktemp --suffix=.ps1)"; name="td-ps-$$-$RANDOM.ps1"
  cat > "$tmp"
  scp -q -i "$KEY" -P "$SSH_PORT" -o IdentitiesOnly=yes -o WarnWeakCrypto=no -o BatchMode=yes \
    -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile="$KNOWN" "$tmp" "Administrator@127.0.0.1:C:/Windows/Temp/$name"
  rm -f "$tmp"
  local rc=0
  ssh -n "${ssh_opts[@]}" Administrator@127.0.0.1 \
    "powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\\Windows\\Temp\\$name; \$c = \$LASTEXITCODE; Remove-Item C:\\Windows\\Temp\\$name; exit \$c" || rc=$?
  return $rc
}

cmd_wait_ssh() {
  local limit="${1:-7200}" t=0
  until ssh -n "${ssh_opts[@]}" Administrator@127.0.0.1 'hostname' >/dev/null 2>&1; do
    pid_running || die "qemu is not running"
    (( t >= limit )) && die "no ssh after ${limit}s"
    sleep 20; t=$((t+20))
  done
  echo "ssh up after ~${t}s"
}

cmd_screenshot() { local f="${1:?file.png}"; hmp "screendump $(realpath -m "$f") -f png" >/dev/null; ls -la "$f"; }

need_stopped() { pid_running && die "stop the VM first" || true; }
cmd_snapshot() { need_stopped; qemu-img snapshot -c "${1:?name}" "$DISK"; qemu-img snapshot -l "$DISK"; }
cmd_reset() { need_stopped; qemu-img snapshot -a "${1:?name}" "$DISK"; echo "disk reverted to ${1}"; }
cmd_snapshots() { qemu-img snapshot -U -l "$DISK"; }   # -U: also works while the VM runs (read-only)

sub="${1:-status}"; shift || true
case "$sub" in
  install) cmd_install "$@";; start) cmd_start;; stop) cmd_stop "$@";; status) cmd_status;;
  run) cmd_run "$@";; media) cmd_media;; ssh) cmd_ssh "$@";; scp) cmd_scp "$@";; ps) cmd_ps;;
  wait-ssh) cmd_wait_ssh "$@";; screenshot) cmd_screenshot "$@";;
  snapshot) cmd_snapshot "$@";; reset) cmd_reset "$@";; snapshots) cmd_snapshots;;
  *) sed -n '2,20p' "$0"; exit 2;;
esac

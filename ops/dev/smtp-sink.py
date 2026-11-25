#!/usr/bin/env python3
"""Lab SMTP sink: accepts every message and writes it to a directory, sends nothing on.

    python3 -I ops/dev/smtp-sink.py --dir DIR [--host 127.0.0.1] [--port 2525] [--headers-only] [--hold-file PATH]

--headers-only keeps only the header block of each message (load drills: many mails, the bodies are not needed).
--hold-file PATH (worker drill): while PATH exists, a message is stored when its DATA ends but the "250" reply is held
back, so the sender is provably inside its send (accepted by the server, not yet acknowledged to it); each held message is
logged ("held <n>") and the reply goes out when PATH is removed.

Both mail senders of the coexistence check use it: the legacy app on the VM (qemu user networking reaches the host's
127.0.0.1 as 10.0.2.2) and TicketDesk.Worker on the host. Each message becomes DIR/<n>.eml with the SMTP envelope in
X-Sink-* headers in front (peer address, MAIL FROM, RCPT TO, receive time). Plain SMTP (no TLS, no AUTH), stdlib only.
"""
import argparse
import asyncio
import datetime
import os

class Sink:
    def __init__(self, directory, headers_only=False, hold_file=None):
        self.dir = directory
        self.headers_only = headers_only
        self.hold_file = hold_file
        self.n = len([f for f in os.listdir(directory) if f.endswith(".eml")])

    async def handle(self, reader, writer):
        peer = writer.get_extra_info("peername")
        mail_from, rcpt = "", []
        def send(line):
            writer.write((line + "\r\n").encode())
        send("220 td-smtp-sink ESMTP")
        await writer.drain()
        try:
            while True:
                raw = await reader.readline()
                if not raw:
                    break
                line = raw.decode("utf-8", "replace").rstrip("\r\n")
                verb = line[:4].upper()
                if verb in ("EHLO", "HELO"):
                    send("250-td-smtp-sink" if verb == "EHLO" else "250 td-smtp-sink")
                    if verb == "EHLO":
                        send("250-8BITMIME")
                        send("250 SIZE 52428800")
                elif verb == "MAIL":
                    mail_from, rcpt = line[10:].strip(), []
                    send("250 OK")
                elif verb == "RCPT":
                    rcpt.append(line[8:].strip())
                    send("250 OK")
                elif verb == "DATA":
                    send("354 end with <CRLF>.<CRLF>")
                    await writer.drain()
                    lines = []
                    while True:
                        l = await reader.readline()
                        if not l or l in (b".\r\n", b".\n"):
                            break
                        lines.append(l[1:] if l.startswith(b"..") else l)
                    self.n += 1
                    path = os.path.join(self.dir, f"{self.n:05d}.eml")
                    head = (f"X-Sink-Peer: {peer[0]}\r\nX-Sink-Mail-From: {mail_from}\r\nX-Sink-Rcpt-To: {', '.join(rcpt)}\r\n"
                            f"X-Sink-Received: {datetime.datetime.now(datetime.timezone.utc).isoformat()}\r\n").encode()
                    body = b"".join(lines)
                    if self.headers_only:
                        body = body.split(b"\r\n\r\n", 1)[0] + b"\r\n\r\n"
                    with open(path + ".tmp", "wb") as f:
                        f.write(head + body)
                    os.replace(path + ".tmp", path)
                    n = self.n
                    if self.hold_file and os.path.exists(self.hold_file):
                        print(f"held {n:05d} {datetime.datetime.now(datetime.timezone.utc).isoformat()}", flush=True)
                        while os.path.exists(self.hold_file):
                            await asyncio.sleep(0.1)
                    send("250 OK queued as %05d" % n)
                elif verb == "RSET":
                    mail_from, rcpt = "", []
                    send("250 OK")
                elif verb == "NOOP":
                    send("250 OK")
                elif verb == "QUIT":
                    send("221 bye")
                    await writer.drain()
                    break
                else:
                    send("502 not implemented")
                await writer.drain()
        finally:
            writer.close()

async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", required=True)
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=2525)
    ap.add_argument("--headers-only", action="store_true")
    ap.add_argument("--hold-file")
    a = ap.parse_args()
    os.makedirs(a.dir, exist_ok=True)
    sink = Sink(a.dir, a.headers_only, a.hold_file)
    server = await asyncio.start_server(sink.handle, a.host, a.port)
    print(f"smtp sink on {a.host}:{a.port} -> {a.dir}", flush=True)
    async with server:
        await server.serve_forever()

if __name__ == "__main__":
    asyncio.run(main())

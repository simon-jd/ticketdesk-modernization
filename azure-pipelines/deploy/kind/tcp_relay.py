#!/usr/bin/env python3
"""tcp_relay.py LISTEN_HOST:PORT=TARGET_HOST:PORT ... - plain TCP relay for the CD kind environment (lab only).

The kind node of cluster td-cd runs on its own Docker network (td-cd-kind); td-sql publishes on 127.0.0.1 only. The relay
listens on that network's gateway address (reachable from containers on that network, not from outside the host) and
forwards to the loopback port, so pods reach td-sql without any change to the shared container. Runs in tmux session
td-pipelines-cd-relay, started and stopped by azure-pipelines/deploy/kind/cd-kind.sh.
"""
import asyncio
import sys


async def pipe(reader, writer):
    try:
        while data := await reader.read(65536):
            writer.write(data)
            await writer.drain()
    except (ConnectionError, asyncio.CancelledError):
        pass
    finally:
        writer.close()


async def serve(listen, target):
    lh, lp = listen.rsplit(":", 1)
    th, tp = target.rsplit(":", 1)

    async def handle(cr, cw):
        try:
            tr, tw = await asyncio.open_connection(th, int(tp))
        except OSError:
            cw.close()
            return
        await asyncio.gather(pipe(cr, tw), pipe(tr, cw))

    server = await asyncio.start_server(handle, lh, int(lp))
    print(f"relay {listen} -> {target}", flush=True)
    async with server:
        await server.serve_forever()


async def main(pairs):
    await asyncio.gather(*(serve(*p.split("=", 1)) for p in pairs))


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    asyncio.run(main(sys.argv[1:]))

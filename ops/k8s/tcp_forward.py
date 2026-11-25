#!/usr/bin/env python3
"""tcp_forward.py LISTEN_HOST:PORT TARGET_HOST:PORT - plain TCP relay (kind lab only).

The kind nodes run on their own Docker network; td-sql and the other shared lab containers publish on 127.0.0.1 only. This
relay listens on that network's gateway address (reachable only from containers on that network) and forwards to the
loopback port, so pods in the kind cluster reach td-sql without changing the shared container. Started and stopped by
ops/k8s/kind.sh (PID file), never by name.
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


async def main(listen, target):
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
    print(f"forwarding {listen} -> {target}", flush=True)
    async with server:
        await server.serve_forever()


if __name__ == "__main__":
    asyncio.run(main(sys.argv[1], sys.argv[2]))

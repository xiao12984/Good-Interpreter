"""Read-only WebSocket route used by the native caption overlay."""

import json
import logging

import aiohttp
from aiohttp import web

from ..services.caption_hub import add_caption_client, remove_caption_client


async def captions_handler(request: web.Request) -> web.WebSocketResponse:
    """Attach a read-only caption client and keep the socket alive."""
    ws = web.WebSocketResponse(heartbeat=30)
    await ws.prepare(request)

    add_caption_client(ws)
    await ws.send_str(json.dumps({"type": "status", "status": "idle"}, ensure_ascii=False))

    try:
        async for msg in ws:
            if msg.type == aiohttp.WSMsgType.ERROR:
                logging.error(f"Caption WebSocket error: {ws.exception()}")
                break
    finally:
        remove_caption_client(ws)

    return ws

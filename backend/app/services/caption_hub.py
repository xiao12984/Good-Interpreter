"""
Caption broadcast hub for the native launcher overlay.

The /ws endpoint remains the audio/control channel for both browser and native
clients. This module only fans out simplified caption events to read-only
caption clients.
"""

import json
import logging

from aiohttp import web


_caption_clients: set[web.WebSocketResponse] = set()


def add_caption_client(ws: web.WebSocketResponse) -> None:
    """Register a native caption overlay WebSocket client."""
    _caption_clients.add(ws)
    logging.info(f"[CAPTION] Client connected, count={len(_caption_clients)}")


def remove_caption_client(ws: web.WebSocketResponse) -> None:
    """Remove a native caption overlay WebSocket client."""
    _caption_clients.discard(ws)
    logging.info(f"[CAPTION] Client disconnected, count={len(_caption_clients)}")


async def broadcast_caption(
    source_text: str = "",
    target_text: str = "",
    source_language: str = "zh",
    target_language: str = "en",
    is_final: bool = False,
    session_id: str = "",
) -> None:
    """Broadcast the current bilingual caption line to all overlay clients."""
    await broadcast_payload(
        {
            "type": "caption",
            "sessionId": session_id,
            "sourceText": source_text,
            "targetText": target_text,
            "sourceLanguage": source_language,
            "targetLanguage": target_language,
            "isFinal": is_final,
        }
    )


async def broadcast_status(status: str, message: str = "") -> None:
    """Broadcast a lightweight service status to all overlay clients."""
    payload = {
        "type": "status",
        "status": status,
    }

    if message:
        payload["message"] = message

    await broadcast_payload(payload)


async def broadcast_payload(payload: dict) -> None:
    """Send a JSON payload to every connected overlay and drop closed clients."""
    if not _caption_clients:
        return

    encoded_payload = json.dumps(payload, ensure_ascii=False)
    disconnected_clients: list[web.WebSocketResponse] = []

    for client in tuple(_caption_clients):
        if client.closed:
            disconnected_clients.append(client)
            continue

        try:
            await client.send_str(encoded_payload)
        except Exception as exc:
            logging.debug(f"Caption broadcast failed: {exc}")
            disconnected_clients.append(client)

    for client in disconnected_clients:
        _caption_clients.discard(client)

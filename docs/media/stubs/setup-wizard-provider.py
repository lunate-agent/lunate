#!/usr/bin/env python3
"""A keyless OpenAI-compatible endpoint for the setup-wizard demo.

The wizard is recorded without a real provider: this server answers model discovery
and one tiny chat completion so the recording shows the connection and model checks
without a key. It listens on 127.0.0.1 only.
"""
from __future__ import annotations

import argparse
import json
import socketserver
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

DEFAULT_MODELS = Path(__file__).resolve().parent.parent / "fixtures" / "setup-wizard-models.json"


class Server(ThreadingHTTPServer):
    def server_bind(self) -> None:
        # Avoid the reverse-DNS lookup in HTTPServer.server_bind: on a cold resolver it can
        # block before the listen socket exists, and the wizard then reports a network failure.
        socketserver.TCPServer.server_bind(self)
        host, port = self.server_address[:2]
        self.server_name = str(host)
        self.server_port = port


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    models_payload = {"object": "list", "data": [{"id": "demo-model", "object": "model"}]}

    def log_message(self, format: str, *args: object) -> None:
        pass

    def _json(self, status: int, payload: dict) -> None:
        raw = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)

    def do_GET(self) -> None:  # noqa: N802 - BaseHTTPRequestHandler API
        if self.path.startswith("/v1/models"):
            self._json(200, Handler.models_payload)
            return
        self._json(404, {"error": {"message": "not found"}})

    def do_POST(self) -> None:  # noqa: N802 - BaseHTTPRequestHandler API
        length = int(self.headers.get("Content-Length", "0"))
        request = json.loads(self.rfile.read(length) or b"{}")
        if not self.path.startswith("/v1/chat/completions"):
            self._json(404, {"error": {"message": "not found"}})
            return

        model = request.get("model", "demo-model")
        if request.get("stream"):
            self.send_response(200)
            self.send_header("Content-Type", "text/event-stream")
            self.send_header("Connection", "close")
            self.end_headers()
            for chunk in (
                {"id": "chatcmpl-demo", "object": "chat.completion.chunk", "created": 0,
                 "model": model, "choices": [{"index": 0, "delta": {"role": "assistant", "content": "ok"}, "finish_reason": None}]},
                {"id": "chatcmpl-demo", "object": "chat.completion.chunk", "created": 0,
                 "model": model, "choices": [{"index": 0, "delta": {}, "finish_reason": "stop"}]},
            ):
                self.wfile.write(("data: " + json.dumps(chunk) + "\n\n").encode())
            self.wfile.write(b"data: [DONE]\n\n")
            self.close_connection = True
            return

        self._json(200, {
            "id": "chatcmpl-demo",
            "object": "chat.completion",
            "created": 0,
            "model": model,
            "choices": [{"index": 0, "message": {"role": "assistant", "content": "ok"}, "finish_reason": "stop"}],
            "usage": {"prompt_tokens": 5, "completion_tokens": 1, "total_tokens": 6},
        })


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=18081)
    parser.add_argument("--models", type=Path, default=DEFAULT_MODELS)
    args = parser.parse_args()
    Handler.models_payload = json.loads(args.models.read_text(encoding="utf-8"))
    Server(("127.0.0.1", args.port), Handler).serve_forever()


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""A keyless OpenAI-compatible endpoint for the setup-wizard demo.

The wizard's provider and connection-test steps are real HTTP: `lunate setup` asks
the endpoint for its model listing and then sends one tiny chat completion. This
server answers both from committed data, so the recording is keyless, offline and
byte-identical on every render.

`fixtures/setup-wizard-models.json` is a recorded discovery response — the body of
`GET https://opencode.ai/inference/go/v1/models` (OpenCode Go), with the real ids
and per-model metadata. To refresh it when the wire changes, re-record the body
(the capture needs a Go key; a render never does):

    curl -sS -H "Authorization: Bearer $OPENCODE_GO_API_KEY" \\
      https://opencode.ai/inference/go/v1/models \\
      > docs/media/fixtures/setup-wizard-models.json

The recording is served as recorded except for one mechanical rewrite: every
`api.<family>.base_url` it declares is repointed at this stub's own origin. The
discovery client refuses a model whose declared API host is foreign to the
configured provider (a real safety rule, see the cross-host fixture in the test
suite), and the recorded host is opencode.ai. Ids, names, capabilities, limits and
families stay exactly as the provider sent them.

Every request is logged to stderr as `<method> <path> <status>` — never a header or
a credential — so a render's provider.log proves which endpoints the recording hit.

It listens on 127.0.0.1 only.
"""
from __future__ import annotations

import argparse
import json
import socketserver
import sys
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
        print(f"stub: {format % args}", file=sys.stderr, flush=True)

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

        model = request.get("model", "demo")
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


def repoint(listing: dict, port: int) -> dict:
    """Repoint every declared per-model API base URL at this stub.

    The recording names opencode.ai; the discovery client refuses a model whose API
    host is foreign to the configured provider, so a local render would fail on the
    recorded host without this rewrite. Nothing else in the response changes.
    """
    origin = f"http://127.0.0.1:{port}/v1"
    for model in listing.get("data", []):
        for apis in (model.get("api") or {}).values():
            if isinstance(apis, dict) and "base_url" in apis:
                apis["base_url"] = origin
    return listing


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=18081)
    parser.add_argument(
        "--models",
        type=Path,
        default=DEFAULT_MODELS,
        help="recorded discovery response served on GET /v1/models",
    )
    args = parser.parse_args()

    Handler.models_payload = repoint(
        json.loads(args.models.read_text(encoding="utf-8")), args.port
    )
    Server(("127.0.0.1", args.port), Handler).serve_forever()


if __name__ == "__main__":
    main()

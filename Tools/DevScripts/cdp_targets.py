#!/usr/bin/env python3
"""List CDP targets on DevTools remote debugging port."""
import json
import urllib.request

with urllib.request.urlopen("http://127.0.0.1:9223/json/list", timeout=5) as r:
    targets = json.load(r)
for t in targets:
    print(f"[{t.get('type')}] {t.get('title')!r} url={t.get('url')!r}")
    print(f"    ws={t.get('webSocketDebuggerUrl')}")

#!/usr/bin/env python3
"""Generic CDP controller for the debug Chrome (port 9222).

Usage:
  cdp_ctl.py list
  cdp_ctl.py new <url>            # open new tab
  cdp_ctl.py nav <substr> <url>   # navigate matching tab
  cdp_ctl.py eval <substr> <js>   # Runtime.evaluate (async result via awaitPromise)
  cdp_ctl.py shot <substr> <out.png>
Match <substr> against page title+url. urllib uses empty ProxyHandler
(system proxy 127.0.0.1:1082 would otherwise intercept localhost).
"""
import base64
import json
import sys
import time
import urllib.request

import websocket

PORT = 9222
OP = urllib.request.build_opener(urllib.request.ProxyHandler({}))
_mid = 0


def targets():
    return [t for t in json.load(OP.open(f"http://127.0.0.1:{PORT}/json/list", timeout=5))
            if t.get("type") == "page"]


def find(substr):
    for t in targets():
        if substr in (t.get("url", "") + t.get("title", "")):
            return t
    raise SystemExit(f"NO_TARGET match={substr}")


def connect(substr):
    t = find(substr)
    return websocket.create_connection(t["webSocketDebuggerUrl"], timeout=30,
                                       suppress_origin=True)


def call(ws, method, params=None, timeout=30):
    global _mid
    _mid += 1
    my_id = _mid
    ws.settimeout(timeout)
    ws.send(json.dumps({"id": my_id, "method": method, "params": params or {}}))
    while True:
        m = json.loads(ws.recv())
        if m.get("id") == my_id:
            if "error" in m:
                raise RuntimeError(m["error"])
            return m.get("result", {})


def main():
    cmd = sys.argv[1]
    if cmd == "list":
        for t in targets():
            print(t.get("title")[:40], "|", t.get("url")[:90])
    elif cmd == "new":
        req = urllib.request.Request(
            f"http://127.0.0.1:{PORT}/json/new?{urllib.parse.quote(sys.argv[2])}",
            method="PUT")
        t = json.load(OP.open(req, timeout=10))
        print(t.get("id"), t.get("url"))
        time.sleep(3)
    elif cmd == "nav":
        ws = connect(sys.argv[2])
        call(ws, "Page.enable")
        call(ws, "Page.navigate", {"url": sys.argv[3]})
        time.sleep(4)
        r = call(ws, "Runtime.evaluate", {"expression": "location.href",
                                          "returnByValue": True})
        print(r["result"]["value"])
    elif cmd == "eval":
        ws = connect(sys.argv[2])
        expr = sys.argv[3]
        r = call(ws, "Runtime.evaluate", {
            "expression": expr, "returnByValue": True,
            "awaitPromise": True}, timeout=60)
        res = r.get("result", {})
        print(json.dumps(res.get("value", res.get("description", "")),
                         ensure_ascii=False)[:3000])
    elif cmd == "shot":
        ws = connect(sys.argv[2])
        r = call(ws, "Page.captureScreenshot", {"format": "png"}, timeout=30)
        with open(sys.argv[3], "wb") as f:
            f.write(base64.b64decode(r["data"]))
        print("saved", sys.argv[3])


if __name__ == "__main__":
    sys.path.insert(0, ".")
    import urllib.parse  # noqa: F401 (used in new)
    main()

#!/usr/bin/env python3
"""Attach to the minigame simulator webview (gamePage.html) via CDP:
collect console messages, optionally evaluate JS, capture a screenshot.

Usage: sim_probe.py [--secs 6] [--eval EXPR] [--out sim_board.png]
"""
import argparse
import base64
import json
import time
import urllib.request

import websocket  # pip websocket-client; suppress_origin=True required by DevTools

PORT = 9223


def find_target():
    import re
    with urllib.request.urlopen(f"http://127.0.0.1:{PORT}/json/list", timeout=5) as r:
        targets = json.load(r)
    pages = [t for t in targets if t.get("type") in ("page", "webview", "iframe")]
    # DevTools 多工程同开：按工程路径映射项目窗口 devid(s0/s1)，只匹配本项目 gamePage
    session = None
    for t in pages:
        if "electron-project" in t.get("url", "") and "PuzzleGameUnity" in t.get("url", ""):
            m = re.search(r"devid=(s\d+)", t.get("url", ""))
            session = m.group(1) if m else None
    for t in pages:  # prefer the game webview of our project session
        if "gamePage" in t.get("url", "") or "gamePage" in t.get("title", ""):
            if session is None or re.search(rf"/{session}/", t.get("url", "")):
                return t
    return pages[0] if pages else None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--secs", type=float, default=6)
    ap.add_argument("--eval", default=None)
    ap.add_argument("--out", default=None)
    ap.add_argument("--reload", action="store_true")
    ap.add_argument("--tap", default=None, help="x,y in CSS px")
    ap.add_argument("--hold", type=float, default=0,
                    help="tap 时按住 N 秒再松开（验证长按）；期间截图到 <out>.hold.png")
    args = ap.parse_args()

    t = find_target()
    if not t:
        print("NO_TARGET")
        return
    print(f"TARGET: {t['title']!r} {t['url']}")

    ws = websocket.create_connection(
        t["webSocketDebuggerUrl"], timeout=10, suppress_origin=True)
    mid = 0

    def send(method, params=None):
        nonlocal mid
        mid += 1
        ws.send(json.dumps({"id": mid, "method": method, "params": params or {}}))
        return mid

    def capture(path):
        i = send("Page.captureScreenshot", {"format": "png"})
        for m in drain(until_id=i):
            data = m.get("result", {}).get("data")
            if data:
                with open(path, "wb") as f:
                    f.write(base64.b64decode(data))
                print(f"SCREENSHOT: {path}")

    def drain(until_id=None, deadline=None):
        msgs = []
        while True:
            try:
                ws.settimeout(max(0.2, deadline - time.time()) if deadline else 2)
                raw = ws.recv()
            except (websocket.WebSocketTimeoutException, TimeoutError,
                    websocket.WebSocketConnectionClosedException):
                break
            if not raw:
                break
            m = json.loads(raw)
            if until_id and m.get("id") == until_id:
                msgs.append(m)
                break
            if not until_id:
                msgs.append(m)
            if deadline and time.time() > deadline:
                break
        return msgs

    send("Runtime.enable")
    send("Log.enable")
    send("Page.enable")

    if args.reload:
        send("Page.reload")
        try:
            ws.close()
        except Exception:
            pass
        time.sleep(3)
        t2 = find_target()
        if not t2 or t2["webSocketDebuggerUrl"] == t["webSocketDebuggerUrl"]:
            # target may keep same ws url; try connect regardless
            t2 = t2 or t
        ws = websocket.create_connection(
            t2["webSocketDebuggerUrl"], timeout=10, suppress_origin=True)
        send("Runtime.enable")
        send("Log.enable")

    end = time.time() + args.secs
    logs = []
    while time.time() < end:
        for m in drain(deadline=end):
            meth = m.get("method", "")
            if meth == "Runtime.consoleAPICalled":
                args_ = m["params"].get("args", [])
                parts = []
                for a in args_:
                    v = a.get("value", a.get("description", ""))
                    parts.append(v if isinstance(v, str) else json.dumps(v))
                text = " ".join(parts)
                logs.append(f"[console.{m['params'].get('type')}] {text}")
            elif meth == "Log.entryAdded":
                e = m["params"]["entry"]
                logs.append(f"[{e.get('level')}] {e.get('text')} {e.get('url', '')}")

    if args.tap:
        x, y = (float(v) for v in args.tap.split(","))

        def dispatch(type_):
            send("Input.dispatchMouseEvent", {
                "type": type_, "x": x, "y": y,
                "button": "left", "clickCount": 1,
                "pointerType": "mouse",
            })

        dispatch("mousePressed")
        if args.hold > 0:
            time.sleep(args.hold)
            if args.out:
                capture(args.out.replace(".png", ".hold.png"))
        dispatch("mouseReleased")
        time.sleep(0.08)
        # give the game a moment to react, then keep draining logs
        deadline = time.time() + 4
        while time.time() < deadline:
            for m in drain(deadline=deadline):
                meth = m.get("method", "")
                if meth == "Runtime.consoleAPICalled":
                    args_ = m["params"].get("args", [])
                    parts = []
                    for a in args_:
                        v = a.get("value", a.get("description", ""))
                        parts.append(v if isinstance(v, str) else json.dumps(v))
                    text = " ".join(parts)
                    logs.append(f"[console.{m['params'].get('type')}] {text}")
                elif meth == "Log.entryAdded":
                    e = m["params"]["entry"]
                    logs.append(f"[{e.get('level')}] {e.get('text')} {e.get('url', '')}")

    if args.eval:
        i = send("Runtime.evaluate", {"expression": args.eval, "returnByValue": True})
        for m in drain(until_id=i):
            if "result" in m:
                print("EVAL:", json.dumps(m["result"].get("result", {}), ensure_ascii=False)[:800])

    if args.out:
        capture(args.out)

    print("--- LOGS ---")
    for l in logs[-60:]:
        print(l)
    if not logs:
        print("(no console output)")
    ws.close()


if __name__ == "__main__":
    main()

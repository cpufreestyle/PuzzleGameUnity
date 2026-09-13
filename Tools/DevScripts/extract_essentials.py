#!/usr/bin/env python3
"""Deterministically extract TMP Essential Resources into Assets/.

AssetDatabase.ImportPackage silently no-ops in -nographics batchmode here, and
TMP_Settings.instance tries to open an importer window when settings are
missing (fails without graphics). So we unpack the .unitypackage (gz+tar)
ourselves, preserving .meta files (and thus GUIDs/references).
"""
import sys
import tarfile
from pathlib import Path

UPKG = Path(sys.argv[1])
ASSETS = Path(sys.argv[2])

with tarfile.open(UPKG, "r:gz") as tar:
    tmp = Path("/Users/a1-6/AI Shared/repo/PuzzleGameUnity/.codely-cli/tmp/essentials_x")
    if tmp.exists():
        import shutil
        shutil.rmtree(tmp)
    tar.extractall(tmp)

count = 0
for entry in sorted(tmp.iterdir()):
    if not entry.is_dir():
        continue
    pname = entry / "pathname"
    if not pname.exists():
        continue
    rel = pname.read_text(encoding="utf-8", errors="replace").splitlines()[0].strip()
    if not rel.startswith("Assets/"):
        print(f"SKIP {rel}")
        continue
    target = ASSETS.parent / rel  # ASSETS is <proj>/Assets
    asset = entry / "asset"
    meta = entry / "asset.meta"
    target.parent.mkdir(parents=True, exist_ok=True)
    if asset.exists():
        (target).write_bytes(asset.read_bytes())
    elif not target.exists():
        target.mkdir(parents=True, exist_ok=True)
    if meta.exists():
        (target.parent / (target.name + ".meta")).write_bytes(meta.read_bytes())
    count += 1
    print(f"OK {rel}")
print(f"imported_entries={count}")

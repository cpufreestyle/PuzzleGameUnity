#!/usr/bin/env python3
"""Strip baked runtime 'piece-X-Y' GameObjects from game.unity.

These 16 root objects were saved into the scene by an editor session
(runtime-created pieces). They are not tracked by Game.pieces, all sit at
origin with dangling sprite refs (photo.jpg is Single-mode now), and must go.
"""
import re
import sys

SCENE = sys.argv[1]
PIECE_NAME = re.compile(r"^  m_Name: piece-\d+-\d+$")

with open(SCENE, "r", encoding="utf-8") as f:
    lines = f.read().split("\n")

# Split into preamble (header tags) + documents
first_doc = next(i for i, l in enumerate(lines) if l.startswith("--- !u!"))
preamble, docs = lines[:first_doc], lines[first_doc:]

# Parse documents: each doc starts with '--- !u!<type> &<id>'
doc_starts = [i for i, l in enumerate(docs) if l.startswith("--- !u!")]
doc_starts.append(len(docs))
parsed = []  # (anchor_id, doc_lines)
for a, b in zip(doc_starts[:-1], doc_starts[1:]):
    parsed.append(docs[a:b])

kill_anchors = set()
for doc in parsed:
    if not doc[0].startswith("--- !u!1 "):
        continue
    for l in doc:
        if PIECE_NAME.match(l):
            # collect component anchors from m_Component block of this GO
            in_comp = False
            for l2 in doc:
                if l2.startswith("  m_Component:"):
                    in_comp = True
                    continue
                if in_comp:
                    m = re.match(r"  - component: \{fileID: (\d+)\}", l2)
                    if m:
                        kill_anchors.add(int(m.group(1)))
                    else:
                        break
            kill_anchors.add(int(re.search(r"&(\d+)", doc[0]).group(1)))
            break

kept, removed = [], 0
for doc in parsed:
    m = re.search(r"&(\d+)", doc[0])
    if m and int(m.group(1)) in kill_anchors:
        removed += 1
        continue
    kept.append(doc)

# Filter SceneRoots entries pointing at removed transforms
for doc in kept:
    if "SceneRoots:" in "\n".join(doc):
        new_body = [
            l for l in doc
            if not (re.match(r"  - \{fileID: (\d+)\}", l)
                    and int(re.match(r"  - \{fileID: (\d+)\}", l).group(1)) in kill_anchors)
        ]
        doc[:] = new_body

out = preamble[:]
for doc in kept:
    out.extend(doc)
with open(SCENE, "w", encoding="utf-8") as f:
    f.write("\n".join(out))

print(f"removed_docs={removed} kill_anchors={len(kill_anchors)}")

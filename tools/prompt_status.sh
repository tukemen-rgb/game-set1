#!/usr/bin/env bash
# プロンプト集に対して、どの画像が揃っていて何が未着かを一覧にする。
# 標準出力に Markdown を書くので docs/prompts/STATUS.md にリダイレクトして使う。
set -euo pipefail
cd "$(dirname "$0")/.."

python3 - <<'PYEOF'
import json
from pathlib import Path

DEST = {
    "BG": "assets/plates", "SKY": "assets/sky", "TEX": "assets/textures/gen",
    "UI": "assets/ui", "DET": "assets/textures/decals", "CHR": "refs/characters", "PROP": "refs/props",
}
EXTS = (".png", ".jpg", ".jpeg", ".webp")

items = json.load(open("docs/prompts/prompts.json", encoding="utf-8"))["items"]

done, todo = [], []
for it in items:
    prefix = it["id"].split("-")[0]
    d = Path(DEST[prefix])
    hit = None
    if d.is_dir():
        for p in sorted(d.iterdir()):
            if p.stem == it["id"] or p.stem.startswith(it["id"] + "_"):
                hit = p
                break
    (done if hit else todo).append((it, hit))

pending = len(list(Path("incoming").glob("*"))) - 1  # README.md を除く
pending = max(pending, 0)

p1 = [(it, hit) for it, hit in done if it.get("phase") == 1]
p1_todo = [(it, hit) for it, hit in todo if it.get("phase") == 1]
n1 = len(p1) + len(p1_todo)

print("# 画像の入荷状況")
print()
print(f"## ★ フェーズ1（まずこれだけ）: **{len(p1)} / {n1}**")
print()
if p1_todo:
    print("残り:")
    for it, _ in p1_todo:
        print(f"- `{it['id']}` — {it['ja']}")
else:
    print("フェーズ1は完了。")
print()
print("## 全体")
print()
print(f"- 揃った: **{len(done)} / {len(items)}**")
print(f"- 未着: {len(todo)}")
print(f"- `incoming/` に未処理: {pending}")
print()
print("`tools/ingest_images.sh` が自動更新する。手で編集しない。")
print()
if done:
    print("## 取り込み済み")
    print()
    for it, p in done:
        print(f"- `{it['id']}` — {it['ja']} → `{p}`")
    print()
print("## 未着（この ID の画像がまだ無い）")
print()
for it, _ in todo:
    ref = " ※下絵の添付が必要" if it["ref"] else ""
    print(f"- `{it['id']}` — {it['ja']}{ref}")
PYEOF

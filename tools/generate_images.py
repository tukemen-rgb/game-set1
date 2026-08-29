#!/usr/bin/env python3
"""未生成のプロンプトを OpenAI 画像APIで生成し incoming/ に置く。

  OPENAI_API_KEY=... python3 tools/generate_images.py

GitHub Actions から1時間ごとに呼ばれる想定。標準ライブラリのみで動く。

環境変数
  OPENAI_API_KEY  必須
  IMAGE_MODEL     既定 gpt-image-2
  IMAGE_QUALITY   既定 medium（low / medium / high）
  MAX_IMAGES      1回の実行で作る枚数。既定 2
  TOTAL_CAP       通算の生成上限。既定 150（暴走時の課金防止）

安全装置
  - incoming/STOP があれば何もせず終了（スマホからでも作れる緊急停止）
  - TOTAL_CAP に達したら生成しない
  - 1枚ごとに docs/prompts/spend_log.json に記録する
"""

import json
import mimetypes
import os
import sys
import urllib.error
import urllib.request
import uuid
from base64 import b64decode
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PROMPTS = ROOT / "docs" / "prompts" / "prompts.json"
LEDGER = ROOT / "docs" / "prompts" / "spend_log.json"
REDO = ROOT / "docs" / "prompts" / "REDO.txt"
INCOMING = ROOT / "incoming"
API = "https://api.openai.com/v1"

# 取り込み先（ingest_images.sh と同じ対応。生成済み判定に使う）
DEST = {
    "BG": "assets/plates", "SKY": "assets/sky", "TEX": "assets/textures/gen",
    "UI": "assets/ui", "DET": "assets/textures/decals",
    "CHR": "refs/characters", "PROP": "refs/props",
}
# 着手順。背景を最優先で片付ける
PRIORITY = ["BG", "SKY", "TEX", "DET", "CHR", "PROP", "UI"]
# アスペクト比 -> API に渡すサイズ（辺は16の倍数、最大辺3840以下）
SIZES = {"16:9": "2048x1152", "1:1": "1024x1024"}


def log(msg):
    print(f"generate: {msg}", flush=True)


def already_done(item_id):
    """assets/ か refs/ に取り込み済み、または incoming/ に投入済みなら True。"""
    dest = ROOT / DEST[item_id.split("-")[0]]
    for d in (dest, INCOMING):
        if not d.is_dir():
            continue
        for p in d.iterdir():
            if p.is_file() and (p.stem == item_id or p.stem.startswith(item_id + "_")):
                return True
    return False


def apply_redo():
    """REDO.txt に書かれた ID の既存画像を退避し、作り直し対象に戻す。

    スマホからでも「この画像だけ作り直して」を指示できるようにするための口。
    退避先は refs/superseded/ なので、消える訳ではない。
    """
    if not REDO.is_file():
        return 0
    moved = 0
    graveyard = ROOT / "refs" / "superseded"
    for raw in REDO.read_text(encoding="utf-8").splitlines():
        item_id = raw.strip()
        if not item_id or item_id.startswith("#"):
            continue
        prefix = item_id.split("-")[0]
        if prefix not in DEST:
            log(f"REDO: 知らないID -> {item_id}")
            continue
        for d in (ROOT / DEST[prefix], INCOMING):
            if not d.is_dir():
                continue
            for f in list(d.iterdir()):
                if f.is_file() and (f.stem == item_id or f.stem.startswith(item_id + "_")):
                    graveyard.mkdir(parents=True, exist_ok=True)
                    f.replace(graveyard / f.name)
                    log(f"REDO: {f.name} を refs/superseded/ へ退避")
                    moved += 1
    REDO.unlink()
    log(f"REDO.txt を適用（{moved} 件退避、ファイルは削除）")
    return moved


def load_ledger():
    if LEDGER.is_file():
        return json.loads(LEDGER.read_text(encoding="utf-8"))
    return {"generated": 0, "failed": 0, "history": []}


def save_ledger(ledger):
    LEDGER.write_text(json.dumps(ledger, ensure_ascii=False, indent=2), encoding="utf-8")


def post_json(path, payload, api_key):
    req = urllib.request.Request(
        f"{API}{path}",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Authorization": f"Bearer {api_key}",
                 "Content-Type": "application/json"},
        method="POST")
    with urllib.request.urlopen(req, timeout=300) as r:
        return json.loads(r.read().decode("utf-8"))


def post_multipart(path, fields, files, api_key):
    """files: [(field_name, filename, bytes)]"""
    boundary = f"----godogen{uuid.uuid4().hex}"
    body = bytearray()
    for k, v in fields.items():
        body += f"--{boundary}\r\n".encode()
        body += f'Content-Disposition: form-data; name="{k}"\r\n\r\n'.encode()
        body += f"{v}\r\n".encode()
    for field, filename, blob in files:
        ctype = mimetypes.guess_type(filename)[0] or "application/octet-stream"
        body += f"--{boundary}\r\n".encode()
        body += (f'Content-Disposition: form-data; name="{field}"; '
                 f'filename="{filename}"\r\n').encode()
        body += f"Content-Type: {ctype}\r\n\r\n".encode()
        body += blob + b"\r\n"
    body += f"--{boundary}--\r\n".encode()

    req = urllib.request.Request(
        f"{API}{path}", data=bytes(body),
        headers={"Authorization": f"Bearer {api_key}",
                 "Content-Type": f"multipart/form-data; boundary={boundary}"},
        method="POST")
    with urllib.request.urlopen(req, timeout=600) as r:
        return json.loads(r.read().decode("utf-8"))


def generate(item, api_key, model, quality):
    """1枚生成して incoming/<ID>.png に書く。成功なら True。"""
    size = SIZES.get(item["aspect"], "1024x1024")
    if item.get("ref"):
        # BG-* は下絵つき（images/edits）。遠近を保つためこれが必須
        plate = ROOT / "docs" / "plates" / item["ref"].rsplit("/", 1)[-1]
        if not plate.is_file():
            log(f"{item['id']}: 下絵が無い ({plate}) -> スキップ")
            return False
        res = post_multipart(
            "/images/edits",
            {"model": model, "prompt": item["prompt"], "size": size,
             "quality": quality, "n": "1"},
            [("image[]", plate.name, plate.read_bytes())],
            api_key)
    else:
        res = post_json("/images/generations", {
            "model": model, "prompt": item["prompt"], "size": size,
            "quality": quality, "n": 1,
        }, api_key)

    data = res.get("data") or []
    if not data or not data[0].get("b64_json"):
        log(f"{item['id']}: 応答に画像が無い -> {str(res)[:200]}")
        return False
    INCOMING.mkdir(exist_ok=True)
    out = INCOMING / f"{item['id']}.png"
    out.write_bytes(b64decode(data[0]["b64_json"]))
    log(f"{item['id']}: 生成 -> {out.name} ({out.stat().st_size // 1024} KB)")
    return True


def main():
    if (INCOMING / "STOP").exists():
        log("incoming/STOP があるので停止（削除すれば再開）")
        return 0

    api_key = os.environ.get("OPENAI_API_KEY", "").strip()
    if not api_key:
        log("OPENAI_API_KEY が未設定。何もせず終了")
        return 0

    model = os.environ.get("IMAGE_MODEL", "gpt-image-2")
    quality = os.environ.get("IMAGE_QUALITY", "medium")
    max_images = int(os.environ.get("MAX_IMAGES", "2"))
    total_cap = int(os.environ.get("TOTAL_CAP", "150"))

    apply_redo()

    ledger = load_ledger()
    if ledger["generated"] >= total_cap:
        log(f"通算 {ledger['generated']} 枚で上限 {total_cap} に到達。停止")
        return 0

    items = json.loads(PROMPTS.read_text(encoding="utf-8"))["items"]
    order = {p: i for i, p in enumerate(PRIORITY)}
    # カテゴリは PRIORITY 順、その中はプロンプト集の並び順
    # （= 場所ごとに 朝→昼→夕→曇→雨上がり→夜。英字順にすると意図と変わる）
    seq = {it["id"]: i for i, it in enumerate(items)}
    pending = [it for it in items if not already_done(it["id"])]
    pending.sort(key=lambda it: (order.get(it["id"].split("-")[0], 99), seq[it["id"]]))

    if not pending:
        log("未生成なし。全部そろっている")
        return 0

    budget = min(max_images, total_cap - ledger["generated"])
    log(f"未生成 {len(pending)} 件 / 今回 {budget} 枚まで / model={model} quality={quality}")

    made = 0
    for item in pending[:budget]:
        try:
            ok = generate(item, api_key, model, quality)
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", "replace")[:300]
            log(f"{item['id']}: HTTP {e.code} -> {detail}")
            ok = False
        except Exception as e:  # ネットワーク断など。1枚失敗しても次へ進む
            log(f"{item['id']}: 失敗 -> {type(e).__name__}: {e}")
            ok = False
        ledger["history"].append({"id": item["id"], "ok": ok, "model": model,
                                  "quality": quality})
        if ok:
            ledger["generated"] += 1
            made += 1
        else:
            ledger["failed"] += 1
        save_ledger(ledger)

    log(f"今回 {made} 枚生成 / 通算 {ledger['generated']} 枚（上限 {total_cap}）")
    return 0


if __name__ == "__main__":
    sys.exit(main())

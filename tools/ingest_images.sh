#!/usr/bin/env bash
# incoming/ に置かれた生成画像を、ID の接頭辞から行き先を判定して取り込む。
#
#   bash tools/ingest_images.sh
#
# 行き先（godot.md の規約: assets/ は実行時に読む物だけ。参考画像は refs/）
#   BG-*   -> assets/plates/        背景プレート（ゲームが描画する）
#   SKY-*  -> assets/sky/           空パノラマ（同上）
#   TEX-*  -> assets/textures/gen/  タイル用テクスチャ（同上）
#   UI-*   -> assets/ui/            UI・キーアート（同上）
#   DET-*  -> assets/textures/decals/ 汚しデカール（同上）
#   CHR-*  -> refs/characters/      3D化の元にする参考画像（実行時は読まない）
#   PROP-* -> refs/props/           同上
set -euo pipefail

cd "$(dirname "$0")/.."
GODOT="${GODOT:-godot}"

shopt -s nullglob nocaseglob
files=(incoming/*.png incoming/*.jpg incoming/*.jpeg incoming/*.webp)
shopt -u nocaseglob

moved=0
unknown=0
for f in "${files[@]}"; do
    base="$(basename "$f")"
    id="${base%%.*}"          # 拡張子を除く
    prefix="${id%%-*}"        # 最初のハイフンまで
    case "$prefix" in
        BG)   dest="assets/plates" ;;
        SKY)  dest="assets/sky" ;;
        TEX)  dest="assets/textures/gen" ;;
        UI)   dest="assets/ui" ;;
        DET)  dest="assets/textures/decals" ;;
        CHR)  dest="refs/characters" ;;
        PROP) dest="refs/props" ;;
        *)
            echo "ingest: 接頭辞が不明なので保留 -> $base"
            unknown=$((unknown + 1))
            continue
            ;;
    esac
    mkdir -p "$dest"
    mv -f "$f" "$dest/$base"
    echo "ingest: $base -> $dest/"
    moved=$((moved + 1))
done

if [ ${#files[@]} -eq 0 ]; then
    echo "ingest: incoming/ に新しい画像なし"
else
    echo "ingest: 取り込み $moved 件 / 保留 $unknown 件"
fi

# Godot に取り込ませる（assets/ 配下に入った物だけが対象になる）
if [ "$moved" -gt 0 ] && command -v "$GODOT" >/dev/null 2>&1; then
    "$GODOT" --headless --path . --import >/dev/null 2>&1 || true
    echo "ingest: godot --import 実行"
fi

bash tools/prompt_status.sh > docs/prompts/STATUS.md
echo "ingest: STATUS.md 更新"

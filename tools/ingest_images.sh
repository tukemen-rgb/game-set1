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

# --- URLS.txt があれば、まず URL から画像を落としてくる ---
# 画像生成AIはバイナリを直接コミットできないが、テキストなら書ける。
# 画像が公開URLに置ける場合は「ID = URL」を書いてもらえば人手が消える。
# ChatGPT のチャット内画像URLは認証付きで外から取れないことが多いので、
# その場合は MAP.txt 方式（社長がドラッグ&ドロップ）にフォールバックする。
urls_file="incoming/URLS.txt"
if [ -f "$urls_file" ]; then
    got=0
    failed=0
    while IFS= read -r line || [ -n "$line" ]; do
        line="${line%$'\r'}"
        case "$line" in ''|'#'*) continue ;; esac
        # 区切りは最初の = 。URL 側にも = が入りうるので ID を左に置く規則
        case "$line" in *"="*) ;; *) continue ;; esac
        id="${line%%=*}"
        url="${line#*=}"
        id="$(printf '%s' "$id" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')"
        url="$(printf '%s' "$url" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e 's/^"//' -e 's/"$//')"
        if [ -z "$id" ] || [ -z "$url" ]; then
            continue
        fi
        # 拡張子は URL のパス部分から拾う（クエリは捨てる）。既定は png
        ext="$(printf '%s' "${url%%\?*}" | sed -n 's/.*\.\([A-Za-z]\{3,4\}\)$/\1/p' | tr 'A-Z' 'a-z')"
        case "$ext" in png|jpg|jpeg|webp) ;; *) ext="png" ;; esac
        out="incoming/${id}.${ext}"
        if curl -fsSL --max-time 120 --max-filesize 20000000 -o "$out" "$url"; then
            # 先頭バイトで本当に画像か確かめる（HTMLのエラーページを掴んでいないか）
            if python3 tools/is_image.py "$out"; then
                echo "ingest: download $id <- $url"
                got=$((got + 1))
            else
                echo "ingest: $id のURLが画像でない（認証ページ？）-> 破棄"
                rm -f "$out"
                failed=$((failed + 1))
            fi
        else
            echo "ingest: $id のダウンロード失敗 -> $url"
            failed=$((failed + 1))
        fi
    done < "$urls_file"
    rm -f "$urls_file"
    echo "ingest: URLS.txt 取得 $got 件 / 失敗 $failed 件（適用後に削除）"
fi

# --- MAP.txt があれば、先にファイル名を ID へ直す ---
# 画像生成AIは「生成画像のバイナリ」を GitHub へ直接置けないが、
# テキストファイルなら直接コミットできる。そこで AI 側に
# 「元のファイル名 = プロンプトID」の対応表だけ書いてもらう。
# こうすると社長は落とした画像を名前を変えずにドラッグ&ドロップするだけでよい。
map_file="incoming/MAP.txt"
if [ -f "$map_file" ]; then
    renamed=0
    while IFS= read -r line || [ -n "$line" ]; do
        line="${line%$'\r'}"
        case "$line" in ''|'#'*) continue ;; esac
        if [[ "$line" == *"->"* ]]; then
            src="${line%%->*}"; dst="${line#*->}"
        elif [[ "$line" == *"="* ]]; then
            src="${line%%=*}"; dst="${line#*=}"
        else
            continue
        fi
        trim() { printf '%s' "$1" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e 's/^"//' -e 's/"$//'; }
        src="$(trim "$src")"
        dst="$(trim "$dst")"
        if [ -z "$src" ] || [ -z "$dst" ]; then
            continue
        fi
        if [ ! -f "incoming/$src" ]; then
            echo "ingest: MAP の $src が incoming/ に無い（未アップロード？）"
            continue
        fi
        ext="${src##*.}"
        dst="${dst%.*}"
        # 社長が既にIDの名前でアップした場合は改名不要（自己移動は mv がエラーになる）
        if [ "$src" = "${dst}.${ext}" ]; then
            echo "ingest: '$src' は既に正しい名前"
            continue
        fi
        mv -f "incoming/$src" "incoming/${dst}.${ext}"
        echo "ingest: rename '$src' -> ${dst}.${ext}"
        renamed=$((renamed + 1))
    done < "$map_file"
    rm -f "$map_file"
    echo "ingest: MAP.txt を適用 $renamed 件（適用後に削除）"
fi

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

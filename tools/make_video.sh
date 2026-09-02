#!/usr/bin/env bash
# キャプチャした連番PNGと frame.wav を1本の動画にする。
#
# Godot のムービーライターは PNG と WAV を別々に吐くので、そのままでは
# 「動いているところ」を人に見せられない。社長が戻ったときに最初に見るのは
# 動画なので、静止画だけで終わらせない。
#
#   bash tools/make_video.sh run docs/video/summer_run.mp4 [開始秒] [長さ秒]
set -eu
cd "$(dirname "$0")/.."

src="screenshots/${1:?撮影フォルダ名}"
out="${2:-docs/video/$1.mp4}"
start="${3:-0}"
dur="${4:-20}"

[ -d "$src" ] || { echo "$src が無い。先に tools/shoot_all.sh $1 を回すこと"; exit 1; }
mkdir -p "$(dirname "$out")"

audio=()
[ -f "$src/frame.wav" ] && audio=(-ss "$start" -i "$src/frame.wav")

ffmpeg -y -loglevel error \
  -framerate 30 -start_number 0 -pattern_type glob -i "$src/frame*.png" \
  "${audio[@]}" \
  -ss "$start" -t "$dur" \
  -c:v libx264 -pix_fmt yuv420p -crf 26 -preset medium \
  -c:a aac -b:a 96k -shortest \
  "$out"

echo "$out  $(du -h "$out" | cut -f1)"

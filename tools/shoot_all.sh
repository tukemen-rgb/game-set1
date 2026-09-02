#!/usr/bin/env bash
# すべての検査キャプチャを一度に回す。
#
# 「検査に載っていない場所は、悪くなっても誰も気づかない」——この夏の開発で
# 三度これに当たった（ゾーンを増やしたのに CamShots は4場面のまま／
# Intro が「つづきから」を撮っていた／花火の時間帯が2秒しか無かった）。
# 一本で全部撮れるようにして、変更のたびに全場面を見られるようにする。
#
#   bash tools/shoot_all.sh          すべて
#   bash tools/shoot_all.sh cams dex 撮る対象を絞る
#
# 出力は screenshots/<名前>/frame*.png。
#
# 全部で 30 分ほどかかる（run / shop / festival が長い）。ふだんは触った所だけ、
# 区切りのときに全部、という使い方をする。
set -u
cd "$(dirname "$0")/.."

export DOTNET_ROOT="${DOTNET_ROOT:-/opt/dotnet}"
GODOT="${GODOT:-godot}"

shoot() {                      # shoot 名前 フレーム数 スクリプト [KEY=VAL ...]
  local name=$1 frames=$2 script=$3; shift 3
  rm -rf "screenshots/$name"; mkdir -p "screenshots/$name"
  echo "--- $name ($frames フレーム)"
  env "$@" xvfb-run -a "$GODOT" --path . \
    --write-movie "screenshots/$name/frame.png" \
    --fixed-fps 30 --quit-after "$frames" --script "$script" \
    >"/tmp/shoot_$name.log" 2>&1
  local n; n=$(ls "screenshots/$name" 2>/dev/null | wc -l)
  echo "    $n 枚"
  [ "$n" -gt 0 ] || echo "    !! 撮れていない: /tmp/shoot_$name.log を見ること"
}

sel=("$@")
has() {                        # 引数が無ければ全部、あれば指定されたものだけ
  [ ${#sel[@]} -eq 0 ] && return 0
  local x; for x in "${sel[@]}"; do [ "$x" = "$1" ] && return 0; done
  return 1
}

has cams    && shoot cams    20   res://test/CamShots.cs   SHOT_HOUR=12.5
has cams_pm && shoot cams_pm 20   res://test/CamShots.cs   SHOT_HOUR=17.2
has rain    && shoot rain    20   res://test/CamShots.cs   SHOT_DAY=8 SHOT_HOUR=11.0
has run     && shoot run     1050 res://test/Presentation.cs
has intro   && shoot intro   320  res://test/Intro.cs      INTRO_FRESH=1
has ending  && shoot ending  260  res://test/Ending.cs
has shop    && shoot shop    820  res://test/Shop.cs
has fishing && shoot fishing 420  res://test/Fishing.cs    FISH_CLOSEUP=1
has dex     && shoot dex     430  res://test/Dex.cs
has radio   && shoot radio   200  res://test/Radio.cs
has talk    && shoot talk    160  res://test/Talk.cs
has festival && shoot festival 620 res://test/Festival.cs
has keyart  && shoot keyart  12   res://test/KeyArt.cs

echo "--- 完了。screenshots/ を見ること"

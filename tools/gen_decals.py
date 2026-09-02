#!/usr/bin/env python3
"""届いた汚しテクスチャを、ゲームで使える形に焼き直す。

生成物（GPT が作った実写風の白黒）は「白地に濃淡」なので、そのまま地面に
貼ると白い面が乗ってしまう。濃淡をアルファに移し替えて、
「影の色 ＋ 濃さのアルファ」の重ね用テクスチャにする。
アルファで持たせておくと、天気や時刻で濃さを動かせる。

  python3 tools/gen_decals.py
"""
import os
from PIL import Image

SRC = "assets/textures/decals"
DST = "assets/textures/gen"


def bake(name, out, rgb, gain, ceiling):
    im = Image.open(f"{SRC}/{name}").convert("L")
    px = im.load()
    w, h = im.size
    dst = Image.new("RGBA", (w, h))
    dp = dst.load()
    for y in range(h):
        for x in range(w):
            # 明るいほど影が薄い。1-明度 を濃さに使う
            a = (1.0 - px[x, y] / 255.0) * gain
            a = min(a, ceiling)
            dp[x, y] = (rgb[0], rgb[1], rgb[2], int(a * 255))
    os.makedirs(DST, exist_ok=True)
    dst.save(f"{DST}/{out}")
    print(f"{DST}/{out}  {w}x{h}")


# 木漏れ日: 地面に落とす葉の影。緑を少し残した暗色
bake("DET-shadow_leaves.webp", "komorebi.png", (38, 44, 30), 1.25, 0.62)
# 雨だれ: 外壁に落とす縦の汚れ。冷たいグレー
bake("DET-rain_streak.webp", "rain_streak.png", (58, 60, 62), 0.95, 0.5)

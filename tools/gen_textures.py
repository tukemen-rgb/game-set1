#!/usr/bin/env python3
"""手元で作れるテクスチャを焼く（外部依存は Pillow だけ）。

  python3 tools/gen_textures.py

- cobble.png: 池のふちの石張り。GPT の参考画（docs/reference/park_pond_gpt.png）の
  ふちは、丸みのある玉石を目地で固めた低い壁なので、不規則な石＋暗い目地で作る。
"""
import math
import random
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parent.parent / "assets" / "textures" / "gen"


def cobble(size=512, seed=3):
    rng = random.Random(seed)
    # 目地の色で塗りつぶし、石を上に置く。端をまたぐ石は反対側にも描いて継ぎ目を消す
    im = Image.new("RGB", (size, size), (78, 72, 66))
    d = ImageDraw.Draw(im)
    cell = 34
    n = size // cell
    stones = []
    for gy in range(n):
        for gx in range(n):
            # 半分ずらした千鳥配置に大きめの揺らぎ。格子に見えないようにする
            cx = gx * cell + cell / 2 + (cell / 2 if gy % 2 else 0) + rng.uniform(-9, 9)
            cy = gy * cell + cell / 2 + rng.uniform(-8, 8)
            rx = cell * rng.uniform(0.46, 0.6)
            ry = cell * rng.uniform(0.4, 0.55)
            tone = 128 + int(40 * rng.uniform(0.0, 1.0))
            col = (tone + rng.randint(-5, 5), tone - 6 + rng.randint(-5, 5), tone - 16 + rng.randint(-6, 4))
            stones.append((cx, cy, rx, ry, col))
    rng.shuffle(stones)
    for cx, cy, rx, ry, col in stones:
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                x, y = cx + ox, cy + oy
                if x + rx < 0 or x - rx > size or y + ry < 0 or y - ry > size:
                    continue
                d.ellipse((x - rx, y - ry, x + rx, y + ry), fill=col)
                # 上面のハイライトと、下側の影で丸みを出す
                hi = tuple(min(255, c + 22) for c in col)
                d.ellipse((x - rx * 0.6, y - ry * 0.75, x + rx * 0.4, y - ry * 0.15), fill=hi)
                lo = tuple(max(0, c - 26) for c in col)
                d.ellipse((x - rx * 0.9, y + ry * 0.35, x + rx * 0.9, y + ry * 1.0), fill=lo)
    im = im.filter(ImageFilter.GaussianBlur(0.9))
    # 細かいざらつき
    px = im.load()
    for y in range(size):
        for x in range(size):
            r, g, b = px[x, y]
            k = rng.randint(-7, 7)
            px[x, y] = (max(0, min(255, r + k)), max(0, min(255, g + k)), max(0, min(255, b + k)))
    return im


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    cobble().save(OUT / "cobble.png")
    print("wrote", OUT / "cobble.png")


if __name__ == "__main__":
    main()

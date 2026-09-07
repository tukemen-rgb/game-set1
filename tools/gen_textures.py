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


def grass(size=1024, seed=11):
    """参考画の芝: 粗い葉が立ち、踏まれた所は土が透ける。均一な緑の絨毯ではない。
    低周波のむら（土）の上に、短い葉の線を何万本も重ねる。端は反対側にも描いて継ぎ目を消す。"""
    rng = random.Random(seed)
    im = Image.new("RGB", (size, size), (78, 84, 34))
    px = im.load()
    # 低周波のむら: 4 つの正弦波の和で土の透け具合を決める
    waves = [(rng.uniform(1, 3), rng.uniform(1, 3), rng.uniform(0, math.tau)) for _ in range(4)]
    for y in range(size):
        for x in range(size):
            v = 0.0
            for fx, fy, ph in waves:
                v += math.sin(math.tau * (fx * x + fy * y) / size + ph)
            v = v / 4.0  # -1..1
            dirt = min(1.0, max(0.0, v - 0.05) * 1.9)   # 0..1 土の透け
            g = (68, 82, 30)
            d = (112, 92, 58)
            px[x, y] = tuple(int(g[i] * (1 - dirt) + d[i] * dirt + rng.randint(-6, 6)) for i in range(3))
    d = ImageDraw.Draw(im)
    for _ in range(90000):
        x = rng.uniform(0, size); y = rng.uniform(0, size)
        L = rng.uniform(5, 14); a = rng.uniform(-0.9, 0.9) - math.pi / 2   # だいたい上向き
        tone = rng.uniform(0, 1)
        col = (int(60 + 80 * tone), int(95 + 90 * tone), int(20 + 40 * tone))
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                d.line((x + ox, y + oy, x + ox + math.cos(a) * L, y + oy + math.sin(a) * L), fill=col, width=1)
    return im.filter(ImageFilter.GaussianBlur(0.4))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    cobble().save(OUT / "cobble.png")
    grass().save(OUT / "grass_coarse.png")
    print("wrote", OUT / "cobble.png", OUT / "grass_coarse.png")


if __name__ == "__main__":
    main()

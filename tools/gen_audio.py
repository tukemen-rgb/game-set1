#!/usr/bin/env python3
"""セミの声と環境音を合成して assets/audio/*.wav に書き出す。

  python3 tools/gen_audio.py

音源を買わず外部依存も足さない方針なので、標準ライブラリだけで合成する。
時間帯ごとに3種類のループを作り、ゲーム側で切り替える。

  cicada_morning  ニイニイゼミ（連続したチー音）＋クマゼミ（シャシャシャ）
  cicada_day      アブラゼミ（ジリジリ）＋ミンミンゼミ（ミーンミンミン）
  cicada_evening  ヒグラシ（カナカナカナ）＋虫の音

どれも 4.0 秒ちょうどで、変調が整数周期で閉じるようにしてある（継ぎ目対策）。
"""

import math
import random
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "assets" / "audio"
RATE = 22050
DUR = 4.0
N = int(RATE * DUR)


def wind_bed(buf, level=0.05):
    """低い帯のノイズ。木々のざわめきと遠い車の代わり。"""
    rng = random.Random(7)
    prev = 0.0
    for i in range(N):
        white = rng.uniform(-1.0, 1.0)
        prev = prev * 0.995 + white * 0.005   # ざっくり低域通過
        buf[i] += prev * level * 8.0


def add_tone(buf, freq, start, dur, amp, mod_hz=0.0, mod_depth=0.0, decay=0.0, seed=0):
    """1匹分の鳴き声を足す。mod_* で細かい震え、decay で減衰。"""
    rng = random.Random(seed)
    i0 = int(start * RATE)
    n = int(dur * RATE)
    detune = 1.0 + rng.uniform(-0.02, 0.02)
    phase = rng.uniform(0, math.tau)
    for k in range(n):
        i = (i0 + k) % N                      # はみ出した分は先頭へ回してループを閉じる
        t = k / RATE
        env = 1.0
        if decay > 0.0:
            env *= math.exp(-t * decay)
        # 立ち上がり・終わりを滑らかにしてプツッという音を防ぐ
        edge = min(1.0, t / 0.01, (dur - t) / 0.01)
        env *= max(0.0, edge)
        m = 1.0
        if mod_hz > 0.0:
            m = 1.0 - mod_depth + mod_depth * (0.5 + 0.5 * math.sin(math.tau * mod_hz * t))
        v = math.sin(math.tau * freq * detune * t + phase)
        v += 0.35 * math.sin(math.tau * freq * 2 * detune * t)   # 倍音でざらつきを出す
        buf[i] += v * amp * env * m


def morning(buf):
    # ニイニイゼミ: 途切れない「チー…」を数匹
    for s in range(4):
        add_tone(buf, 4200 + s * 260, 0.0, DUR, 0.05, mod_hz=28.0, mod_depth=0.35, seed=s)
    # クマゼミ: 「シャシャシャ」を等間隔で
    for s in range(3):
        for k in range(8):
            add_tone(buf, 3000 + s * 180, k * 0.5 + s * 0.06, 0.34, 0.07,
                     mod_hz=16.0, mod_depth=0.8, seed=100 + s * 10 + k)


def day(buf):
    # アブラゼミ: ジリジリと鳴り続ける地の音
    for s in range(5):
        add_tone(buf, 3400 + s * 320, 0.0, DUR, 0.055, mod_hz=44.0, mod_depth=0.5, seed=200 + s)
    # ミンミンゼミ: 「ミーン」のあと「ミンミンミン」
    for rep in range(2):
        base = rep * 2.0
        add_tone(buf, 3650, base + 0.05, 0.62, 0.11, mod_hz=7.0, mod_depth=0.25, seed=300 + rep)
        for k in range(3):
            add_tone(buf, 3650, base + 0.75 + k * 0.26, 0.20, 0.10,
                     mod_hz=9.0, mod_depth=0.3, seed=310 + rep * 5 + k)


def evening(buf):
    # ヒグラシ: 「カナカナカナ」。少しずつ音程が下がるのが特徴
    for rep in range(2):
        base = rep * 2.0
        for k in range(9):
            add_tone(buf, 3000 - k * 55, base + k * 0.155, 0.13, 0.12,
                     decay=9.0, seed=400 + rep * 20 + k)
    # 遠くでもう1匹、半拍ずらして重ねる
    for k in range(9):
        add_tone(buf, 2700 - k * 45, 0.9 + k * 0.16, 0.12, 0.05, decay=9.0, seed=500 + k)
    # 夕方の虫の音（高い連続音）
    for s in range(2):
        add_tone(buf, 6200 + s * 300, 0.0, DUR, 0.018, mod_hz=33.0, mod_depth=0.6, seed=600 + s)


def save(name, fill):
    buf = [0.0] * N
    wind_bed(buf)
    fill(buf)
    peak = max(1e-6, max(abs(v) for v in buf))
    scale = 0.72 / peak                       # 歪まない範囲で揃える
    frames = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, v * scale)) * 32767)) for v in buf)
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / f"{name}.wav"
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(frames)
    print(f"audio: {path.name}  {len(frames) // 2} samples  peak={peak:.3f}")


if __name__ == "__main__":
    save("cicada_morning", morning)
    save("cicada_day", day)
    save("cicada_evening", evening)

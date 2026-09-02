#!/usr/bin/env python3
"""撮影した frame.wav の音量を1秒ごとに測る。

音は目で見えないので、キャプチャを眺めていても穴に気づけない。実際、
測って初めて2つ見つかった:
  - セミの声の入れ替わり（11時・16時）で 8〜10dB の谷（dB を直線で
    クロスフェードしていた）
  - 雨の日が晴れの日より 8dB 静かで、開始直後に -42.7dB まで落ちる

  python3 tools/check_audio.py screenshots/run
  python3 tools/check_audio.py screenshots/rainaudio 20   # 先頭20秒だけ

キャプチャが途中で切れると WAV のヘッダが未確定になるので、
ヘッダを信じずに data チャンク以降を実サイズで読む。
"""
import math
import struct
import sys

path = sys.argv[1] if len(sys.argv) > 1 else "screenshots/run"
limit = int(sys.argv[2]) if len(sys.argv) > 2 else 0
if not path.endswith(".wav"):
    path = path.rstrip("/") + "/frame.wav"

raw = open(path, "rb").read()
off = raw.find(b"data") + 8
SR, CH, SW = 48000, 2, 4          # Godot のムービーライターは 48kHz/32bit/2ch
n = (len(raw) - off) // (CH * SW)
vals = struct.unpack("<%di" % (n * CH), raw[off:off + n * CH * SW])

peak = float(2 ** 31)
step = SR * CH
levels = []
for s in range(n // SR):
    seg = vals[s * step:(s + 1) * step]
    if len(seg) < step:
        break
    rms = math.sqrt(sum((v / peak) ** 2 for v in seg) / len(seg))
    levels.append(20 * math.log10(rms) if rms > 0 else -99.0)
    if limit and len(levels) >= limit:
        break

print(f"{path}  {len(levels)} 秒")
print(" ".join(f"{d:.1f}" for d in levels))
if levels:
    lo, hi = min(levels), max(levels)
    print(f"最小 {lo:.1f} / 最大 {hi:.1f} / 差 {hi - lo:.1f} dB")
    # 前後より 5dB 以上落ちている秒を穴として挙げる
    holes = [i for i in range(1, len(levels) - 1)
             if levels[i] < levels[i - 1] - 5 or levels[i] < levels[i + 1] - 5]
    print("穴の候補: " + (", ".join(f"{i}秒({levels[i]:.1f})" for i in holes) if holes else "なし"))

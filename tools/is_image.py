#!/usr/bin/env python3
"""引数のファイルが PNG / JPEG / WebP なら終了コード 0、それ以外は 1。

ダウンロードしたものが画像ではなく HTML のエラーページや
ログイン画面だった場合を弾くために使う。
"""
import sys
from pathlib import Path

SIGNATURES = (
    (b"\x89PNG\r\n\x1a\n", "png"),
    (b"\xff\xd8\xff", "jpeg"),
)


def main() -> int:
    if len(sys.argv) != 2:
        return 2
    p = Path(sys.argv[1])
    if not p.is_file() or p.stat().st_size < 64:
        return 1
    head = p.read_bytes()[:16]
    for sig, _ in SIGNATURES:
        if head.startswith(sig):
            return 0
    if head[:4] == b"RIFF" and head[8:12] == b"WEBP":
        return 0
    return 1


if __name__ == "__main__":
    raise SystemExit(main())

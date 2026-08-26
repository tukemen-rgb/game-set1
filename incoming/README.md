# incoming — 生成画像の受け取り口

画像生成AIで作った画像は**このフォルダに入れる**。
5分ごとに `tools/ingest_images.sh` が拾って、ID の接頭辞から
正しい場所へ振り分け、Godot に取り込み、`docs/prompts/STATUS.md` を更新する。

## 入れ方

1. `docs/prompts/PROMPTS.md` のプロンプトで画像を作る
2. ファイル名を**プロンプトのIDと同じ**にする（例 `BG-danchi-morning.png`）
3. このフォルダにアップロードして commit する

ファイル名が ID と違うと振り分けできずここに残る。作り直したときは
`BG-danchi-morning_v2.png` のように末尾を足す。

対応拡張子: `.png` `.jpg` `.jpeg` `.webp`

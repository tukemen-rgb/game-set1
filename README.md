# game-set1 — Obstacle Dodge

Godot 4 製のミニゲーム。上から降ってくる障害物を左右移動でかわして、
スコア（生存時間）を伸ばすシンプルな 2D 避けゲーです。

## 遊び方

| 操作 | キー |
| --- | --- |
| 移動 | ← → （または A / D） |
| スタート / リトライ | スペース または Enter |

時間が経つほど障害物の落下速度と生成頻度が上がります。

## 実行方法

1. [Godot Engine 4.3 以降](https://godotengine.org/download/)（無料・オープンソース）を
   **公式サイトから**ダウンロードする
2. Godot を起動 → 「インポート」でこのフォルダの `project.godot` を選ぶ
3. エディタ上部の ▶（実行）を押す

外部アセット・アドオンは一切使っていないので、クローンしてすぐ動きます。

## 構成

```
project.godot        … プロジェクト設定（720x1280 / GL Compatibility）
scenes/main.tscn     … メインシーン（進行管理・UI）
scenes/player.tscn   … プレイヤー（青い三角形）
scenes/obstacle.tscn … 障害物（赤い八角形）
scripts/*.gd         … 各シーンの GDScript
```

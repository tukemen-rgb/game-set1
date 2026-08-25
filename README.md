# game-set1 — いなかの夏（プロトタイプ）

「ぼくのなつやすみ」風の夏休みゲーム。[Godogen](https://github.com/htdt/godogen)
の Godot/Claude パイプライン（`CLAUDE.md` / `godot.md`）に沿って開発している。
昭和の田舎を舞台に、8月1日〜31日をのんびり過ごす。

![screenshot](https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/screenshot.png)

## できていること

- **固定カメラ 4 場面**（庭=斜め見下ろし / 田舎道=ローアングル望遠 /
  川辺=対岸から広角 / 原っぱ=高め俯瞰）。プレイヤー位置で自動切替。
  アングルの根拠は `docs/RESEARCH.md` のカメラ文法
- **日数システム**: 8月1日(金)開始〜8月31日まで。19時に日没→その日の日記→翌朝
- **時刻で変わる空**: 朝→昼→夕焼け（金色経由の補間）。太陽の角度・色も連動
- **セミとり**: 毎日ランダムな木にセミが湧く。近づいてスペースで捕獲（成功率65%）
- **世界**: 漆喰壁と瓦屋根の家・縁側・砂利道・電柱・田んぼ・ひまわり・
  掘り下げた川と土手・入道雲・遠景の山。すべてプロシージャル生成（外部アセットなし）
- **プロシージャルテクスチャ**: `scenes/BuildTextures.cs` がノイズから
  草地・砂利・田んぼ・葉・漆喰・瓦・水面法線マップの PNG を焼き込む。
  空は ProceduralSky のグラデーション（時刻で天頂と地平線の色が変化、太陽つき）
- おまけ: 初代プロトタイプの 2D 避けゲー `scenes/main.tscn`（GDScript）も残置

## まだのもの

- 釣り・ラジオ体操スタンプ・会話などのアクティビティ追加
- AI 生成アセットへの差し替え（水彩背景・キャラモデル。プロンプト集は
  `docs/RESEARCH.md`。API キーを用意して Godogen の asset-gen スキルを導入する）
- 音（セミの声・川のせせらぎ・BGM）
- タイトル画面とセーブ

## アセット表

| アセット | 現状 | 置き換え予定 |
| --- | --- | --- |
| 地形・家・木・ひまわり・電柱・田んぼ | プロシージャル（BuildSummer.cs） | 生成背景・GLBモデル |
| テクスチャ（草・砂利・田・葉・漆喰・瓦・水面法線） | ノイズ焼き込み PNG（BuildTextures.cs） | 生成テクスチャ |
| 主人公（麦わら帽子の男の子） | プリミティブ組み合わせ | Tripo3D リグ付きモデル |
| セミ | 茶色の小箱 | 生成モデル or スプライト |
| UI フォント | SystemFont（Noto Sans CJK ほか） | そのまま |

## 遊び方

| 操作 | キー |
| --- | --- |
| 移動 | 矢印キー |
| 虫あみ / 決定 | スペース / Enter |

## 開発環境と実行

必要なもの: **Godot 4.4 (.NET 版)** と **.NET SDK 8**。

```bash
dotnet build                                # C# コンパイル
godot --headless --path . --script res://scenes/BuildTextures.cs # テクスチャ生成
godot --headless --import                   # 生成 PNG の取り込み
godot --headless --path . --script res://scenes/BuildSummer.cs   # シーン再生成
godot --path .                              # 実行（またはエディタで F5）
```

シーンは手書きせず `scenes/BuildSummer.cs`（ビルド時生成）が
`scenes/summer_main.tscn` を出力する（`godot.md` の規約）。
動作検証は `test/Presentation.cs` ＋ movie writer で行う:

```bash
xvfb-run -a godot --path . --write-movie screenshots/run/frame.png \
  --fixed-fps 30 --quit-after 590 --script res://test/Presentation.cs
```

## 構成

```
CLAUDE.md            … Godogen ランタイムマニフェスト（この環境向け注記つき）
godot.md             … Godogen の Godot エンジンガイド
docs/RESEARCH.md     … 本家の視覚文法の調査＋AI生成用プロンプト集
scenes/BuildSummer.cs … シーンビルダー（summer_main.tscn を生成）
scripts/SummerMain.cs … 進行管理（日数・空・カメラ・セミ・日記）
scripts/PlayerController.cs … プレイヤー移動
test/Presentation.cs  … キャプチャ用スクリプト
```

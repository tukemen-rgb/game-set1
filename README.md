# game-set1 — ニュータウンの夏（プロトタイプ）

**30代のサラリーマンが、2000年ごろの子供時代——ニュータウンの夏休み——に
戻る**ゲーム。[Godogen](https://github.com/htdt/godogen) の Godot/Claude
パイプライン（`CLAUDE.md` / `godot.md`）で開発している。
舞台は千里ニュータウンを参考にした架空の1住区（団地・商店街・公園・空き地）。
「夏休みもの」の文法（有限の31日・固定カメラ・小さな遊び・日記）は使うが、
舞台・年代・意匠はオリジナル（経緯は `docs/RESEARCH.md` コンセプト v2）。

![screenshot](https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/screenshot.png)

## できていること

- **固定カメラ4場面**: 団地の広場（斜め見下ろし45°）/ 商店街（通りの軸で
  望遠20°）/ 公園（池の対岸から広角55°）/ 大通りと空き地（高め俯瞰50°）。
  プレイヤー位置で自動切替
- **日数システム**: 2000年8月1日(火)〜8月31日。19時に日没→日記→翌朝
- **時刻で変わる空**: ProceduralSky の天頂・地平線2色を時刻で補間。
  夕方は金色経由で夕焼けに。太陽・霧・環境光も連動
- **セミとり**: 毎日ランダムな木にセミが湧く。スペースで捕獲（成功率65%）
- **世界（すべてプロシージャル・外部アセットなし）**:
  - 団地: 板状住棟2棟（窓とベランダは生成テクスチャ）・キノコ型給水塔・広場
  - 商店街: 近隣センター風。両側の店（テント庇・看板・ガラス戸とシャッター）、
    半透明アーケード屋根、自販機。大通りからの入口ギャップあり
  - 公園: 池（さざ波の法線マップ＋土手リング）・すべり台・ブランコ・砂場・ベンチ
  - 大通り: アスファルト・歩道タイル・横断歩道・電柱
  - 空き地の土管、遠景の高層住宅スカイラインと丘、入道雲

## まだのもの

- 商店街での買い物・会話、ラジオ体操、日付固定イベント（お祭りなど）
- 「30代が子供に戻る」導入・エンディングの演出
- AI 生成アセットへの差し替え（プロンプト集は `docs/RESEARCH.md`。
  API キーを用意して Godogen の asset-gen スキルを導入する）
- 音（セミの声・商店街の環境音・BGM）、タイトル画面とセーブ

## アセット表

| アセット | 現状 | 置き換え予定 |
| --- | --- | --- |
| 団地・商店街・公園・道路の造形 | プロシージャル（BuildSummer.cs） | 生成背景・GLBモデル |
| テクスチャ（外壁・草・舗装・アスファルト・葉・水面法線ほか） | ノイズ＋矩形描画の焼き込み PNG（BuildTextures.cs） | 生成テクスチャ |
| 主人公（キャップの少年） | プリミティブ組み合わせ | Tripo3D リグ付きモデル |

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
動作検証は `test/Presentation.cs` ＋ movie writer:

```bash
xvfb-run -a godot --path . --write-movie screenshots/run/frame.png \
  --fixed-fps 30 --quit-after 700 --script res://test/Presentation.cs
```

キャプチャの再現性が要るときは `test/SeedProbe.cs` で乱数シードを探し、
`Presentation.cs` の `RngSeed` に渡す。

## 構成

```
CLAUDE.md             … Godogen ランタイムマニフェスト（この環境向け注記つき）
godot.md              … Godogen の Godot エンジンガイド
docs/RESEARCH.md      … コンセプト v2（千里NT調査・プロンプト集）＋ v1 資料
scenes/BuildTextures.cs … プロシージャルテクスチャ焼き込み
scenes/BuildSummer.cs  … シーンビルダー（summer_main.tscn を生成）
scripts/SummerMain.cs  … 進行管理（日数・空・カメラ・セミ・日記）
scripts/PlayerController.cs … プレイヤー移動（池・マップ境界のクランプ）
test/Presentation.cs   … キャプチャ用スクリプト / test/SeedProbe.cs … シード探索
```

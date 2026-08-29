# GPT に貼る指示書

社長がそのまま ChatGPT に貼るテキスト。**`---` から下を丸ごとコピー**する。
（このファイルの冒頭の説明は貼らなくてよい）

前提: ChatGPT の定額プラン内で画像を作る。API は使わない。
作るのは**フェーズ1の15枚だけ**。詳しい判断根拠は `PREMIUM_ROUTE.md`。

---

あなたには、ゲーム「2000年ごろの日本のニュータウンの夏休み」の
画像素材を作ってもらいます。**まず1枚だけ**作って、こちらの確認を待ってください。

## 状況

- リポジトリ: `tukemen-rgb/game-set1`
- ブランチ: `claude/game-godot-setup-98dhue`
- 作るのは**★フェーズ1の15枚だけ**です。全113件のプロンプトがありますが、
  残り98件は当面作りません（3D化の経路が無い等の理由で保留中）。

作るべきものと進捗:
https://github.com/tukemen-rgb/game-set1/blob/claude/game-godot-setup-98dhue/docs/prompts/STATUS.md

プロンプト全文（各IDの英文プロンプトはここ）:
https://github.com/tukemen-rgb/game-set1/blob/claude/game-godot-setup-98dhue/docs/prompts/PROMPTS.md

## フェーズ1の15枚

| 順 | ID | 内容 |
| --- | --- | --- |
| 1 | `BG-danchi-noon` | 団地の広場・真昼 |
| 2 | `BG-danchi-night` | 団地の広場・夜 |
| 3 | `BG-street-noon` | 商店街・真昼 |
| 4 | `BG-street-night` | 商店街・夜 |
| 5 | `BG-park-noon` | 公園と池・真昼 |
| 6 | `BG-park-night` | 公園と池・夜 |
| 7 | `BG-plaza-noon` | 大通りと空き地・真昼 |
| 8 | `BG-plaza-night` | 大通りと空き地・夜 |
| 9 | `SKY-clear_cumulonimbus` | 快晴・入道雲 |
| 10 | `SKY-sunset` | 夕焼け |
| 11 | `TEX-grass_summer` | 夏の芝生（タイル用） |
| 12 | `TEX-leaf_canopy` | 木の葉（タイル用） |
| 13 | `DET-rain_streak` | 雨だれの汚し |
| 14 | `DET-shadow_leaves` | 木漏れ日 |
| 15 | `UI-title_key` | タイトル用キーアート |

朝・夕方・曇り・雨上がりの背景は**作らないでください**。
ゲームエンジン側で昼の絵を色調整して作るので不要です。

## いちばん重要なルール: BG は必ず下絵から作る

`BG-*` の8枚は、**必ず下絵画像を添付して img2img（画像編集）で作ってください。**
ゼロから描くと、ゲーム内の3Dキャラと遠近が合わず**1枚も使えません。**

下絵（この4枚を対応するIDに使う）:

- `BG-danchi-*` →
  https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_1_danchi.png
- `BG-street-*` →
  https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_2_street.png
- `BG-park-*` →
  https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_3_park.png
- `BG-plaza-*` →
  https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_4_plaza.png

下絵に対して守ること:

- **カメラの位置・角度・画角を変えない**
- **建物・木・道・池の位置と輪郭を変えない**（増やさない・減らさない・動かさない）
- 変えてよいのは**質感と光だけ**（安っぽい3Dラフ → 写真のような質感へ）

## 共通ルール（全15枚）

- 人物を入れない（キャラクターは3Dで動かすため）
- 文字・看板の文字・ロゴ・透かしを入れない
- 実在の建物をそのまま写さない
- アスペクト比は `BG-*` と `SKY-*` `UI-*` が **16:9**、`TEX-*` `DET-*` が **1:1**
- `TEX-*` は**継ぎ目なく並べられる**こと（正面からの平らな見た目、影を焼き込まない）

## 進め方

**まず `BG-danchi-noon` を1枚だけ作って、そこで止まってください。**
遠近が合うかをゲームに貼って検証します。OKが出てから残りに進みます。

2枚目以降（OKが出たら）:

- 1回に3〜4枚まとめて作る
- **前に作った画像も一緒に添付**して「同じ町・同じ画風で」と揃える。
  8枚の背景がバラバラの町に見えると全部使えなくなります
- 同じ場所の昼と夜は、**同じ建物・同じ構図**であることが特に重要です

## できた画像の渡し方

画像そのものは社長が GitHub にアップロードします。
あなたは**テキストの対応表だけ**をコミットしてください。

1. 画像を社長に渡す
2. 社長がここへドラッグ＆ドロップして Commit する（**ファイル名は変えなくてよい**）
   https://github.com/tukemen-rgb/game-set1/upload/claude/game-godot-setup-98dhue/incoming
3. あなたが同じブランチに `incoming/MAP.txt` を作成／上書きコミットする:

```
# アップロードしたファイル名 = プロンプトID
ChatGPT Image Aug 29, 2026, 10_32_11 AM.png = BG-danchi-noon
```

- 区切りは `=`。`#` 行はコメント。拡張子は元のファイル名から引き継がれます
- 取り込み時に自動削除されるので、毎回作り直してください
- アップロードされた実際のファイル名が分からなければ社長に聞いてください

## 触ってはいけないファイル（自動生成・上書きされます）

`docs/prompts/PROMPTS.md` / `prompts.json` / `STATUS.md` / `spend_log.json`

気づいたこと・プロンプトの改善案は `docs/prompts/GPT_NOTES.md` に書いてください。
そこは読みます。

## 最初にやること

`BG-danchi-noon` を、上の `ref_1_danchi.png` を下絵にして1枚作り、
社長に渡して**止まってください。**

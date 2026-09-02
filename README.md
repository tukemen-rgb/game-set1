# game-set1 — ニュータウンの夏

**2026年の疲れたサラリーマンが、2000年の夏休みに戻る**ゲーム。
舞台は千里ニュータウンを参考にした架空の1住区（団地・商店街・公園・空き地）。
[Godogen](https://github.com/htdt/godogen) の Godot/Claude パイプライン
（`CLAUDE.md` / `godot.md`）で開発している。

「夏休みもの」の文法（有限の31日・固定カメラ・小さな遊び・日記）は使うが、
舞台・年代・意匠はオリジナル。経緯は `docs/RESEARCH.md`、
自走開発の判断ログは `docs/AUTOPILOT.md`。

![screenshot](https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/screenshot.png)

## できていること

### 物語の骨格

- **導入**: 黒画面から「二〇二六年。会議、見積、終電。」→ 目が覚めると
  セミの声。音だけが先に夏になる構成
- **31日**: 8月1日(火)〜8月31日。1日は8時〜19時（実時間 約3.7分）
- **結末**: 2026年へ戻り、プレイの結果で最後の一行が変わる。
  導入の「セミの こえが していた」で開き、結末の「セミが 鳴いている
  気がした」で閉じる
- **中断と再開**: 毎朝 自動保存。次の起動は「つづきから はじめる。」。
  夏が終わると記録は消え、また8月1日から

### 遊び

- **セミとり**: 6種（ニイニイ / クマ / アブラ / ミンミン / ツクツクボウシ /
  ヒグラシ）。**時間帯で顔ぶれが変わる**ので、時間の経過が遊びに接続する。
  種類ごとに捕獲率が違い、ヒグラシ（夕方のみ・40%）が最難関
- **雨の日の生き物**: カタツムリ / アマガエル。雨の日はセミが出ない代わりに
  この2種だけが出る
- **ザリガニつり**: 公園の池のふちに立ってスペース。**垂らす → 待つ →
  「ツン、と ひいた！」で引く**の3拍。早く引くと逃げる。糸を垂らす間は
  虫あみを竿に持ち替え、ウキは水面に浮く。虫あみが
  「近づいて振る」だけなのに対し、待ってから反射で応える手ざわりにした
- **図鑑**: 9種。日をまたいで残る。未捕獲の個体に近づくと ★ が出る
- **発見**: 世界の8か所で、近づくと一度だけ独白が出る。
  **締切も失敗も無い**（癒し系で残数を突きつけると焦りになるため）
- **駄菓子屋**: 商店街に唯一の人物。おばあさんの一言は日替わり（8種）
- **日記**: 一日の終わりに、その日の出来事が書かれる。達成表にはしない

### 一日と一月の変化

- **時刻**: 空の天頂・地平線の2色を時刻で補間。夕方は金色を経由して夕焼けへ。
  遠景の実写も同じ光の下に置く
- **天気**: 晴れ / くもり / 雨。**日付から決定的に決まる**。
  雨の日はセミが1匹も出ず、雨音と雨粒に変わる
- **行事**: 8/7 ラジオ体操最終日 / 8/13 お盆 / 8/16 送り火 /
  **8/24 夏まつり（夕方に花火）**。祭りの5日前から日記が数え始める

### 見た目と音

- **固定カメラ4場面**: 団地の棟間（斜め）/ 商店街（望遠20°）/
  公園（広角55°）/ 大通り（俯瞰）。プレイヤー位置で自動切替
- **遠景の実写**: 場面ごとに専用の写真を貼り替え、昼と夜をクロスフェード
- **主人公**: 腕・脚・靴・目、歩行アニメ（腕は脚と逆位相・歩きと走りで
  歩調と歩幅が変わる）、
  **虫あみ**を持ち、振ると腕が振りかぶってから前へ抜ける
- **町**: 彫りの深い団地（ベランダ・階段室・布団・室外機・雨どい）、
  棟間の広場（舗装・生垣・駐輪場・ゴミ集積所・物干し）、
  暖簾と平台のある商店街（端の店は袖看板・室外機・雨どい・貼り紙・
  ビールケースで側面を作り込み、東の入口に門柱）、池とすべり台の公園、
  電線、土管
- **音**: セミの声を時間帯で3種クロスフェード、雨音、虫あみの
  振る/捕る/逃がす、花火。**すべて標準ライブラリで合成**（外部依存なし）

## まだのもの

- 商店街での買い物
- 生成アセットの残り（空2・テクスチャ2・汚し2・キーアート1 = 7枚）
- タイトル画面、設定（音量・速度）

## アセット表

| アセット | 現状 | 置き換え予定 |
| --- | --- | --- |
| 遠景（4場所 × 昼夜 = 8枚） | **生成済みの実写を使用中** | — |
| 団地・商店街・公園・道路の造形 | プロシージャル（BuildSummer.cs） | GLBモデル |
| 外壁・舗装・屋上 | Poly Haven CC0 実写＋雨だれ加工 | 生成テクスチャ |
| 草・葉・田・水面法線ほか | ノイズ焼き込み PNG（BuildTextures.cs） | 生成テクスチャ |
| 音（セミ3種・雨・効果音4種） | 標準ライブラリで合成（gen_audio.py） | — |
| 主人公・おばあさん・セミ | プリミティブ組み合わせ | リグ付きモデル |

## 遊び方

| 操作 | キー |
| --- | --- |
| 歩く | 矢印キー |
| 走る | Shift ＋ 矢印キー |
| 虫あみ / 話す / 決定 | スペース / Enter |

既定は**歩き**（2.6 m/s）。急ぐときだけ走る（4.5 m/s）。
のんびり眺める遊びなので、速いほうを既定にしない。

## 開発環境と実行

必要なもの: **Godot 4.4 (.NET 版)** と **.NET SDK 8**。

コンテナを作り直すと Godot は消えるので入れ直す（`.NET SDK` は `/opt/dotnet`）。
**`DOTNET_ROOT` を渡さないと `Failed to load hostfxr` で落ちる**（無言ではないが
原因が分かりにくい）。

```bash
curl -sSL -o /tmp/godot.zip https://github.com/godotengine/godot/releases/download/4.4-stable/Godot_v4.4-stable_mono_linux_x86_64.zip
unzip -q /tmp/godot.zip -d /opt/godot
ln -sf /opt/godot/Godot_v4.4-stable_mono_linux_x86_64/Godot_v4.4-stable_mono_linux.x86_64 /usr/local/bin/godot
export DOTNET_ROOT=/opt/dotnet PATH="$PATH:/opt/dotnet"
```

```bash
dotnet build                                                     # C# コンパイル
godot --headless --path . --script res://scenes/BuildTextures.cs # テクスチャ生成
python3 tools/gen_audio.py                                       # 音の合成
godot --headless --import                                        # 生成物の取り込み
godot --headless --path . --script res://scenes/BuildSummer.cs   # シーン再生成
godot --path .                                                   # 実行
```

シーンは手書きせず `scenes/BuildSummer.cs` が `scenes/summer_main.tscn` を
出力する（`godot.md` の規約）。**取り込み後の `--import` を飛ばすと、
`ResourceLoader.Exists` が false を返して機能が無言で消える**ので注意。

## 検査（キャプチャ）

目視検査用のスクリプトを `test/` に置いている。すべて `SkipIntro` を立てるので、
**検査が人のセーブを壊さない**。

| スクリプト | 見るもの |
| --- | --- |
| `Presentation.cs` | 通しプレイ（決まった動線・乱数固定） |
| `Intro.cs` / `Ending.cs` | 導入 / 結末（結末は最終日から開始） |
| `CloseUp.cs` | 寄り。`CLOSEUP_WALK=1` 歩行 / `CLOSEUP_SWING=1` 虫あみ |
| `CamShots.cs` | 固定カメラ4場面 / `KeyArt.cs` 俯瞰の一枚絵 |
| `Weather.cs` | 天気（`CLOSEUP_DAY` で日付指定） |
| `Festival.cs` | 夏まつりの花火 / `Shop.cs` 駄菓子屋の会話 |
| `SaveCheck.cs` | 中断と再開 / `SeedProbe.cs` 乱数シード探索 |
| `Fishing.cs` | ザリガニつり（当たりの時刻は乱数なので画面の文言で待つ） |
| `SpeedProbe.cs` | 歩き／走りの移動距離の実測（ヘッドレス） |

```bash
xvfb-run -a godot --path . --write-movie screenshots/run/frame.png \
  --fixed-fps 30 --quit-after 700 --script res://test/Presentation.cs
```

## 画像生成の連携

GPT に背景などを作ってもらい、`incoming/` 経由で取り込む仕組みがある。

- `docs/prompts/PROMPTS.md` プロンプト集（113件。うち★15件が優先）
- `docs/prompts/STATUS.md` 入荷状況（自動更新）
- `docs/prompts/GPT_INSTRUCTIONS.md` GPT に貼る指示書
- `tools/ingest_images.sh` 取り込み（`MAP.txt` / `URLS.txt` に対応）
- `.github/workflows/generate-images.yml` API キーがあれば1時間ごとに自動生成

## 構成

```
scenes/BuildSummer.cs    シーンビルダー（summer_main.tscn を生成）
scenes/BuildTextures.cs  プロシージャルテクスチャ
scripts/SummerMain.cs    進行管理（日数・天気・行事・カメラ・音・セーブ）
scripts/PlayerController.cs  移動と歩行アニメ
tools/gen_audio.py       音の合成 / tools/gen_prompts.py プロンプト集の生成
docs/AUTOPILOT.md        自走開発の判断ログ（何をなぜ直したかの記録）
```

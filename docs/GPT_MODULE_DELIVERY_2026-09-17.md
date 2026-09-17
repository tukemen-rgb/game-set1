# 便利屋モジュール3件の納品（2026-09-17）

依頼元: `docs/GPT_CODE_PROTOCOL.md`。基準: Claude側 `2a380a3b561d31e6f6a9f8713783dfa188844da7`。
この納品は独立モジュールと検査コード。既存のゲーム本編への接続・撮影はClaudeの担当。
Unityの制作ブランチと既存のゲームシーンには変更を加えていない。

## ファイル（リンク先に全文）

- [scripts/RequestBook.cs](../scripts/RequestBook.cs)
- [data/requests.json](../data/requests.json)
- [scripts/DayClock.cs](../scripts/DayClock.cs)
- [scripts/Routine.cs](../scripts/Routine.cs)
- [test/BenriyaModules.cs](../test/BenriyaModules.cs)

## 変更点

- 依頼5件の全文・座標・報酬を既存C#のリテラルからそのままJSONへ移し、Godot FileAccess/Jsonで読み込む。
- 8月の時刻・天気・曜日・太陽を独立部品にし、19時と31日で止める。
- 住人の時刻別の目的地切替と毎秒1.2mの移動を独立部品にし、エンジン上で検査する。

## 実測した検証

Godot `4.4.stable.mono.official.4c311cbee` / .NET SDK `8.0.425` / Godot.NET.Sdk `4.4.0` / net8.0。
基準コミットの全33 C#ソースと既存プロジェクト設定に、この納品の4 C#ソースを追加してビルド。

- `dotnet build`: 警告0・エラー0。
- 実Godotのヘッドレス実行: `[requests-test] ok`、`[dayclock] ok`、`[routine-test] ok`。
- `[benriya-modules] ok (55 assertions)`。これに加えてDayClock.SelfTest内で31日すべての朝・雨・曜日・19時停止を検査。
- JSON構文/型不正、必須キー欠落、重複ID、存在しない解禁先、日本語改行、Vector2、任意項目既定値を検査。
- 時刻と移動のフレーム分割、日付巻き戻し時の目的地選択、空の予定、解放済みノード、移動の行き過ぎを検査。
- 不正データの検査で出る `[requests]` 警告は意図した出力。部分的に読み込んだ依頼は返さない。
- ゲームシーンへの接続後の依頼全行程・住人の衝突回避・見た目・撮影は未検証。モデルのUnity実レンダーとは別の検証。

再実行:

```bash
dotnet build
DEBUG_MSG=1 godot --headless --path . --script res://test/BenriyaModules.cs
```

検査は一時的な一意の `user://benriya-module-*.json` だけを書き、終了時に削除する。ゲームのセーブを使わない。

## 組み込みで必要な調整

### RequestBook

JSONルートは配列。キーはRequestDefと同じPascalCase。Kindは Deliver/Fix/Find/Listen の文字列。
位置は `[x,z]`。欠省可能なのはNeedTool（空文字）とUnlocks（0）。
その他の必須キー欠落、明示的nullや不正な型は警告と空リストを返す。
IDは正の一意な整数。ファイル順を保持し、連番を強制しない。未知のキーは無視する。
Godot Jsonの仕様どおり末尾カンマ等は許容される。厳密なRFC JSON検証器とは称していない。

現行BenriyaMainは独自の `Request` / `Kind` と配列を使うため、Load呼び出し1行だけでは接続できない。

1. private enum Kind と private record struct Request、直書き配列を除去。
2. 呼び出し側の型を `RequestDef` / `RequestKind` に統一。
3. シーンの `_Ready` で `IReadOnlyList<RequestDef>` に `RequestBook.Load()` の結果を設定。静的初期化でのI/Oを避ける。
4. `Requests.Length` は `Requests.Count` に変更。
5. `Requests[id - 1]` の各箇所は `Dictionary<int, RequestDef>` などのID索引で参照。追加・並べ替え・欠番で別の依頼を選ばないようにする。
6. 読み込み失敗（Count=0）は依頼なしとして扱い、ID索引はTryGetValueで取得。保持中の古いIDにも対応する。
7. 既存 `test/Benriya.cs` で5依頼と回想往復を検査し、実画面を撮影する。

### DayClock

**既存 `test/Weather.cs` がglobalのWeatherクラスを持っている。** 発注のenum Weatherをglobalに置くと実ビルドでCS0101になる。
既存検査を改名せず、納品のDayClockとWeatherを `GameSet1.Time` 名前空間に置いた。
型名・プロパティ・メソッドの形は発注どおり。呼び出し側では明示的エイリアスを使う:

```csharp
using DayClock = GameSet1.Time.DayClock;
using ClockWeather = GameSet1.Time.Weather;
```

既定値は8/1 8:00、1時間20秒。19:00以降は翌日に勝手に進まない。
NextDayは翌日8:00へ。8/31では何もせず、最終日の時刻も保持する。結末/日記は呼び出し側が担当。
負のdelta、NaN、無限、0以下のSecondsPerHourはArgumentOutOfRangeException。
有限の大きいdeltaは19:00に収める。

発注に開始日時/復元APIはない。現行のStartDay/StartHourや回想復帰には、値を検証した上で新規Clockを作り、
NextDayを開始日-1回呼び、Advance((開始時刻-8)*SecondsPerHour)で合わせる必要がある。
ゲーム中の一時停止はAdvanceを呼ばない。回想をまたぐ保存はDay/Hourの値を保持する。
Weekdayは**2000年8月の曜日**。2026年用HUDにそのまま表示しない。

### Routine

Stop.Pos/Targetは親基準のローカル座標、RotYはラジアン。親は実寸1倍を前提とする。
内部で予定をコピーして時刻順に整列。時刻重複・非有限値は例外。
最初のFromHour以前/予定なしは現在位置で待機する。時刻が戻っても目的地を再選択する。
到着時にRotYを適用し、位置はMoveTowardで行き過ぎない。ログはDEBUG_MSG=1時の目的地切替だけ。

**経路探索や障害物回避は含まない。** 発注どおりPositionを直接動かすため、壁を横切らない目的地/中間Stopを
Claude側で設定する。実際の2000年の地形・歩行経路が確定してから各住人へ設定する。
2026年と回想の住人ノードは別のRoutineインスタンスにする。

## こちらで確かめてほしいこと

- 上記の型とID参照を合わせて依頼5件→道具→原付→写真→回想往復が成立するか。
- 開始日時と回想復帰時の時刻が保持され、2000年の曜日を2026年に誤用していないか。
- 住人が建物・柵・段差を抜けず、実際の歩行可能な経路で動くか。
- JSONを実行ファイルへ書き出す際はエクスポート設定に `data/*.json` を含めること。
- 日課への接続後はゲームの実画面を撮影し、docs/AUTOPILOT.mdへ結果を追記すること。

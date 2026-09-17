# GPT とのコード協業の決まり — システム実装・コード生成

社長の方針（2026-09-17）: **システムの実装やコード生成は GPT と協力する。**
GPT は Godot を動かせないので、役割を分ける。

| 役割 | GPT | Claude（この環境） |
| --- | --- | --- |
| 設計・仕様 | 案を出す／レビューする | 仕様書を書く（境界と受け入れ条件） |
| コード | **独立したモジュールを書く**（C#、ファイル丸ごと） | 組み込む・ビルド・ヘッドレス検査・撮影 |
| 素材 | テクスチャ・遠景・GLB・参考画 | 組み込みと調整 |
| 判断 | 案 A/B を出す | 判断材料を並べる |
| 決定 | — | — （社長） |

## 1. GPT に渡す「発注書」の形（毎回これ）

1. **目的**: 何ができるようになるか（1 行）
2. **境界**: 触ってよいファイル、触ってはいけないファイル、使ってよい API
3. **インターフェース**: 呼び出し側から見た型・メソッド（こちらで固定）
4. **受け入れ条件**: ヘッドレス検査で確認できる形（「DEBUG_MSG=1 で `[msg]` にこれが出る」など）
5. **返し方**: ファイルパスと**ファイル全文**、変更点の要約 3 行、想定した失敗ケース

## 2. GPT から受け取る「納品」の形

```
### ファイル: scripts/Requests.cs（全文）
（コード）
### 変更点
- …
### こちらで確かめてほしいこと
- …
```

複数ファイルは 1 ファイルずつ。差分（patch）ではなく全文。Godot 4.4 / C# 12 / .NET 8。
外部パッケージは使わない。`GD.Print` の検査ログは `[tag]` 付き。

## 3. 守ること（GPT・Claude 共通）

- 遊びの仕様の数値（`docs/HANDOVER_GPT.md` §2）と企画（`docs/CONCEPT_BENRIYA.md`）に従う
- 実在の曲・ロゴ・人物は使わない。有料 API は社長の判断なしに使わない
- 締切・失敗・暴力を足さない
- `scripts/SummerMain.cs`（2000 年の本編）は**回想として動く**ので、GPT は触らない。
  便利屋側は `scripts/BenriyaMain.cs` と、新しく作る独立ファイルだけ

## 4. 最初の発注 3 件（独立していて、並行して出せる）

### 発注 1: 依頼のデータ化 `scripts/RequestBook.cs`

- 目的: いまコードに直書きの依頼 5 件を **JSON**（`data/requests.json`）から読み、
  依頼を増やすのにコードを触らなくてよくする
- 境界: 新規 `scripts/RequestBook.cs` と `data/requests.json`。`BenriyaMain.cs` は
  `RequestBook.Load()` を呼ぶ 1 行だけ変える（こちらでやる）
- インターフェース（固定）:
  ```csharp
  public enum RequestKind { Deliver, Fix, Find, Listen }
  public sealed record RequestDef(int Id, string Client, Vector2 ClientPos, string Title, string Ask,
      RequestKind Kind, Vector2 Target, string TargetLabel, string NeedTool, string Reward, string Done, int Unlocks);
  public static class RequestBook
  {
      public static IReadOnlyList<RequestDef> Load(string path = "res://data/requests.json");
  }
  ```
- 受け入れ条件: `Load()` が 5 件を返し、欠けたキーは既定値（`NeedTool=""`, `Unlocks=0`）。
  壊れた JSON では空リストと `GD.PushWarning`。`DEBUG_MSG=1` で `[requests] 5 件` と出る
- 想定した失敗: 日本語の改行 `\n`、Vector2 の書き方（`[x, z]` の配列にする）

### 発注 2: 日と時間の共通部品 `scripts/DayClock.cs`

- 目的: 2000 年（SummerMain）と 2026 年（BenriyaMain）で別々に書いている
  「時刻・日付・天気・太陽の向き」を 1 つの部品にする
- 境界: 新規 `scripts/DayClock.cs` のみ。既存 2 本の置き換えはこちらで段階的にやる
- インターフェース（固定）:
  ```csharp
  public enum Weather { Sunny, Cloudy, Rainy }
  public sealed class DayClock
  {
      public int Day { get; }            // 1..31
      public double Hour { get; }        // 8.0..19.0
      public double SecondsPerHour { get; set; }
      public Weather Today { get; }      // (day*37+11)%100: <18 雨, <40 くもり。8/16 は 17 日と入れ替え
      public void Advance(double delta); // 19.0 で止まる（EndDay はこちらが呼ぶ）
      public void NextDay();
      public Vector3 SunRotationDegrees { get; }   // (-10-50*arc, -150+120*t, 0)
      public float Warm { get; }         // 夕方の暖色 0..1（14 時から 19 時で 1、指数 1.3）
      public string Weekday { get; }     // 2000 年 8 月の曜日（8/1 は火）
  }
  ```
- 受け入れ条件: `Today` が 8/8・17・19・27 で Rainy、8/16 で Sunny。`Weekday` が 8/1 火・8/31 木。
  ヘッドレスの検査用に `public static void SelfTest()` を付け、`[dayclock] ok` と出る

### 発注 3: 住人の日課 `scripts/Routine.cs`

- 目的: 住人が時刻で居場所を変える（管理人は 8 時は広場、12 時は管理室、17 時は掲示板前）。
  今は全員が一日中同じ場所に立っている
- 境界: 新規 `scripts/Routine.cs`。住人ノードは `Node3D`、移動は瞬間移動ではなく
  `Position` を毎フレーム目標へ 1.2 m/s で寄せる
- インターフェース（固定）:
  ```csharp
  public sealed record Stop(double FromHour, Vector3 Pos, float RotY);
  public sealed class Routine
  {
      public Routine(Node3D body, IReadOnlyList<Stop> stops);
      public void Update(double hour, double delta);   // いまの時刻の Stop へ歩く
      public Vector3 Target { get; }
  }
  ```
- 受け入れ条件: 時刻を 8→19 に回す検査で、各 Stop の時刻に `Target` が切り替わり、
  `Position` が滑らかに追従する。`[routine] <name> → <stop>` のログ

## 5. 進め方

1. 社長が §4 の発注 1 を GPT に貼る（下の文をそのまま）。
2. GPT の納品（ファイル全文）を社長がこのリポジトリに上げる（`scripts/` と `data/`）。
3. Claude がビルド・検査・撮影して結果を `docs/AUTOPILOT.md` に書く。直しが要れば
   発注書の形で GPT に戻す。

## 6. ChatGPT に貼る文（発注 1）

```
Godot 4.4（C# 12 / .NET 8）のゲームで、依頼データを JSON から読むモジュールを書いてください。
外部パッケージは使わないでください。ファイルは全文で返してください（差分は不可）。

作るもの:
1. scripts/RequestBook.cs — 下のインターフェースを固定で実装する
2. data/requests.json — 下の 5 件のデータ

インターフェース（変更不可）:
public enum RequestKind { Deliver, Fix, Find, Listen }
public sealed record RequestDef(int Id, string Client, Vector2 ClientPos, string Title, string Ask,
    RequestKind Kind, Vector2 Target, string TargetLabel, string NeedTool, string Reward, string Done, int Unlocks);
public static class RequestBook {
    public static IReadOnlyList<RequestDef> Load(string path = "res://data/requests.json");
}

要件:
- Godot の FileAccess と Json クラスで読む。Vector2 は JSON では [x, z] の配列。
- 欠けたキーは既定値（NeedTool="", Unlocks=0）。壊れた JSON は空リストを返し GD.PushWarning。
- 環境変数 DEBUG_MSG=1 のとき GD.Print("[requests] N 件")。
- 文中の改行は JSON の \n をそのまま使う。

5 件のデータ（Id, Client, ClientPos, Title, Ask, Kind, Target, TargetLabel, NeedTool, Reward, Done, Unlocks）:
1, 管理人, [-6,-4], 回覧板を 3号棟へ, 「回覧板、3号棟の 佐々木さんに 届けてくれんか。\nわしは 膝が もう だめでな」, Deliver, [-18.6,-1.1], 3号棟の 佐々木さん, "", 工具箱, 「助かった。これ、亡くなった 前の 管理人の 工具箱だ。\n使って やってくれ」, 2
2, 佐々木さん, [-18.6,-1.1], 物干しの 修理, 「ベランダの 物干しが 傾いてね。\n工具が あれば 直せると 思うんだけど」, Fix, [-25,-3.4], 3号棟の 物干し場, 工具箱, 台車, 「まあ、まっすぐに なった。\n台車、うちの 人のだけど もう 使わないから」, 3
3, 米屋の 田中さん, [-8,13.5], 米 20kg を 3号棟へ, 「米を 20 キロ、3号棟の 階段の 下まで。\n昔は わしが 担いだが、もう 無理だ」, Deliver, [-16,-9.5], 3号棟の 階段の 下, 台車, 原付, 「おう、ご苦労。裏の カブ、もう 乗らんから 持ってけ。\n隣の 団地まで 行ける」, 5
4, ベンチの おじいさん, [-1.2,15.3], 落とした 写真, 「公園の 池の ところで、古い 写真を 落としてなあ。\n2000年の 夏の。孫が 写っとる」, Find, [13.5,-15], 公園の 池の ふち, "", 古い写真, 「これだ、これだ。……あんた、もしかして この 子か？」, 0
5, 管理人, [-6,-4], 隣の 団地へ 荷物, 「隣の 団地の バス停まで、この 段ボールを。\n歩きじゃ 遠い。乗り物が あるなら 頼む」, Deliver, [36,8.5], 隣の 団地の バス停, 原付, 鍵束, 「ありがとう。……これ、空き部屋の 鍵束だ。\n次の 依頼は、また 明日」, 0

返し方: 「### ファイル: パス（全文）」「### 変更点」「### こちらで確かめてほしいこと」の3節で。
```

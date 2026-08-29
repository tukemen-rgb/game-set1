# スマホだけで進める手順

自動生成パイプラインを入れたので、**社長の作業は最初の1回だけ**になった。
以降は画像のダウンロードもアップロードも不要。

## 1回だけやること: APIキーを登録する

これを登録した時点から、1時間ごとに勝手に画像が増えていく。

### (a) OpenAI の APIキーを作る

1. スマホのブラウザで https://platform.openai.com/api-keys を開く
2. ログイン →「Create new secret key」→ 名前は何でもよい
3. **表示された `sk-...` をコピー**（この画面を閉じると二度と見られない）
4. 支払い方法が未設定なら Billing で登録する（従量課金）

### (b) GitHub に登録する

1. スマホのブラウザでこのリンクを開く
   https://github.com/tukemen-rgb/game-set1/settings/secrets/actions
2. 開けない場合は**デスクトップ表示に切り替える**
   - iPhone Safari: アドレスバー左の「ぁあ」→「デスクトップ用Webサイトを表示」
   - Android Chrome: 右上の︙→「PC版サイト」
3. 「New repository secret」をタップ
4. Name に **`OPENAI_API_KEY`**（この綴りちょうど）
5. Secret に (a) でコピーした `sk-...` を貼る
6. 「Add secret」をタップ

これで完了。毎時17分に自動で動き出す。

## 最初の1枚だけ手動で回す（推奨）

いきなり113枚に走らせず、**まず1枚だけ作って見た目を確認**する。

1. https://github.com/tukemen-rgb/game-set1/actions/workflows/generate-images.yml
2. 「Run workflow」をタップ
3. `この実行で生成する枚数` に **1** を入れる
4. 「Run workflow」で実行
5. 2〜3分待つ

できた画像は Claude 側で検証して、遠近が合っているか・プロンプトを直すべきかを報告する。

## 日々の操作（ほぼゼロ）

| やりたいこと | 操作 |
| --- | --- |
| 進捗を見る | Claude に「今日どこまでできた？」と聞く |
| 進捗を自分で見る | [STATUS.md](https://github.com/tukemen-rgb/game-set1/blob/claude/game-godot-setup-98dhue/docs/prompts/STATUS.md) を開く |
| この画像を作り直したい | Claude か GPT に「◯◯を作り直して」と言う（REDO.txt に書かれる） |
| 今すぐ止めたい | `incoming/STOP` という空ファイルを作る（下記） |
| 再開したい | `incoming/STOP` を削除する |

### 緊急停止のしかた（スマホ）

1. https://github.com/tukemen-rgb/game-set1/new/claude/game-godot-setup-98dhue/incoming
2. ファイル名に `STOP` と入力（中身は空でよい）
3. 「Commit changes」

次の実行から生成が止まる。課金も止まる。

## 課金の安全装置（最初から入っている）

- **1回あたり2枚まで**（毎時17分 × 2枚 = 1日48枚が上限）
- **通算150枚で自動停止**（113枚 + 作り直しの余裕）
- `incoming/STOP` で即時停止
- 1枚ごとに `docs/prompts/spend_log.json` に記録が残る

上限を変えたいときは Claude に言えば調整する。

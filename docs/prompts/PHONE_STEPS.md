# スマホだけで進める手順

社長がPCを使えないときの操作。**タップ数が少ない順**に並べてある。
上から試して、動いたところで止まればよい。

## 手順0: 社長は何もしない（まずこれを試す）

GPT に URLS.txt を1件だけ書かせる。社長の操作は**GPTに一言頼むだけ**。

> incoming/URLS.txt に BG-danchi-morning = <生成画像の公開URL> を
> 1行だけ書いてコミットして

5分以内に自動でダウンロードを試み、結果が `STATUS.md` に出る。
取得できれば**以降113枚すべて社長の操作ゼロ**で進む。
ChatGPT の画像URLは認証付きで外から取れない可能性があるが、
失敗しても自動で破棄されるだけなので害はない。

## 手順1: スマホから GitHub にアップロード（手順0が失敗したら）

クラウドストレージ不要。iPhone / Android どちらも同じ。

1. ChatGPT アプリで画像を長押し → **保存**（写真アプリに入る）
2. スマホのブラウザでこのリンクを開く
   https://github.com/tukemen-rgb/game-set1/upload/claude/game-godot-setup-98dhue/incoming
3. **デスクトップ用サイトに切り替える**
   - iPhone Safari: アドレスバー左の「ぁあ」→「デスクトップ用Webサイトを表示」
   - Android Chrome: 右上の︙→「PC版サイト」にチェック
4. 「choose your files」をタップ → **フォトライブラリ**から画像を選ぶ
5. 下までスクロールして **Commit changes** をタップ
6. GPT に「アップした、MAP.txt 書いて」と伝える（ファイル名はそのままでよい）

※ デスクトップ表示に切り替えないとアップロード欄が出ないことがある。

## 手順2: リンクを Claude に貼るだけ（上が両方ダメなら）

1. 画像を Dropbox か Google ドライブのアプリに保存
2. 共有リンクを作成してコピー
3. **そのリンクをこのチャットに貼る**（IDも一緒に。例「BG-danchi-morning これ→ https://…」）

あとは Claude 側でダウンロードからコミットまで全部やる。
リンクの形式が直リンクでなくても Claude 側で変換を試すので、
**うまくいかない心配はしなくてよい**。

参考（直リンクへの変換）:
- Dropbox: 末尾の `?dl=0` を `?raw=1` に変える
- Google ドライブ: `.../file/d/【ID】/view` → `https://drive.google.com/uc?export=download&id=【ID】`

## 進捗の見かた（スマホでOK）

https://github.com/tukemen-rgb/game-set1/blob/claude/game-godot-setup-98dhue/docs/prompts/STATUS.md

「揃った ◯ / 113」が増えていれば取り込めている。

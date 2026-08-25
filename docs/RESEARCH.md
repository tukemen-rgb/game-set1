# 「ぼくのなつやすみ」風ゲーム — 情報収集ノート

AI 生成（Godogen / Gemini / Tripo3D など）に **モデル・背景・カメラアングルを
インプットするための資料**。実装（`scenes/summer/`）もこの仕様に沿っている。

## 1. 本家の基本情報（出典つき）

- シリーズ名: ぼくのなつやすみ（2000年〜, PlayStation ほか）
- 開発: ミレニアムキッチン / ディレクター **綾部和（Kaz Ayabe）**
- キャラクターデザイン: **上田三根子**（キレイキレイのイラストでも有名。
  明るくポップな線の少ない絵柄）
- 背景美術: アニメ美術の **スタジオ草薙** による手描き背景
- 舞台: 1975年8月、日本の田舎（月夜野がモデル）。8月1日〜31日の1ヶ月を過ごす

出典:
- https://en.wikipedia.org/wiki/Boku_no_Natsuyasumi
- https://en.wikipedia.org/wiki/Kaz_Ayabe
- https://en.wikipedia.org/wiki/Mineko_Ueda
- https://ja.wikipedia.org/wiki/上田三根子

## 2. ビジュアル文法（＝再現すべき要素）

1. **固定画面 × 場面ごとに違う映画的カメラ**。プレイヤーは動くが
   カメラは動かず、場面（スクリーン）を移動するとアングルが切り替わる
2. **手描き（水彩・ガッシュ調）の一枚絵背景 ＋ 3Dトゥーンキャラ**の重ね合わせ。
   背景は情報量が多く、キャラは線が少なくフラット
3. **昭和の夏のモチーフ**: 入道雲、ひまわり、セミの声、麦わら帽子、
   虫あみ、ラジオ体操、縁側、蚊取り線香、川遊び、田んぼ、砂利道
4. **1日のサイクル**: 朝→昼→夕方→夜、夜に日記。数字を競わず
   「思い出を集める」トーン

## 3. カメラアングル文法（実装済みの値）

本家は「場面ごとに1つの決め打ちカメラ」。今回のプロトタイプでは
1つの3D空間に4つの固定カメラを置き、プレイヤー位置で切り替える。

| 場面 | 位置 (x,y,z) | 注視点 | FOV | ねらい |
| --- | --- | --- | --- | --- |
| 庭（家の前） | (-17, 8, 11) | (-9, 0.5, 1) | 40° | 斜め見下ろしで家と庭を1枚絵に |
| 田舎道 | (24, 1.4, 9.5) | (-2, 1, 8) | 22° | ローアングル望遠。道の奥行きを圧縮する本家らしい画 |
| 川辺 | (3, 2.6, -22) | (0, 0.6, -10) | 55° | 対岸からの広角の引き。空と入道雲を入れる |
| 原っぱ | (13, 11, 15) | (0, 0, -1) | 50° | 高め俯瞰の「ジオラマ」ショット |

コツ: 望遠（FOV 20〜30°）＋ローアングルは「大人が子供の夏を思い出して
眺めている」画になる。広角（50°〜）は空を大きく入れて入道雲を見せる。

## 4. AI 生成用インプット（プロンプト集）

### 背景一枚絵（Gemini / 画像生成向け）

共通スタイル指定:

> 1970s rural Japan in mid-summer, hand-painted watercolor and gouache
> anime background art, soft edges, warm sunlight, towering cumulonimbus
> clouds, nostalgic Showa-era atmosphere, no people, painterly like
> classic Japanese animation background studios

場面別に足す指定（上のカメラ表と対応させる）:

- 庭: "old wooden Japanese farmhouse with engawa veranda, seen from a
  slightly elevated 3/4 angle, 40mm lens"
- 道: "gravel country road lined with sunflowers and utility poles,
  low-angle telephoto 100mm compression, heat haze"
- 川辺: "shallow clear river with stones, view from the opposite bank,
  wide 24mm lens, big summer sky"

### キャラクターモデル（Tripo3D / image-to-3D 向け）

> A cheerful Japanese boy around 9 years old, straw hat, white T-shirt,
> navy shorts, simple flat cel-shaded toon style with minimal lines,
> chibi-realistic proportion (4 heads tall), rigged biped, T-pose

※ 上田三根子タッチの「線が少ないポップな絵柄」を言葉で指定する。
実在キャラ名・作品名をプロンプトに入れないこと（下記 6.）。

### Godogen に渡す一文（ローカルで使う場合）

> 昭和50年の日本の田舎を舞台にした夏休み探索ゲーム。固定カメラを場面ごとに
> 切り替える。水彩画風の背景に、トゥーン調の麦わら帽子の男の子。
> 8月1日から31日までの日数システム、虫とり、夜の日記。

## 5. 今回の実装との対応

| 本家の要素 | プロトタイプでの実装 |
| --- | --- |
| 固定カメラ切替 | 4ゾーン×固定 Camera3D、位置で自動切替 |
| 手描き背景 | （暫定）フラット色の3Dジオラマ。将来AI背景に差し替え |
| 1ヶ月システム | 8/1〜8/31、19時に日没→日記→翌朝 |
| 虫とり | 木にセミが湧く。近づいてスペースで捕獲（成功率65%） |
| 時間経過 | 空・太陽・霧の色が朝→昼→夕で変化 |
| 日記 | 1日の終わりにその日の成果を日記表示 |

## 6. 権利面の注意（重要）

- 「ぼくのなつやすみ」の**名称・キャラクター・楽曲・スクリーンショットを
  流用しない**。ここで作るのはあくまで「昭和の夏休み」ジャンルのオマージュ
- 画像生成プロンプトに作品名・作家名を入れない（スタイルは一般語で指定）
- 公開時のタイトルも独自名にする（現状の仮名: *いなかの夏*）

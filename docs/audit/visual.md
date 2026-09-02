# 視覚 QA レポート — 日付×時刻×天候スイープ（2026-09-02）

撮影: `test/CamShots.cs`（6 固定カメラ、UI と主人公は非表示、`RngSeed=2`）を
`SHOT_DAY` / `SHOT_HOUR` を変えて 14 通り回した。手順は `screenshots/sweep/run.sh`
（`--fixed-fps 10 --quit-after 24`、1 通り約 25 秒）。frame 番号 → カメラの対応は
2=CamDanchi / 5=CamStreet / 8=CamPark / 11=CamParkWest / 14=CamPlaza / 17=CamLot。
切り出しは `screenshots/sweep/crops/`。`screenshots/` は `.gitignore` 対象なので証拠画像はこの作業環境にのみ残る（再現は `bash screenshots/sweep/run.sh`）。ソースは変更していない。

## 先に: 指示の日付と実際の天気のずれ

天気は `WeatherOfDay(day) = (day*37+11)%100`（`scripts/SummerMain.cs:1485`）で
日付から決まる。指示にあった組み合わせのうち次は実態と違うので、撮る日を差し替えた。

| 指示 | 実際 | 撮った日 |
| --- | --- | --- |
| 晴れ昼 day 3 | day 3 は **くもり**(h=22) | 晴れ → **day 4**（h=59）|
| 曇り day 5 | day 5 は **晴れ**(h=96) | 曇り → **day 3** と **day 30**(h=21) |
| 祭り day 13 | 夏まつりは **day 24**（`FestivalDay = 24`, :186）。day 13 は `ObonDay`（窓が多く灯るだけ）| **day 24** 18:00 と、参考に day 13 18:00 |
| 送り火 day 16 | day 16 は **雨**(h=3) | そのまま撮った → 欠陥 #3 |

撮ったもの: d4 12.0/17.5/18.3/18.8、d3 12.0、d5 12.0、d8 12.0/17.5、d24 18.0、
d16 17.5/18.5、d30 12.0、d10 7.0（`StartHour` は `>0` なら通るので 7 時も撮れる）、d13 18.0。

## 欠陥（重要度順）

### 1. 全テクスチャの mipmap が無効 → 芝が「砂嵐」になる【高】

- **何が**: 少し離れた芝がピクセル単位の白・黄・緑のちらつきになり、俯瞰の
  CamDanchi では奥の芝が「ノイズの壁」に見える。空き地・公園の地面も同じ。
  時刻・天候に関係なく全カットに出る。
- **証拠**: `screenshots/sweep/d4h12.0/frame00000002.png`（中央奥の芝）、
  `screenshots/sweep/d4h12.0/frame00000017.png`（画面上半分の芝）、
  切り出し `screenshots/sweep/crops/sun_danchi_grass.png`。
- **原因の推定**: `assets/textures/**/*.import` の **21 ファイルすべて**が
  `mipmaps/generate=false`（例 `assets/textures/gen/TEX-grass_summer.jpg.import`）。
  エディタを通さずスクリプトで 3D に使っているため「Detect 3D」による自動切替が
  起きていない。芝は `scenes/BuildSummer.cs:227` で `Uv1Scale 43×43` に敷いているので
  縮小率が大きく、mipmap 無しの縮小サンプリングがそのままエイリアシングになる。
- **直し方**: 全 `.import` を `mipmaps/generate=true`（3D 用途なら `compress/mode=2`
  VRAM 圧縮も検討）にして `godot --headless --import` で再インポート。あわせて
  `StandardMaterial3D.TextureFilter` を `LinearWithMipmapsAnisotropic` にする。
  効果は同じカメラで撮り直して比較する。

### 2. 雨・くもりの日は時刻で空も光も一切変わらない【高】

- **何が**: 雨の日の 12:00 と 17:30、18:30 が**同じ絵**。夕方でも真昼の灰色のまま。
  お盆送り火（8/16 は雨、#3）の 18:30 も昼の明るさで、窓の灯りと炎だけが
  「夜」を主張していてちぐはぐ。
- **証拠**: `screenshots/sweep/d8h12.0/frame00000002.png` と
  `screenshots/sweep/d8h17.5/frame00000002.png` を画素比較 → 差分 >12 の画素は
  **1.0%**（=雨粒の線だけ）。`screenshots/sweep/d16h18.5/frame00000002.png`
  （18:30 なのに昼）。曇りは 12 時しか撮っていないが同じ経路。
- **原因の推定**: `scripts/SummerMain.cs:1141-1165` `UpdateSky()` の `Overcast` 分岐は
  固定色を入れて `return` しており、`_hour` を一度も参照しない。晴れ側にある
  `arc`/`evening`/`nightAlpha`/`_duskSky` の処理へ到達しない
  （`_nightPano` の重ねも晴れ限定）。
- **直し方**: Overcast 分岐でも `t = (_hour-6)/13` から明るさ係数（例
  `0.35 + 0.65*sin(πt)`）を作り、`SkyTopColor`/`SkyHorizonColor`/
  `AmbientLightColor`/`LightEnergy`/`ApplySkyTint` の値に掛ける。`_nightPano` の
  17 時以降の重ねも共通化する（`return` の前に置く）。既知: イテレーション 35/51 は
  「雨の空色」「晴れの夕方」を直したもので、雨・曇りの夕方は未着手。

### 3. 8/16 の送り火が土砂降りの中で燃えている【高】

- **何が**: 送り火の日（`OkuribiDay = 16`）は `WeatherOfDay(16)` が **雨**。
  白い雨の線が降る灰色の昼景の中で、4 つの炎と橙色の灯りが壁を照らしている。
  雨で灰色なので炎だけ浮き、しかも #2 により夕方にならない。
- **証拠**: `screenshots/sweep/d16h17.5/frame00000002.png`、
  `screenshots/sweep/d16h18.5/frame00000002.png`、切り出し
  `screenshots/sweep/crops/okuribi_rain.png`。
- **原因の推定**: `scripts/SummerMain.cs:1485` のハッシュがたまたま 16 で 3 を返す。
  送り火（:600-627）は天気を見ずに `_day == OkuribiDay && _hour >= 17` で点く。
- **直し方**: `WeatherOfDay` で `ObonDay`/`OkuribiDay`/`FestivalDay` を晴れ扱いに固定
  する（1 行）。日記・おばあさんの予報も同じ関数を通るので整合は保たれる。
  既知: イテレーション 56 で「送り火が無い」を直したときに 18:24 を撮っているが、
  雨だったことには触れていない。

### 4. 雨の日、池のふちが磨いた金属の輪になる【中】

- **何が**: 雨の日だけ、公園の池を囲む土のふちが暗い光沢のある「ブロンズのリング」
  に見える。晴れ・曇りは砂色のまま。
- **証拠**: `screenshots/sweep/d8h12.0/frame00000008.png`（雨）と
  `screenshots/sweep/d3h12.0/frame00000008.png`（曇り）の比較。
- **原因の推定**: 池のふちは `scenes/BuildSummer.cs:1301` で `TexMat("dirt")` の
  トーラス。`CollectGround()`（`scripts/SummerMain.cs:1500`）はテクスチャ名に
  `dirt` を含む材質を全部「地面」として拾い、`ApplyWetGround()`（:1517）で
  `Roughness 0.3 / Metallic 0.25` にする。平面なら濡れ色で済むが、丸い断面の
  トーラスでは環境反射がハイライトの帯になって金属に読める。
- **直し方**: 濡らす対象を「上向きの平面（BoxMesh/PlaneMesh）」に限定するか、
  ふちの材質に別名（例 `dirt_rim`）を付けて除外。`Metallic` は 0 のまま
  `Roughness` だけ下げる方が土らしい。

### 5. アーケードの屋根の下に雨が降る【中】

- **何が**: 商店街の通路（屋根つき）の中、店先や奥の突き当たりに雨の線が落ちている。
- **証拠**: `screenshots/sweep/d8h12.0/frame00000005.png`、切り出し
  `screenshots/sweep/crops/rain_arcade_streaks.png`（左の店の前と奥の柱の間）。
- **原因の推定**: `scripts/SummerMain.cs:979-1006` `SetupRainFx()` は
  y=14 から 68×68 m の一枚の箱で放出する `CpuParticles3D`。当たり判定は無く、
  `y=4.4` の屋根（`scenes/BuildSummer.cs:491`）を素通りする。
- **直し方**: 放出範囲をアーケードの足跡（x −19〜15、z 13.3〜18.5）を避けた
  2〜3 個の箱に分ける（`CpuParticles3D` を複製して `EmissionBoxExtents` を変える）か、
  屋根に `GPUParticlesCollisionBox3D` を置けるよう `GPUParticles3D` に替える。

### 6. 曇り・雨の日、低いカメラでは地平線に晴れの入道雲が残る【中】

- **何が**: 空のドームは灰色なのに、CamLot（目線 4.6 m）と CamPark（3.2 m）の
  地平線付近に、実写パノラマの青空と入道雲がそのまま（灰色に掛け算されて）見える。
  曇りの日（day 3 / 30）で特に目立つ。
- **証拠**: `screenshots/sweep/d3h12.0/frame00000017.png` 右上、切り出し
  `screenshots/sweep/crops/cloudy_lot_pano.png`（雲の形がそのまま）。雨は
  `screenshots/sweep/crops/rain_lot_seam.png`（帯の下端で写真の建物が見える）。
- **原因の推定**: `PanoramaRain`（`scenes/BuildSummer.cs:1636-1656`）は y=2〜58 の
  円筒だが、勾配（:1626-1629）は下端 α=0、42% まで 0.92 なので、
  **y≈2〜25 の帯は半透明**。目線が低いカメラはちょうどそこを見る。
  イテレーション 35 は CamDanchi（y=7.5 俯角 14.7°）で計算して y=2 に下げたが、
  透ける帯そのものは残っている。曇りは濃さ 0.72 なのでさらに透ける。
- **直し方**: 勾配の透明部分を短くする（例 0.12 で α=0.85）か、写真パノラマ側に
  「彩度を落とす」処理（`ApplySkyTint` は掛け算なので青が残る）を足す。
  雲のシルエットは消せないので、曇り用の 2 枚目の帯（雲写真）を用意するのが本筋。

### 7. 祭りの花火が CamDanchi では「白い塊」に見える【中】

- **何が**: 8/24 18:00、団地カメラの棟間の空に、輪郭がこぶ状の**クリーム色の塊**が
  出る。公園カメラでは点の集まりとして花火に見えるが、こちらは雲か爆発。
- **証拠**: `screenshots/sweep/d24h18.0/frame00000002.png` 上中央、切り出し
  `screenshots/sweep/crops/fes_danchi_blob.png`。同時刻の
  `screenshots/sweep/d24h18.0/frame00000008.png` は正常（点の花火）。
  晴れの同時刻 `screenshots/sweep/d4h18.3/frame00000002.png` には無い。
- **原因の推定**: `scripts/SummerMain.cs:1017-1046` 花火は半径 0.5×scale 1.1〜2.4 の
  白い球 420 個を加算合成。`FireworkFromHour = 17.5` で、17:40 に最大になる夕焼け
  （`_duskSky`）の明るい空に白を加算すると飽和して一枚の塊になる。
  カメラ距離が近い CamDanchi 側で顕著。
- **直し方**: 打ち上げ開始を 18.3 以降にするか、粒を小さく（`Radius 0.25`、
  `ScaleAmountMax 1.4`）して色を付ける（白でなく `Color` にオレンジ／緑）。
  加算なら空が暗くなるまで待つのが一番効く。

### 8. 真昼でも 30 m 先が白く沈む（霞が強すぎる）【中】

- **何が**: 晴れの正午、CamStreet の商店街の突き当たり（約 30 m）や CamDanchi の
  隣棟が霧の中のように白く抜け、CamPlaza は画面上 1/4 が灰色一色になる。
  8 月の晴天というより煙霧の日に見える。
- **証拠**: `screenshots/sweep/d4h12.0/frame00000005.png`、
  `screenshots/sweep/d4h12.0/frame00000014.png`（上部の灰色）。
- **原因の推定**: `scenes/BuildSummer.cs:198` と `scripts/SummerMain.cs:1167` の
  `FogDensity = 0.006`。Godot 4 の指数霧は 50 m で約 26%、100 m で約 45% 沈む。
  加えて `FogLightColor` に地平線色を入れているので昼は白っぽく被る。
- **直し方**: 昼は `0.002〜0.003`、夕方に向けて上げる（時刻で補間）。`FogSkyAffect`
  を下げて空の抜けを残す。

### 9. アーケードの屋根が透けて、蛍光灯が空に浮く【中〜低】

- **何が**: 正午の CamStreet で、屋根の位置に青空と遠景の木が見え、その中に
  灰色の四角（蛍光灯の器具）が縦一列に浮いている。屋根として読めない。
- **証拠**: `screenshots/sweep/crops/sun_street_roof.png`
  （元 `screenshots/sweep/d4h12.0/frame00000005.png`）。
- **原因の推定**: `scenes/BuildSummer.cs:485-493` 屋根は `alpha 0.45` の一枚板で、
  骨組みも縁も無い。器具（:573）は不透明。
- **直し方**: 屋根の α を 0.8 前後に上げる、または梁（横桟）を 5 m おきに置いて
  「透ける波板屋根」の骨格を見せる。

### 10. すべり台が「芝に置いた白い板 ＋ 浮いた階段」に見える【低〜中】

- **何が**: CamParkWest では、すべり台の滑走面が芝に寝た白い板、階段が右に離れた
  2 段の灰色ブロックに見え、一つの遊具に見えない。
- **証拠**: `screenshots/sweep/crops/sun_parkwest_slide.png`
  （元 `screenshots/sweep/d4h12.0/frame00000011.png`）。
- **原因の推定**: `scenes/BuildSummer.cs:1304-1312`。階段は 3 つの箱を z 方向に
  ずらして積んだだけで支柱・手すりが無く、滑走面は −24° の薄い板一枚。
  横から見ると連結部が無い。
- **直し方**: 階段の下に支柱 2 本、滑走面の両側に 5 cm の縁（手すり）を足す。

### 11. 水たまりが「マンホールの蓋」に見える【低】

- **何が**: 雨の日の水たまりは輪郭のはっきりした灰色の楕円で、反射も揺らぎも無く、
  舗装に置いた金属板に見える。商店街の中央のものは特にそう。
- **証拠**: `screenshots/sweep/crops/rain_plaza_puddle.png`
  （元 `screenshots/sweep/d8h12.0/frame00000014.png`）、
  `screenshots/sweep/d8h12.0/frame00000005.png` 中央。
- **原因の推定**: `scenes/BuildSummer.cs:1242-1275` `Metallic 0.7 / Roughness 0.05`
  だが、曇天では映すものが一様な灰色の空だけ。`NormalScale 0.35` の揺らぎは
  この距離では見えない。α 0.85 で下の舗装がほぼ隠れる。
- **直し方**: α を 0.5 前後に下げて舗装を透かす、縁を `Uv1Scale` の大きい
  ノイズで崩す、雨粒の波紋（小さな輪の一枚絵）を重ねる。

### 12. 見下ろしのカメラでは雨がほとんど見えない【低】

- **何が**: CamDanchi（俯角 14.7°、y=7.5）では雨の線が数本しか写らず、
  同じ時刻の CamPark では大量に降っている。
- **証拠**: `screenshots/sweep/d8h12.0/frame00000002.png`（線がほぼ無い）と
  `screenshots/sweep/d8h12.0/frame00000008.png`。
- **原因の推定**: 雨粒は `0.035×0.9×0.035` の縦長の箱（`scripts/SummerMain.cs:996`）。
  上から見ると点になる。
- **直し方**: `Mesh` を `QuadMesh` にして `BillboardMode = Enabled` の材質にする
  （カメラに正対する縦長の帯）。

### 13. 南の丘が塗り絵の塊【低】

- **何が**: CamLot の背景の丘が、陰影も稜線の細部も無い一色の丸い塊で、
  夕方は茶色の巨大な影に見える。
- **証拠**: `screenshots/sweep/d4h12.0/frame00000017.png`、
  `screenshots/sweep/d4h18.8/frame00000017.png`。
- **原因の推定**: `scenes/BuildSummer.cs:1701-1727` 潰した球 3 個 × 3 山、Unshaded。
- **直し方**: 丘の色を高さで 2 段に（麓は霞色へ寄せる）、または遠景写真の帯に
  丘を含める。優先度は低い（遠景の「霞んだ緑」としては成立している）。

### 14. 祭りの提灯・裸電球が 18:00 でまだ光っていない【低・要確認】

- **何が**: 8/24 18:00 の CamStreet で、屋台の裸電球は白い球のままで灯って見えない。
- **証拠**: `screenshots/sweep/d24h18.0/frame00000005.png`（中央の屋台）。解像度が
  低く確信度は中。
- **原因の推定**: `scripts/SummerMain.cs:1594-1597` は `AlbedoColor` を紙色→灯色に
  補間するだけで `Emission` を使わない。18:00 は `night=0.5` で中間色。
- **直し方**: 窓（`scripts/SummerMain.cs:1541`）と同じく `EmissionEnabled` + `EmissionEnergyMultiplier = night`。
  既知: イテレーション監査 18 は「昼に灯っている」を直したもので、夕方の弱さは
  触れていない。

### 15. 朝 7 時の空が正午とほぼ同じ【低・参考】

- **何が**: `SHOT_HOUR=7.0` は通るが、空の青さは正午と見分けがつかない
  （影は長く光は少し暖かい）。通常プレイは 8 時開始なので影響は小さい。
- **証拠**: `screenshots/sweep/d10h7.0/frame00000008.png` と
  `screenshots/sweep/d4h12.0/frame00000008.png`。
- **原因の推定**: `scripts/SummerMain.cs:1181-1186` 7 時は `topMorning→topNoon` の
  u=0.33 で、`topMorning` 自体が青い。
- **直し方**: 使うなら `topMorning`/`horMorning` をもっと淡く（白〜薄桃）する。

## 確認できた「正しく動いているもの」

- 団地の窓の灯り: 17:30 消灯 → 18:18 まだら点灯 → 18:48 多数点灯（d4 の frame 2）。
- 商店街の蛍光灯・自販機: 17:30 から点き始める（d4h17.5 frame 5）。
- 雨の日の地面の濡れ色・水たまりの表示、雨粒（d8）。晴れの日は水たまり非表示。
- 祭りの屋台は 8/24 だけ（d13h18.0 frame 5 には無い）。
- 送り火は 8/16 17 時以降だけ点く（d16h17.5 frame 2 には有り、d4/d24 には無い）。
- 夕焼けの帯は 17:30 で最大、18:48 で夜色へ（d4 の frame 8）。
- 曇りの日は影が消え、光が平坦になる（d3/d30 frame 14）。

## 今回の撮り方で見えなかったもの（次回）

- 曇りの夕方（day 3 / 30 の 17.5〜18.8）— #2 の曇り側の確認。
- 花火の全時間帯（17.5 / 18.5 / 19.0）と CamPlaza からの見え方。
- 商店街の看板の文字（この解像度では判読不能。`CloseUp.cs` の `CLOSEUP_AT` で寄る）。
- CamParkWest 17:30 の右下に出る星形の明るい斑（`d4h17.5/frame00000011.png`）。
  影の抜けか光漏れか判断できなかったので今回は載せていない。

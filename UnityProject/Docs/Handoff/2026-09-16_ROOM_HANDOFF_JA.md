# game-set1 別ルーム引き継ぎ入口 — 2026-09-16

この文書は会話の引き継ぎ入口。全制作履歴・全LOD索引・元ファイル監査は、同時納品した `GAME_SET1_FULL_HANDOFF_JA.txt` と `game_set1_handoff_2026-09-16.zip` に収録。元モデル25ZIPも別の全成果物付きパックで納品する。GLB/OBJバイナリをこの文書コミットでGitHubへ追加したわけではない。

## 1. 現在地・絶対条件

対象リポジトリは `tukemen-rgb/game-set1`、ブランチは `gpt/unity-migration`。世界観は2000年ごろの日本のニュータウンの夏休み。普通に人が暮らしている団地・公園・ベランダを作る。

- Godotを保持。base merge・force更新は明示承認なしで行わない。
- 新規コミットは2026-09-18 23:59 Asia/Tokyoまで。9/19 00:00 JST以降は新しい指示がなければ要約のみ。
- 地震・災害・復興テーマは除外。
- ユーザーの優先は、実3Dモデル／材質／見える改良。仮説的QAの追加だけで連続した実行を消費しない。
- 4K実写映画のような見た目を、リアルタイムUnityで成立させる。外部診断画像やAI画像では合格を主張しない。
- 各アセットは製造・施工・部品・寸法／板厚・取付・隙間／シール・曝露・経年の因果・geometryとmaterialの分担を記録。MASTERとLOD0〜3を作る。
- 材質はalbedo色空間、roughness、metallic/specular、F0、normalの目標と実装有無、microstructure、wetness、UV aging、角度応答を区別する。

## 2. 調査時に直接確認したGitHub状態

調査基準HEADは `de33fff98fc9a03c365950d278311d16be95410c`。この文書自身のコミットはその後に追加される。次の担当者は必ず最新HEADを再読取する。

- de33fff...：青一色表示を直す、材質色・全メッシュ対応CPU renderer。
- 親 `8450a0834df172f401b653ba417e966ca26c917d`：床排水口とエアコンドレンの統合。
- その親 `3b00f07f533086bae3949603c3082ef68dbf167c`：実開口付き外壁とベランダHero統合。
- このブランチのGitHub Actions検索は0実行。ユーザーPCにUnityがないという意味ではない。
- `ProjectSettings/ProjectVersion.txt` は6000.3.0f1、revision欄は000000000000。実Editor・実コンパイル成功を証明する値ではない。
- Implementation Readinessは過去記録93/100、今回再採点なし。ゲーム完成率ではない。
- Visual Fidelityはscore=null、未採点、PASS未証明。今回の追加得点0。

## 3. 最新のユーザー要望：青単色ではなく実素材色で表示

ユーザーの指摘は「説明書確認画像とかってなんで青色なの？色を塗ってきてほしい」。青い網など本来青い部品以外もデフォルト青にした確認画像を成果の代表にしない。

原因：一部のMatplotlib表示で色・テクスチャを渡さずデフォルト色を使用。さらに65,000/70,000面で表示を打ち切るコードがあり、全形状の検証にならなかった。元GLBに材質がないとは限らない。

最新の `UnityProject/Tools/render_material_preview.py` はGLBの全face、scene-node transform、base色・MR・対応normalを読む。CPU z-buffer、backface culling、diagnostic shadow、supersamplingを実装。任意の面数上限で欠落させない。

```bash
mkdir -p output/material-review
python UnityProject/Tools/render_material_preview.py input.glb --output output/material-review/preview.png --palette
```

`--palette`を省くとsource色。指定するとcream外壁、銀サッシ、ivory室外機、灰床、off-white軒裏等の別色GLBを出す。原本を壊さない。numpy/trimesh/Pillow/numbaとフォント参照等の実行環境を確認する。

ソースSHA256：`507ddd2f1993d4ad699a35002fcc2ae1ea2e1a86b710360552b0c77772ecf229`。

**色付き結果はGrounded3600を入力にしたもので、Drained3600と同一ではない。** 記録上の `balcony_material_colors_2026-09-16.zip` は、この引き継ぎ環境に実ファイルが未マウント。25個の回収ZIPに含まれると説明しない。GitHubからrendererを取り、最新入力版で再描画するか、当該ZIPを回収する。

色付き表示で残課題として記録されたのは、点検口の向き、返し手すりの接地、縦格子上端の接続。色の修正は構造の修正ではない。ガラスはまだ不透明な色調proxy、環境反射・屈折・GIも実Unity未確認。

## 4. 2つの担当系統を混ぜない

### 正式Unity QualityBlock系

`Assets/Editor/QualityBlock*.cs`、`Assets/QA/*contract*.json`、`Assets/Scenes/QualityBlock1990s.unity`。

今回 `QualityBlockNative4KReviewPacket.cs` を読み、既に外壁開口・サッシ周辺・手すり・隔て板・排水防水・室外機・雨樋・布団・公園・植生等の呼び出しがあることを確認した。

`QualityBlockRainwaterDownpipeInstallationQA.cs` も現存。RainGutterとHD_RainwaterDownpipeAssembly、75mm外径、4継手、8支持位置等を持つ。調査時コードのPipeX=4.45、FacadeZ=-7.30、PipeZ=-7.18。

### 外部Art/Toolsライブラリ系

`Assets/Art/<Set>`、`Tools/generate_*.py`、会話へ納品したOBJ/GLB。多くはUnity以外で生成・表示したモデルで、正式sceneへ統合・実行済みとは限らない。

後半の「railing/handrail/parapet、gutter/downspout/rainwaterを検索し既存ownerなし」は探索範囲不足。正式Editor側に関連実装があることを今回再確認。新Art系が必ず二重表示されているとは断定しないが、「元から実装がなかった」は引き継がない。

次はEditor・Tools・Art・QA・適用経路を全て調べ、同じ機能を別ownerで二重生成しない。外部VP75の89mm径と正式75mm外径の竪樋を同じものとして扱わない。

## 5. 正式系の主要な過去作業

1. scene/material/contractをrender receiptへSHA256束縛し、core evaluator直呼出しもbinding必須にする設計。
2. JsonUtilityのbool欠落をfalse扱いしないためraw JSONで12 critical decisionsを検査。present/status欠落・重複・unchecked/uncertain・矛盾はfail-closed。
3. クレセント錠の重複を撤回。既存FacadeSashLatchHardwareを原本とし、旧60 handlesをformalでdisabled。
4. 公園家具のUV/雨/手接触/shelter別weathering。掲示板アクリルsmoothness .875へ整合。
5. 30区画の隔て板：6mmboard、32mmframe、60mm床間隙、取付部品、独立rootと4LOD。
6. 重複雨樋を撤回し既存QualityBlockRainwaterDownpipeInstallationQAを正式ownerに戻す。
7. 54x36mを24patchへ分けた立体芝。舗装除外候補を再抽選せず、面積比例密度。保存fingerprintとformal pre-reflection/pre-still/pre-temporalのreadonly検証。
8. 草・隔て板・バルコニー排水の正式packet生成保存とmetadata coverage登録。
9. 太陽・影・草等の別名defect/categoryを中央canonical IDsへ修正。
10. compile receiptの存在だけで93→100にしないため、実Unity6000.3.0f1 clean compileと入力SHAを束縛する設計。

これらは実行成功を保証しない。各C#の共存、コールバック順序、保存／再open、reflection待機、Unity compile/runtimeが次の検証対象。

## 6. 外部3D制作の全バッチ索引

全MASTER/LOD数・保存ファイルパスは納品 `ASSET_LOD_CATALOG_JA.md` / `asset_lod_catalog.json` に掲載。ここでは機能と原本の変遷を保持する。

| バッチ | アセット／内容 |
|---|---|
| GardenProps | TerracottaPot240、TerracottaSaucer214、GalvanizedGardenBucket10L。実排水穴、足、薄肉バケツ、別底・縁・取付・木グリップ |
| MorningGlory | MorningGloryTrellisPlant。竹支柱・節・麻紐・つる・葉・漏斗花・土。既存鉢を再利用 |
| 朝顔修正 | Y-upカメラ、葉上面と側面の隙間修正、花内面＋縁追加。MorningGlorySurfaceRepairAuthoringを既存生成後処理に |
| LaundryHardware | BalconyLaundryArm450、LaundryPole2560。3実穴、実中空竿、金物2本の実寸配置 |
| BalconyLife | WateringCanCompact、ClothespinBasket224、WoodenClothespin70。67散水穴、32slot、実ばねと座 |
| SummerProps | BalconySlipperPair、EnamelWashBasin280、MosquitoCoilTray140。実4.4巻coil等 |
| Cleaning | OutdoorBroom900、PlasticDustpan265、HandScrubBrush185。個別毛・実吊り穴。既存小物とのベランダcornerレビュー |
| LaundrySoftgoods | BathTowel700x1200PoleDrape、CottonTShirtM_Hanger、SummerSheet1400x1900RailDrape。布厚・縁・重力変形 |
| PinchSmallLaundry | RoundPinchHanger360_24Clips、AnkleSockPair240、Handkerchief380Pinned。全LOD24pinch、実履き口・布厚 |
| TShirtRefinement | 前後身頃、実首・裾・左右袖口、縁・襟・縫製。初版の板状構造を修正 |
| FaucetHose | BalconyFaucetG13、GardenHoseCoilNozzle。実吐水・33散水穴、接続部 |
| ServiceCorner | BalconyFaucetG13Heroの軸・ガスケット・UV/normal、GardenHoseCoilNozzleRestingの接地姿勢、BalconyFloorDrain100の18実slot |
| HangingSoapNet | ユーザー写真を参考に青網、石鹸、吊り金物。初版32糸、v2は28糸の柔らかい短い袋。写真ピクセルは使わない |
| FaucetSoapReferenceV2 | 丸い鋳造本体・短いレバー・26歯nut・下向き吐水口。既存owner改良 |
| BalconyAC | OutdoorACCondenser780、RefrigerantPipeTrunking60x55_900、CondensateDrainHose16_1200。ファン・ガード・フィン・管厚 |
| ACInstallDetails | RefrigerantLineSetPair_6p35_9p52、PipeCoverWallElbow60x55、ACWallPenetrationSeal75。断熱、実穴、パテ、壁貫通 |
| BalconyWindow | AluminumSlidingSashWindow1800x1800、MosquitoScreenPanel870x1760、ExteriorSillDripFlashing1800 |
| WindowInstall | WindowPerimeterSealJoint1840x1840、WindowHeadDripCap1900、WindowSillWeepHoodPair |
| Guardrail | PowderCoatedBalconyGuardrail3600x1100、BalconyGuardrailCornerReturn1200x1100、ConcreteBalconyKerb3600x180x120 |
| Soffit | PaintedFiberCementSoffit3600x1200、BalconySoffitAccessHatch450、SoffitDripEdgeFlashing3600 |
| Rainwater | RainwaterDownpipeVP75_2400、RainwaterOffsetAssembly75_520、RainwaterPipeWallClamp75。89mm外径の外部モデル |
| ExteriorFacade | PaintedFiberCementFacade3600x2700、ExteriorVentHood150、FacadeBaseStarterFlashing3600 |
| HeroBay | PaintedFiberCementFacadeWindowService3600x2700の窓・換気・AC実開口＋既存外装統合 |
| GroundContact | BalconyWaterproofFloor3600x970、RainwaterDownpipeFloorSleeve75、ACCondenserLevelingShimPair＋Grounded3600 |
| DrainageIntegration | BalconyWaterproofFloorDrainIntegrated3600x970、CondensateDrainHose16_FloorDrainRoute、既存FloorDrain100＋Drained3600 |
| MaterialColor | Grounded3600へ素材色を反映、全face実メッシュrenderer。Drained版との統合は未確認 |

### 重要な改良版の三角形数（MASTER / LOD0 / LOD1 / LOD2 / LOD3）

- 朝顔原版：36264 / 23888 / 15028 / 5134 / 2644。
- 朝顔面修正：47064 / 29776 / 17764 / 6094 / 2956。
- Tシャツ初版：29856 / 6456 / 4724 / 1104 / 372。
- Tシャツ改良版：71648 / 18392 / 12228 / 3248 / 1324。
- 写真寄せ蛇口v2：13472 / 8712 / 4992 / 3080 / 2012。
- 写真寄せ青ネットv2：88480 / 51552 / 25872 / 14352 / 8672。
- FloorDrain100：7936 / 6008 / 4076 / 2476 / 1896。

寸法・材質は各ZIP metadata参照。現行製品の寸法参考を年代確定資料と呼ばない。

## 7. 統合モデルの版管理

| 統合版 | MASTER | LOD0 | LOD1 | LOD2 | LOD3 |
|---|---:|---:|---:|---:|---:|
| BalconyExteriorHeroBay3600 | 168152 | 93604 | 43532 | 22236 | 13384 |
| BalconyExteriorHeroGrounded3600 | 171524 | 95584 | 44584 | 22788 | 13672 |
| BalconyExteriorHeroDrained3600 | 181180 | 101684 | 48720 | 25284 | 15584 |

上3行は今回の納品ZIPのGLBアクセサから保存数を読み直した。Unity描画やトポロジー再テストではない。色付きGroundedはgeometry同じでLOD0 95584。

GroundContactの回はローカル納品のみで、独立Gitコミットを確認できていない。後続Drained8450...の親はHeroBay3b00...。Gitにないバイナリは納品ZIPを引き継ぐ。

排水統合：床勾配1.5%、排水位置x=.45,z=.82、cutout半径43.4mm、flange52mm、支持幅8.6mm。ドレン始点[1.08009202,-.94782974,.262]から下降し、出口中心床上18mm、外周床間隔約7.8mmという当時の数値検査。数値上ほぼ0の誤差を現実施工精度と呼ばない。

## 8. 品質Gateの正本

`Assets/QA/visual_fidelity_gate.json`。調査時blob `2ba75b937a530f2344678d53752aad4d598ddc67`。

PASSは合計>=92 AND 全最低点 AND 重大欠陥ゼロ。

| ID | 配点 | 最低 |
|---|---:|---:|
| geometry_construction | 20 | 18 |
| material_pbr | 20 | 18 |
| lighting_shadows_reflections | 15 | 13 |
| texture_microdetail | 10 | 9 |
| weathering_causality | 10 | 9 |
| vegetation_natural_complexity | 8 | 7 |
| period_authenticity | 7 | 6 |
| cinematic_image | 5 | 4 |
| temporal_lod_aliasing | 5 | 4 |

重大欠陥canonical IDs：visible_primitive_placeholder、baked_or_painted_highlights、impossible_material_physics、obvious_repetition、hero_geometry_intersection、sun_shadow_inconsistency、forbidden_disaster_theme、severe_aliasing_or_shimmer、visible_lod_pop、major_light_leak、missing_construction_material_metadata、unverified_render_claim。

hero/oblique/grazingの実Unity3840x2160、100%crop、temporal、カテゴリ別証拠・減点が必要。外部画像、watertight、ファイル存在で点数を付けない。uncheckedを欠陥なしに変換しない。

## 9. 未解決・失敗から学ぶ点

- 朝顔の表示不良はカメラだけでなく葉接合と花内面にも実欠陥があった。数値成功だけで画像を見ず納品しない。
- 閉面・正体積は部品単体の証明。組み合わせた接触、floating、貫通、流路、実際の施工成立は別。
- Guardrail LOD3の格子間引き、網戸のLOD別網密度変更は見え方が大きく変わり得る。三角形数の厳密減少のために構造を消さない。
- 低解像度微細proxyの繰返し、UV/normal tangent、ガラス、粗い素体、木の面取り・端面、布・網の薄さ等は4K未検証。
- 多くの後半モデルは正式QualityBlockへ未統合。既存Editor ownerを壊さず統合計画が必要。
- 一部「完全generator」納品は要約だけ。今回確認したRainwaterの663バイト、GroundContactの1237バイトの.pyは定数・コメントで関数なし。再生成できると説明しない。
- 完全スクリプトでも前回ZIP、絶対/mnt/data、notebook関数、依存ライブラリへの前提あり。入力SHAと出力SHAを固定し、作業コピーで実行確認する。
- 元画像をAIで塗り直して実モデルの修正済み画像と見せるのは禁止。ユーザー写真の透かしは消さず、写真ピクセルは流用しない。

## 10. 次の作業順

1. HEAD・並行差分・成果物の所在・実Unityログを確認。
2. Editor/Art/Tools双方を見て原本を確定。
3. 最新Drained版を全face・正transform・実素材色で表示。Grounded色版と混同しない。
4. 点検口の向き、返しの接地、格子上端を既存owner/配置で修正し、同じ視点の全景と接写で確認。
5. Unityが利用可能なら新規拡張停止。compile/import→正式packet→4K/crop/temporal→Gate採点。
6. Unity不可なら既知の形状・接地・高占有率材質を改善し、実GLB/OBJ・完全実行ソース・検査・色付き画像を納品。QA-only連続追加を避ける。

正式入口の現在ソース：`QualityBlockNative4KReviewPacket.Prepare()`、メニュー `NewTown/QA/Prepare Complete Native 4K Review Packet`。sceneの保存/再open/反射待機を含むため、原本と作業コピーを確認してから実行。ピクセル確認前に自動で合格にはしない。

## 11. 自動実行タスク

既存 `Unity 3Dモデル制作`、ID `6a9f66b5f9a4819199b9eb686719ae39`、1時間ごと、調査時enabled。

最終実行表示2026-09-16T08:45:42.795912Z、prompt更新08:48:41.476152Z。色付き表示・全face・3構造欠陥の優先が追記済み。別ルームで重複作成しない。

schedule返却はDTSTARTにAsia/TokyoがあるがUNTIL末尾Zなし、defaultTimezoneはPacific/Fiji。設定表示だけで解釈せず、ユーザーのJST停止期限を各コミット直前に判定する。この引き継ぎではタスク変更なし。

## 12. 添付の全引き継ぎファイル

- `GAME_SET1_FULL_HANDOFF_JA.txt`：本文＋全モデルLOD表＋元ZIP一覧、約4.7万文字。
- `HANDOFF_JA.md` / `.txt`：詳細本文。前半QAの各作業、後半全バッチ、修正理由、過去SHAを整理。
- `START_NEXT_ROOM.txt`：新ルームへ貼る開始指示。
- `CURRENT_SNAPSHOT.json`：HEAD、Gate、タスク、固定SHA参照先。
- `ASSET_LOD_CATALOG_JA.md` / `asset_lod_catalog.json` / `.csv`：83組のアーカイブ×モデル群を版別に記録、朝顔OBJ補完。
- `ARTIFACTS_JA.md` / `artifact_manifest.json`：25ZIPのSHA256・サイズ・CRC等。
- `mesh_file_index.json`：339GLB格納エントリ。重複／レビュー込みで、ユニーク資産数ではない。
- `source_integrity_audit.json`：同梱Python51件の静的構文確認。実行成功証明ではない。
- `evidence/archive_records.json`：元ZIP内101JSONのmetadata・検査記録・receiptを出典つき集約。
- 全モデル付きパック：元25ZIP（約234.46MiB）を変更せず同梱。最新色ZIPは今回未回収なので含まない。

別ルームはこの会話のsandboxや/mnt/dataを自動的に読めない場合がある。ユーザーが保存し新ルームへ添付する。GitHubだけで全GLBが得られると約束しない。

## 13. この文書作成時の検証範囲

今回実施：GitHub現在ref/commit/Gate/既存owner/ProjectVersion/Actions読取、既存タスク読取、25ZIPのCRCとSHA、339GLBのヘッダ・アクセサ確認、朝顔OBJ行数、51Pythonの構文、JSON記録集約、引き継ぎ文書化。

今回未実施：モデルの新規制作、全メッシュの再生成・接触/衝突再検査、Unity compile/import/render、画質採点、Godot変更、base merge。過去の検査成功報告をこの回で再実行したと言わない。

この文書は固定時点の入口。読む時点の最新差分を確認し、実施済み／ソースのみ／外部検証／Unity未検証を分けたまま引き継ぐこと。

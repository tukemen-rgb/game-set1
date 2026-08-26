# 画像生成プロンプト集

全 113 件。`tools/gen_prompts.py` が自動生成する（手で編集せず、スクリプト側を直して再実行）。

**共通ルール**: 人物を背景に入れない / 文字・ロゴを入れない /
実在の建物をそのまま写さない / 生成物は `incoming/` に置く。

**ファイル名の規則**: `<ID>.png`（例 `BG-danchi-morning.png`）。
同じIDで作り直したときは `<ID>_v2.png` のように末尾を足す。

## 背景プレート（img2img・下絵を必ず添付）（24件）

### `BG-danchi-morning` — 背景プレート / 団地の広場 / 夏の朝 8時

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_1_danchi.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-danchi-morning.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: five-storey Japanese danchi apartment blocks with deep balconies, handrails, weathered concrete, a mushroom-shaped water tower behind, street trees, power lines. Lighting: early summer morning around 8am, clear sky, long soft shadows, fresh light. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-danchi-noon` — 背景プレート / 団地の広場 / 真夏の正午

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_1_danchi.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-danchi-noon.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: five-storey Japanese danchi apartment blocks with deep balconies, handrails, weathered concrete, a mushroom-shaped water tower behind, street trees, power lines. Lighting: high summer noon, harsh overhead sunlight, deep short shadows, towering cumulonimbus clouds. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-danchi-evening` — 背景プレート / 団地の広場 / 夕方 18時

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_1_danchi.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-danchi-evening.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: five-storey Japanese danchi apartment blocks with deep balconies, handrails, weathered concrete, a mushroom-shaped water tower behind, street trees, power lines. Lighting: summer evening around 6pm, golden hour, warm orange light raking across the surfaces. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-danchi-overcast` — 背景プレート / 団地の広場 / 曇天

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_1_danchi.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-danchi-overcast.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: five-storey Japanese danchi apartment blocks with deep balconies, handrails, weathered concrete, a mushroom-shaped water tower behind, street trees, power lines. Lighting: overcast summer day, flat soft diffused light, no cast shadows, pale grey sky. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-danchi-rain` — 背景プレート / 団地の広場 / 雨上がり

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_1_danchi.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-danchi-rain.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: five-storey Japanese danchi apartment blocks with deep balconies, handrails, weathered concrete, a mushroom-shaped water tower behind, street trees, power lines. Lighting: just after summer rain, wet reflective ground, puddles, damp concrete, breaking clouds. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-danchi-night` — 背景プレート / 団地の広場 / 夜

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_1_danchi.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-danchi-night.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: five-storey Japanese danchi apartment blocks with deep balconies, handrails, weathered concrete, a mushroom-shaped water tower behind, street trees, power lines. Lighting: summer night, windows glowing warm, street lights, dark blue sky. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-street-morning` — 背景プレート / 商店街 / 夏の朝 8時

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_2_street.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-street-morning.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a covered shopping arcade of a Japanese neighbourhood centre, small shops with fabric awnings, shutters half down, a vending machine, translucent arcade roof, long telephoto compression down the street. Lighting: early summer morning around 8am, clear sky, long soft shadows, fresh light. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-street-noon` — 背景プレート / 商店街 / 真夏の正午

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_2_street.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-street-noon.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a covered shopping arcade of a Japanese neighbourhood centre, small shops with fabric awnings, shutters half down, a vending machine, translucent arcade roof, long telephoto compression down the street. Lighting: high summer noon, harsh overhead sunlight, deep short shadows, towering cumulonimbus clouds. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-street-evening` — 背景プレート / 商店街 / 夕方 18時

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_2_street.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-street-evening.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a covered shopping arcade of a Japanese neighbourhood centre, small shops with fabric awnings, shutters half down, a vending machine, translucent arcade roof, long telephoto compression down the street. Lighting: summer evening around 6pm, golden hour, warm orange light raking across the surfaces. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-street-overcast` — 背景プレート / 商店街 / 曇天

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_2_street.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-street-overcast.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a covered shopping arcade of a Japanese neighbourhood centre, small shops with fabric awnings, shutters half down, a vending machine, translucent arcade roof, long telephoto compression down the street. Lighting: overcast summer day, flat soft diffused light, no cast shadows, pale grey sky. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-street-rain` — 背景プレート / 商店街 / 雨上がり

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_2_street.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-street-rain.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a covered shopping arcade of a Japanese neighbourhood centre, small shops with fabric awnings, shutters half down, a vending machine, translucent arcade roof, long telephoto compression down the street. Lighting: just after summer rain, wet reflective ground, puddles, damp concrete, breaking clouds. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-street-night` — 背景プレート / 商店街 / 夜

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_2_street.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-street-night.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a covered shopping arcade of a Japanese neighbourhood centre, small shops with fabric awnings, shutters half down, a vending machine, translucent arcade roof, long telephoto compression down the street. Lighting: summer night, windows glowing warm, street lights, dark blue sky. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-park-morning` — 背景プレート / 公園と池 / 夏の朝 8時

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_3_park.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-park-morning.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a neighbourhood park with a round pond, a slide, swings, a sandbox, benches, trees, danchi blocks visible beyond the trees. Lighting: early summer morning around 8am, clear sky, long soft shadows, fresh light. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-park-noon` — 背景プレート / 公園と池 / 真夏の正午

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_3_park.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-park-noon.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a neighbourhood park with a round pond, a slide, swings, a sandbox, benches, trees, danchi blocks visible beyond the trees. Lighting: high summer noon, harsh overhead sunlight, deep short shadows, towering cumulonimbus clouds. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-park-evening` — 背景プレート / 公園と池 / 夕方 18時

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_3_park.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-park-evening.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a neighbourhood park with a round pond, a slide, swings, a sandbox, benches, trees, danchi blocks visible beyond the trees. Lighting: summer evening around 6pm, golden hour, warm orange light raking across the surfaces. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-park-overcast` — 背景プレート / 公園と池 / 曇天

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_3_park.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-park-overcast.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a neighbourhood park with a round pond, a slide, swings, a sandbox, benches, trees, danchi blocks visible beyond the trees. Lighting: overcast summer day, flat soft diffused light, no cast shadows, pale grey sky. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-park-rain` — 背景プレート / 公園と池 / 雨上がり

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_3_park.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-park-rain.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a neighbourhood park with a round pond, a slide, swings, a sandbox, benches, trees, danchi blocks visible beyond the trees. Lighting: just after summer rain, wet reflective ground, puddles, damp concrete, breaking clouds. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-park-night` — 背景プレート / 公園と池 / 夜

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_3_park.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-park-night.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a neighbourhood park with a round pond, a slide, swings, a sandbox, benches, trees, danchi blocks visible beyond the trees. Lighting: summer night, windows glowing warm, street lights, dark blue sky. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-plaza-morning` — 背景プレート / 大通りと空き地 / 夏の朝 8時

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_4_plaza.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-plaza-morning.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a suburban main street seen from above, asphalt road, zebra crossing, paved sidewalks, telephone poles, a park with a pond on one side. Lighting: early summer morning around 8am, clear sky, long soft shadows, fresh light. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-plaza-noon` — 背景プレート / 大通りと空き地 / 真夏の正午

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_4_plaza.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-plaza-noon.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a suburban main street seen from above, asphalt road, zebra crossing, paved sidewalks, telephone poles, a park with a pond on one side. Lighting: high summer noon, harsh overhead sunlight, deep short shadows, towering cumulonimbus clouds. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-plaza-evening` — 背景プレート / 大通りと空き地 / 夕方 18時

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_4_plaza.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-plaza-evening.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a suburban main street seen from above, asphalt road, zebra crossing, paved sidewalks, telephone poles, a park with a pond on one side. Lighting: summer evening around 6pm, golden hour, warm orange light raking across the surfaces. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-plaza-overcast` — 背景プレート / 大通りと空き地 / 曇天

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_4_plaza.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-plaza-overcast.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a suburban main street seen from above, asphalt road, zebra crossing, paved sidewalks, telephone poles, a park with a pond on one side. Lighting: overcast summer day, flat soft diffused light, no cast shadows, pale grey sky. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-plaza-rain` — 背景プレート / 大通りと空き地 / 雨上がり

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_4_plaza.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-plaza-rain.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a suburban main street seen from above, asphalt road, zebra crossing, paved sidewalks, telephone poles, a park with a pond on one side. Lighting: just after summer rain, wet reflective ground, puddles, damp concrete, breaking clouds. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `BG-plaza-night` — 背景プレート / 大通りと空き地 / 夜

- **添付する下絵**: https://raw.githubusercontent.com/tukemen-rgb/game-set1/claude/game-godot-setup-98dhue/docs/plates/ref_4_plaza.png
- **アスペクト比**: 16:9
- **出力ファイル名**: `BG-plaza-night.png`

```
Redraw this 3D rough as a photorealistic image. Keep the composition, camera angle, and the position and outline of every building and tree EXACTLY as in the rough. A Japanese new town suburb in the year 2000: a suburban main street seen from above, asphalt road, zebra crossing, paved sidewalks, telephone poles, a park with a pond on one side. Lighting: summer night, windows glowing warm, street lights, dark blue sky. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

## 空・遠景パノラマ（8件）

### `SKY-clear_cumulonimbus` — 空・遠景 / 快晴・入道雲

- **アスペクト比**: 16:9
- **出力ファイル名**: `SKY-clear_cumulonimbus.png`

```
A wide panoramic view of clear blue summer sky with huge towering cumulonimbus clouds on the horizon, seen from a suburban Japanese housing estate. Only sky and a low distant skyline of apartment blocks and hills. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `SKY-sunset` — 空・遠景 / 夕焼け

- **アスペクト比**: 16:9
- **出力ファイル名**: `SKY-sunset.png`

```
A wide panoramic view of summer sunset sky, orange to violet gradient, scattered clouds lit from below, seen from a suburban Japanese housing estate. Only sky and a low distant skyline of apartment blocks and hills. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `SKY-overcast` — 空・遠景 / 曇天

- **アスペクト比**: 16:9
- **出力ファイル名**: `SKY-overcast.png`

```
A wide panoramic view of flat overcast summer sky, pale grey, soft texture, seen from a suburban Japanese housing estate. Only sky and a low distant skyline of apartment blocks and hills. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `SKY-morning_haze` — 空・遠景 / 朝もや

- **アスペクト比**: 16:9
- **出力ファイル名**: `SKY-morning_haze.png`

```
A wide panoramic view of hazy early morning sky, pale blue with warm haze near the horizon, seen from a suburban Japanese housing estate. Only sky and a low distant skyline of apartment blocks and hills. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `SKY-after_rain` — 空・遠景 / 雨上がり

- **アスペクト比**: 16:9
- **出力ファイル名**: `SKY-after_rain.png`

```
A wide panoramic view of sky just after rain, breaking clouds with sunbeams, patches of blue, seen from a suburban Japanese housing estate. Only sky and a low distant skyline of apartment blocks and hills. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `SKY-dusk` — 空・遠景 / 薄暮

- **アスペクト比**: 16:9
- **出力ファイル名**: `SKY-dusk.png`

```
A wide panoramic view of deep blue dusk sky, the last warm glow at the horizon, seen from a suburban Japanese housing estate. Only sky and a low distant skyline of apartment blocks and hills. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `SKY-starry` — 空・遠景 / 夏の星空

- **アスペクト比**: 16:9
- **出力ファイル名**: `SKY-starry.png`

```
A wide panoramic view of summer night sky with faint stars and the milky way, deep navy, seen from a suburban Japanese housing estate. Only sky and a low distant skyline of apartment blocks and hills. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `SKY-typhoon` — 空・遠景 / 台風前

- **アスペクト比**: 16:9
- **出力ファイル名**: `SKY-typhoon.png`

```
A wide panoramic view of dramatic dark storm clouds before a typhoon, greenish grey, seen from a suburban Japanese housing estate. Only sky and a low distant skyline of apartment blocks and hills. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

## タイル用テクスチャ（24件）

### `TEX-wall_danchi` — テクスチャ / 団地の外壁

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-wall_danchi.png`

```
A seamless tileable texture of weathered painted concrete wall of a 1970s Japanese apartment block, cream colour, vertical rain streaks, hairline cracks, patch repairs. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-concrete_raw` — テクスチャ / 打ち放しコンクリート

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-concrete_raw.png`

```
A seamless tileable texture of raw board-formed concrete surface, form tie holes, subtle stains. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-asphalt` — テクスチャ / アスファルト

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-asphalt.png`

```
A seamless tileable texture of old asphalt road surface, fine gravel aggregate, faint cracks and tar repair lines. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-paving_tile` — テクスチャ / 歩道の平板

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-paving_tile.png`

```
A seamless tileable texture of Japanese sidewalk concrete paving slabs, square grid joints, slightly stained. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-paving_block` — テクスチャ / インターロッキング

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-paving_block.png`

```
A seamless tileable texture of interlocking paving blocks in a herringbone pattern, muted red and grey. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-grass_summer` — テクスチャ / 夏の芝生

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-grass_summer.png`

```
A seamless tileable texture of summer lawn grass seen from directly above, slightly dry patches, clover mixed in. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-dirt_ground` — テクスチャ / 土のグラウンド

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-dirt_ground.png`

```
A seamless tileable texture of bare dry dirt playground ground, small pebbles, faint footprints. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-sand_box` — テクスチャ / 砂場の砂

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-sand_box.png`

```
A seamless tileable texture of fine playground sand seen from above, small ridges and footprints. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-water_pond` — テクスチャ / 池の水面

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-water_pond.png`

```
A seamless tileable texture of calm pond water surface seen from above, gentle ripples, slight green tint. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-leaf_canopy` — テクスチャ / 木の葉

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-leaf_canopy.png`

```
A seamless tileable texture of dense summer foliage of a broadleaf tree, layered green leaves, filtered light. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-metasequoia` — テクスチャ / メタセコイアの葉

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-metasequoia.png`

```
A seamless tileable texture of dense feathery foliage of a metasequoia conifer, fine bright green needles. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-bark` — テクスチャ / 木の幹

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-bark.png`

```
A seamless tileable texture of bark of a mature street tree, vertical furrows. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-roof_tile` — テクスチャ / 瓦屋根

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-roof_tile.png`

```
A seamless tileable texture of Japanese grey ceramic roof tiles in rows. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-shutter` — テクスチャ / シャッター

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-shutter.png`

```
A seamless tileable texture of closed corrugated metal shop shutter, pale beige, small rust spots and scratches. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-chain_link` — テクスチャ / 金網フェンス

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-chain_link.png`

```
A seamless tileable texture of chain link fence mesh, galvanised steel, flat frontal view. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-roof_gravel` — テクスチャ / 屋上の砂利防水

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-roof_gravel.png`

```
A seamless tileable texture of rooftop ballast gravel waterproofing layer seen from above, grey pebbles. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-curb_stone` — テクスチャ / 縁石

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-curb_stone.png`

```
A seamless tileable texture of a concrete kerb stone edge, weathered, faint tyre scuffs. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-manhole` — テクスチャ / マンホール

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-manhole.png`

```
A seamless tileable texture of a Japanese cast iron manhole cover with a geometric non-textual pattern, seen straight down. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-side_gutter` — テクスチャ / 側溝のふた

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-side_gutter.png`

```
A seamless tileable texture of a concrete side gutter with slotted drainage covers, seen straight down. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-playground_rubber` — テクスチャ / 遊具下のゴム舗装

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-playground_rubber.png`

```
A seamless tileable texture of soft rubber safety surfacing of a playground, faded green, fine speckles. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-mosaic_tile` — テクスチャ / モザイクタイル

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-mosaic_tile.png`

```
A seamless tileable texture of small square mosaic wall tiles from a 1970s Japanese building, muted beige and brown, visible grout. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-stucco_spray` — テクスチャ / 吹付けタイル

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-stucco_spray.png`

```
A seamless tileable texture of sprayed stucco exterior wall finish of a Japanese apartment, fine bumpy texture, cream colour. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-hedge` — テクスチャ / 植え込み

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-hedge.png`

```
A seamless tileable texture of a dense clipped evergreen hedge seen straight on, small glossy leaves. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `TEX-fallen_leaves` — テクスチャ / 落ち葉

- **アスペクト比**: 1:1
- **出力ファイル名**: `TEX-fallen_leaves.png`

```
A seamless tileable texture of scattered dry fallen leaves on concrete, seen straight down. Flat orthographic view straight on, evenly lit, no perspective, no cast shadows, no vignette, edges must tile seamlessly. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

## キャラクター（15件）

### `CHR-boy_front` — キャラクター / 主人公・正面

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-boy_front.png`

```
a cheerful Japanese boy about 10 years old in the year 2000, red baseball cap, white t-shirt, navy shorts, white sneakers, front view, arms slightly out, standing straight. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-boy_side` — キャラクター / 主人公・側面

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-boy_side.png`

```
the same boy, exact side profile view, same clothes and proportions. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-boy_back` — キャラクター / 主人公・背面

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-boy_back.png`

```
the same boy, exact back view, same clothes and proportions. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-boy_net` — キャラクター / 主人公・虫あみ

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-boy_net.png`

```
the same boy holding a long insect net over his shoulder, three-quarter view. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-boy_run` — キャラクター / 主人公・走る

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-boy_run.png`

```
the same boy running, side view, mid stride. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-girl_neighbour` — キャラクター / 近所の女の子

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-girl_neighbour.png`

```
a Japanese girl about 10 years old in the year 2000, straw hat, yellow sundress, sandals, front view. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-boy_friend` — キャラクター / 友達の男の子

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-boy_friend.png`

```
a chubby Japanese boy about 11, blue t-shirt, black shorts, holding a handheld game console, front view. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-mother` — キャラクター / お母さん

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-mother.png`

```
a Japanese woman in her thirties in the year 2000, simple summer blouse and skirt, apron, front view. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-shopkeeper` — キャラクター / 駄菓子屋のおばあさん

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-shopkeeper.png`

```
an elderly Japanese woman in a simple summer cardigan sitting, kind expression, front view. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-cicada` — キャラクター / セミ

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-cicada.png`

```
a large brown Japanese cicada (minmin-zemi) clinging to a tree trunk, side view, detailed wings. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-boy_squat` — キャラクター / 主人公・しゃがむ

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-boy_squat.png`

```
the same boy squatting down to look at something on the ground, side view. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-boy_look_up` — キャラクター / 主人公・見上げる

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-boy_look_up.png`

```
the same boy looking up at a tree, three-quarter view. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-boy_sleep` — キャラクター / 主人公・寝る

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-boy_sleep.png`

```
the same boy lying asleep on a futon, seen from above. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-beetle` — キャラクター / カブトムシ

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-beetle.png`

```
a Japanese rhinoceros beetle on a tree trunk, three-quarter view, detailed horn. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `CHR-stray_cat` — キャラクター / 野良猫

- **アスペクト比**: 1:1
- **出力ファイル名**: `CHR-stray_cat.png`

```
a calico cat sitting in the shade, side view. Simple flat cel-shaded toon style with minimal lines, about 4 heads tall, full body visible, standing on a plain flat mid-grey background, even lighting, no shadow on the background. no text or lettering, no watermark, no logo, do not copy any real existing building.
```

## 小物（26件）

### `PROP-insect_net` — 小物 / 虫あみ

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-insect_net.png`

```
a Japanese childrens insect net, bamboo pole, green mesh. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-insect_cage` — 小物 / 虫かご

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-insect_cage.png`

```
a green plastic Japanese insect cage with a hinged lid and carrying handle. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-vending_machine` — 小物 / 自販機

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-vending_machine.png`

```
a red Japanese drink vending machine from around 2000, front view, blank unbranded panels. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-post_box` — 小物 / 郵便ポスト

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-post_box.png`

```
a red cylindrical Japanese post box, front view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-bicycle` — 小物 / 子供用自転車

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-bicycle.png`

```
a childrens bicycle from around 2000 with a basket, side view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-radio_card` — 小物 / ラジオ体操カード

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-radio_card.png`

```
a paper Japanese radio exercise stamp card hanging from a string, blank grid of stamp squares. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-dagashi` — 小物 / 駄菓子の詰め合わせ

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-dagashi.png`

```
an assortment of Japanese penny sweets in colourful blank wrappers, top down. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-handheld_game` — 小物 / 携帯ゲーム機

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-handheld_game.png`

```
a grey handheld game console from around 2000, unbranded, front view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-card_game` — 小物 / カードゲームのカード

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-card_game.png`

```
a fan of blank collectible trading cards held in a hand. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-concrete_pipe` — 小物 / 土管

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-concrete_pipe.png`

```
large concrete drainage pipes stacked in an empty lot, weathered. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-bench_park` — 小物 / 公園のベンチ

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-bench_park.png`

```
a weathered wooden park bench with concrete legs, three-quarter view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-swing_set` — 小物 / ブランコ

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-swing_set.png`

```
a pale blue steel swing set with two wooden seats, three-quarter view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-slide` — 小物 / すべり台

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-slide.png`

```
a small childrens slide with concrete steps and a metal chute, three-quarter view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-water_tower` — 小物 / 給水塔

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-water_tower.png`

```
a mushroom-shaped concrete water tower of a Japanese housing estate, weathered, full view against sky. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-watermelon` — 小物 / スイカ

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-watermelon.png`

```
a cut Japanese watermelon on a plate, top down. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-kayari` — 小物 / 蚊取り線香

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-kayari.png`

```
a Japanese mosquito coil in a ceramic holder, thin smoke rising. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-mailboxes` — 小物 / 集合郵便受け

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-mailboxes.png`

```
a wall of aluminium apartment mailboxes, front view, blank name plates. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-notice_board` — 小物 / 掲示板

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-notice_board.png`

```
a wooden community notice board with a small roof, empty cork surface, three-quarter view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-laundry_pole` — 小物 / 物干し竿

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-laundry_pole.png`

```
a balcony laundry pole rack with plain towels hanging, three-quarter view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-aircon_unit` — 小物 / 室外機

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-aircon_unit.png`

```
an outdoor air conditioner condenser unit from around 2000, beige, front view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-curve_mirror` — 小物 / カーブミラー

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-curve_mirror.png`

```
an orange framed convex traffic mirror on a pole, three-quarter view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-trash_point` — 小物 / ゴミ集積所

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-trash_point.png`

```
a small neighbourhood rubbish collection point with a green net over bags, three-quarter view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-flower_bed` — 小物 / 花壇

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-flower_bed.png`

```
a small concrete block flower bed with summer marigolds, three-quarter view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-bike_rack` — 小物 / 自転車置き場

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-bike_rack.png`

```
a simple steel bicycle parking rack with a corrugated roof, three-quarter view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-water_faucet` — 小物 / 公園の水飲み場

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-water_faucet.png`

```
a public park drinking fountain with a stainless basin on a concrete post, three-quarter view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `PROP-bench_engawa` — 小物 / 縁台

- **アスペクト比**: 1:1
- **出力ファイル名**: `PROP-bench_engawa.png`

```
a low wooden bench for sitting outside on a summer evening, three-quarter view. A single object centred on a plain flat mid-grey background, even studio lighting, no shadow on the background. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

## ディテール・汚しデカール（10件）

### `DET-rain_streak` — ディテール・汚し / 雨だれ

- **アスペクト比**: 1:1
- **出力ファイル名**: `DET-rain_streak.png`

```
A close-up photograph of vertical rain streak stains running down a concrete wall, filling the frame, shot straight on, evenly lit, isolated on a plain flat mid-grey background around it. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `DET-crack_wall` — ディテール・汚し / 壁のひび

- **アスペクト比**: 1:1
- **出力ファイル名**: `DET-crack_wall.png`

```
A close-up photograph of a fine branching crack in a concrete wall, filling the frame, shot straight on, evenly lit, isolated on a plain flat mid-grey background around it. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `DET-patch_repair` — ディテール・汚し / 補修跡

- **アスペクト比**: 1:1
- **出力ファイル名**: `DET-patch_repair.png`

```
A close-up photograph of a rectangular mortar patch repair on a concrete wall, slightly different colour, filling the frame, shot straight on, evenly lit, isolated on a plain flat mid-grey background around it. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `DET-moss_corner` — ディテール・汚し / 苔

- **アスペクト比**: 1:1
- **出力ファイル名**: `DET-moss_corner.png`

```
A close-up photograph of green moss growing in the damp corner of concrete, filling the frame, shot straight on, evenly lit, isolated on a plain flat mid-grey background around it. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `DET-rust_stain` — ディテール・汚し / 錆の垂れ

- **アスペクト比**: 1:1
- **出力ファイル名**: `DET-rust_stain.png`

```
A close-up photograph of a rust stain running down from a steel fixing on concrete, filling the frame, shot straight on, evenly lit, isolated on a plain flat mid-grey background around it. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `DET-road_crack` — ディテール・汚し / 路面のひび

- **アスペクト比**: 1:1
- **出力ファイル名**: `DET-road_crack.png`

```
A close-up photograph of cracked asphalt with black tar repair lines, filling the frame, shot straight on, evenly lit, isolated on a plain flat mid-grey background around it. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `DET-puddle` — ディテール・汚し / 水たまり

- **アスペクト比**: 1:1
- **出力ファイル名**: `DET-puddle.png`

```
A close-up photograph of a shallow puddle on asphalt after rain, seen from above, filling the frame, shot straight on, evenly lit, isolated on a plain flat mid-grey background around it. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `DET-chalk_drawing` — ディテール・汚し / ろう石の落書き

- **アスペクト比**: 1:1
- **出力ファイル名**: `DET-chalk_drawing.png`

```
A close-up photograph of faded chalk scribbles of simple shapes on concrete pavement, no letters or numbers, filling the frame, shot straight on, evenly lit, isolated on a plain flat mid-grey background around it. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `DET-tire_mark` — ディテール・汚し / タイヤ痕

- **アスペクト比**: 1:1
- **出力ファイル名**: `DET-tire_mark.png`

```
A close-up photograph of a faint tyre scuff mark on asphalt, filling the frame, shot straight on, evenly lit, isolated on a plain flat mid-grey background around it. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `DET-shadow_leaves` — ディテール・汚し / 木漏れ日

- **アスペクト比**: 1:1
- **出力ファイル名**: `DET-shadow_leaves.png`

```
A close-up photograph of dappled shadow of tree leaves cast on a flat pale surface, filling the frame, shot straight on, evenly lit, isolated on a plain flat mid-grey background around it. Photorealistic, no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

## UI・キーアート（6件）

### `UI-title_key` — UI・キーアート / タイトル用キーアート

- **アスペクト比**: 16:9
- **出力ファイル名**: `UI-title_key.png`

```
a nostalgic key illustration for a summer holiday game: a Japanese housing estate in summer seen from a low angle, huge cumulonimbus clouds, warm light, painterly. no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `UI-diary_paper` — UI・キーアート / 日記の紙

- **アスペクト比**: 16:9
- **出力ファイル名**: `UI-diary_paper.png`

```
a blank sheet of Japanese childrens summer diary paper with a faint grid, slightly aged, flat top down. no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `UI-bug_book` — UI・キーアート / セミ図鑑のページ

- **アスペクト比**: 16:9
- **出力ファイル名**: `UI-bug_book.png`

```
a blank illustrated field guide page layout for insects, empty frames, flat top down. no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `UI-calendar` — UI・キーアート / 8月のカレンダー

- **アスペクト比**: 16:9
- **出力ファイル名**: `UI-calendar.png`

```
a blank paper calendar page layout for one month, empty grid squares, flat top down. no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `UI-frame_wood` — UI・キーアート / UIの木枠

- **アスペクト比**: 16:9
- **出力ファイル名**: `UI-frame_wood.png`

```
a simple wooden frame border for a game interface panel, seamless edges, flat. no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

### `UI-map_estate` — UI・キーアート / 住区の地図

- **アスペクト比**: 16:9
- **出力ファイル名**: `UI-map_estate.png`

```
a simple hand drawn map of a Japanese housing estate neighbourhood, blocks, park, shopping street, top down. no people, no text or lettering, no watermark, no logo, do not copy any real existing building.
```

#!/usr/bin/env python3
"""画像生成AI用のプロンプト集を生成する。

  python3 tools/gen_prompts.py

docs/prompts/PROMPTS.md（人が読む・GPTに見せる）と
docs/prompts/prompts.json（取り込みスクリプトが読む）を書き出す。
プロンプトを増やすときはこのファイルのデータを足して再実行する。
"""

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "docs" / "prompts"
BRANCH = "claude/game-godot-setup-98dhue"
RAW = f"https://raw.githubusercontent.com/tukemen-rgb/game-set1/{BRANCH}"

# 全カテゴリ共通の禁止事項。生成物をゲームに使うための制約。
COMMON_RULES = (
    "no people, no text or lettering, no watermark, no logo, "
    "do not copy any real existing building"
)

# --- 背景プレート（img2img。下絵の構図を厳守させる） ---
CAMERAS = [
    ("danchi", "団地の広場", "ref_1_danchi.png",
     "five-storey Japanese danchi apartment blocks with deep balconies, "
     "handrails, weathered concrete, a mushroom-shaped water tower behind, "
     "street trees, power lines"),
    ("street", "商店街", "ref_2_street.png",
     "a covered shopping arcade of a Japanese neighbourhood centre, small "
     "shops with fabric awnings, shutters half down, a vending machine, "
     "translucent arcade roof, long telephoto compression down the street"),
    ("park", "公園と池", "ref_3_park.png",
     "a neighbourhood park with a round pond, a slide, swings, a sandbox, "
     "benches, trees, danchi blocks visible beyond the trees"),
    ("plaza", "大通りと空き地", "ref_4_plaza.png",
     "a suburban main street seen from above, asphalt road, zebra crossing, "
     "paved sidewalks, telephone poles, a park with a pond on one side"),
]

CONDITIONS = [
    ("morning", "夏の朝 8時", "early summer morning around 8am, clear sky, "
     "long soft shadows, fresh light"),
    ("noon", "真夏の正午", "high summer noon, harsh overhead sunlight, "
     "deep short shadows, towering cumulonimbus clouds"),
    ("evening", "夕方 18時", "summer evening around 6pm, golden hour, "
     "warm orange light raking across the surfaces"),
    ("overcast", "曇天", "overcast summer day, flat soft diffused light, "
     "no cast shadows, pale grey sky"),
    ("rain", "雨上がり", "just after summer rain, wet reflective ground, "
     "puddles, damp concrete, breaking clouds"),
    ("night", "夜", "summer night, windows glowing warm, street lights, "
     "dark blue sky"),
]

# --- 空・遠景パノラマ（背景板の後ろ。前後関係の問題が無く低リスク） ---
SKIES = [
    ("clear_cumulonimbus", "快晴・入道雲", "clear blue summer sky with huge "
     "towering cumulonimbus clouds on the horizon"),
    ("sunset", "夕焼け", "summer sunset sky, orange to violet gradient, "
     "scattered clouds lit from below"),
    ("overcast", "曇天", "flat overcast summer sky, pale grey, soft texture"),
    ("morning_haze", "朝もや", "hazy early morning sky, pale blue with warm "
     "haze near the horizon"),
    ("after_rain", "雨上がり", "sky just after rain, breaking clouds with "
     "sunbeams, patches of blue"),
    ("dusk", "薄暮", "deep blue dusk sky, the last warm glow at the horizon"),
    ("starry", "夏の星空", "summer night sky with faint stars and the milky "
     "way, deep navy"),
    ("typhoon", "台風前", "dramatic dark storm clouds before a typhoon, "
     "greenish grey"),
]

# --- タイル用テクスチャ ---
TEXTURES = [
    ("wall_danchi", "団地の外壁", "weathered painted concrete wall of a 1970s "
     "Japanese apartment block, cream colour, vertical rain streaks, "
     "hairline cracks, patch repairs"),
    ("concrete_raw", "打ち放しコンクリート", "raw board-formed concrete "
     "surface, form tie holes, subtle stains"),
    ("asphalt", "アスファルト", "old asphalt road surface, fine gravel "
     "aggregate, faint cracks and tar repair lines"),
    ("paving_tile", "歩道の平板", "Japanese sidewalk concrete paving slabs, "
     "square grid joints, slightly stained"),
    ("paving_block", "インターロッキング", "interlocking paving blocks in a "
     "herringbone pattern, muted red and grey"),
    ("grass_summer", "夏の芝生", "summer lawn grass seen from directly above, "
     "slightly dry patches, clover mixed in"),
    ("dirt_ground", "土のグラウンド", "bare dry dirt playground ground, small "
     "pebbles, faint footprints"),
    ("sand_box", "砂場の砂", "fine playground sand seen from above, small "
     "ridges and footprints"),
    ("water_pond", "池の水面", "calm pond water surface seen from above, "
     "gentle ripples, slight green tint"),
    ("leaf_canopy", "木の葉", "dense summer foliage of a broadleaf tree, "
     "layered green leaves, filtered light"),
    ("metasequoia", "メタセコイアの葉", "dense feathery foliage of a "
     "metasequoia conifer, fine bright green needles"),
    ("bark", "木の幹", "bark of a mature street tree, vertical furrows"),
    ("roof_tile", "瓦屋根", "Japanese grey ceramic roof tiles in rows"),
    ("shutter", "シャッター", "closed corrugated metal shop shutter, pale "
     "beige, small rust spots and scratches"),
    ("chain_link", "金網フェンス", "chain link fence mesh, galvanised steel, "
     "flat frontal view"),
    ("roof_gravel", "屋上の砂利防水", "rooftop ballast gravel waterproofing "
     "layer seen from above, grey pebbles"),
]

# --- キャラクター ---
CHARACTERS = [
    ("boy_front", "主人公・正面", "a cheerful Japanese boy about 10 years old "
     "in the year 2000, red baseball cap, white t-shirt, navy shorts, white "
     "sneakers, front view, arms slightly out, standing straight"),
    ("boy_side", "主人公・側面", "the same boy, exact side profile view, "
     "same clothes and proportions"),
    ("boy_back", "主人公・背面", "the same boy, exact back view, same clothes "
     "and proportions"),
    ("boy_net", "主人公・虫あみ", "the same boy holding a long insect net over "
     "his shoulder, three-quarter view"),
    ("boy_run", "主人公・走る", "the same boy running, side view, mid stride"),
    ("girl_neighbour", "近所の女の子", "a Japanese girl about 10 years old in "
     "the year 2000, straw hat, yellow sundress, sandals, front view"),
    ("boy_friend", "友達の男の子", "a chubby Japanese boy about 11, blue "
     "t-shirt, black shorts, holding a handheld game console, front view"),
    ("mother", "お母さん", "a Japanese woman in her thirties in the year 2000, "
     "simple summer blouse and skirt, apron, front view"),
    ("shopkeeper", "駄菓子屋のおばあさん", "an elderly Japanese woman in a "
     "simple summer cardigan sitting, kind expression, front view"),
    ("cicada", "セミ", "a large brown Japanese cicada (minmin-zemi) clinging "
     "to a tree trunk, side view, detailed wings"),
]

# --- 小物（3D化 or スプライト用） ---
PROPS = [
    ("insect_net", "虫あみ", "a Japanese childrens insect net, bamboo pole, "
     "green mesh"),
    ("insect_cage", "虫かご", "a green plastic Japanese insect cage with a "
     "hinged lid and carrying handle"),
    ("vending_machine", "自販機", "a red Japanese drink vending machine from "
     "around 2000, front view, blank unbranded panels"),
    ("post_box", "郵便ポスト", "a red cylindrical Japanese post box, front view"),
    ("bicycle", "子供用自転車", "a childrens bicycle from around 2000 with a "
     "basket, side view"),
    ("radio_card", "ラジオ体操カード", "a paper Japanese radio exercise stamp "
     "card hanging from a string, blank grid of stamp squares"),
    ("dagashi", "駄菓子の詰め合わせ", "an assortment of Japanese penny sweets "
     "in colourful blank wrappers, top down"),
    ("handheld_game", "携帯ゲーム機", "a grey handheld game console from "
     "around 2000, unbranded, front view"),
    ("card_game", "カードゲームのカード", "a fan of blank collectible trading "
     "cards held in a hand"),
    ("concrete_pipe", "土管", "large concrete drainage pipes stacked in an "
     "empty lot, weathered"),
    ("bench_park", "公園のベンチ", "a weathered wooden park bench with "
     "concrete legs, three-quarter view"),
    ("swing_set", "ブランコ", "a pale blue steel swing set with two wooden "
     "seats, three-quarter view"),
    ("slide", "すべり台", "a small childrens slide with concrete steps and a "
     "metal chute, three-quarter view"),
    ("water_tower", "給水塔", "a mushroom-shaped concrete water tower of a "
     "Japanese housing estate, weathered, full view against sky"),
    ("watermelon", "スイカ", "a cut Japanese watermelon on a plate, top down"),
    ("kayari", "蚊取り線香", "a Japanese mosquito coil in a ceramic holder, "
     "thin smoke rising"),
]

# --- UI・キーアート ---
UI_ITEMS = [
    ("title_key", "タイトル用キーアート", "a nostalgic key illustration for a "
     "summer holiday game: a Japanese housing estate in summer seen from a "
     "low angle, huge cumulonimbus clouds, warm light, painterly"),
    ("diary_paper", "日記の紙", "a blank sheet of Japanese childrens summer "
     "diary paper with a faint grid, slightly aged, flat top down"),
    ("bug_book", "セミ図鑑のページ", "a blank illustrated field guide page "
     "layout for insects, empty frames, flat top down"),
    ("calendar", "8月のカレンダー", "a blank paper calendar page layout for "
     "one month, empty grid squares, flat top down"),
    ("frame_wood", "UIの木枠", "a simple wooden frame border for a game "
     "interface panel, seamless edges, flat"),
    ("map_estate", "住区の地図", "a simple hand drawn map of a Japanese "
     "housing estate neighbourhood, blocks, park, shopping street, top down"),
]


def build():
    items = []

    for cam_id, cam_ja, ref, cam_desc in CAMERAS:
        for cond_id, cond_ja, cond_desc in CONDITIONS:
            items.append({
                "id": f"BG-{cam_id}-{cond_id}",
                "category": "background",
                "ja": f"背景プレート / {cam_ja} / {cond_ja}",
                "ref": f"{RAW}/docs/plates/{ref}",
                "aspect": "16:9",
                "prompt": (
                    "Redraw this 3D rough as a photorealistic image. "
                    "Keep the composition, camera angle, and the position and "
                    "outline of every building and tree EXACTLY as in the "
                    f"rough. A Japanese new town suburb in the year 2000: "
                    f"{cam_desc}. Lighting: {cond_desc}. "
                    f"Photorealistic, {COMMON_RULES}."
                ),
            })

    for sky_id, ja, desc in SKIES:
        items.append({
            "id": f"SKY-{sky_id}",
            "category": "sky",
            "ja": f"空・遠景 / {ja}",
            "ref": None,
            "aspect": "16:9",
            "prompt": (
                f"A wide panoramic view of {desc}, seen from a suburban "
                "Japanese housing estate. Only sky and a low distant skyline "
                f"of apartment blocks and hills. Photorealistic, {COMMON_RULES}."
            ),
        })

    for tex_id, ja, desc in TEXTURES:
        items.append({
            "id": f"TEX-{tex_id}",
            "category": "texture",
            "ja": f"テクスチャ / {ja}",
            "ref": None,
            "aspect": "1:1",
            "prompt": (
                f"A seamless tileable texture of {desc}. Flat orthographic "
                "view straight on, evenly lit, no perspective, no cast "
                "shadows, no vignette, edges must tile seamlessly. "
                f"Photorealistic, {COMMON_RULES}."
            ),
        })

    for chr_id, ja, desc in CHARACTERS:
        items.append({
            "id": f"CHR-{chr_id}",
            "category": "character",
            "ja": f"キャラクター / {ja}",
            "ref": None,
            "aspect": "1:1",
            "prompt": (
                f"{desc}. Simple flat cel-shaded toon style with minimal "
                "lines, about 4 heads tall, full body visible, standing on a "
                "plain flat mid-grey background, even lighting, no shadow on "
                f"the background. {COMMON_RULES.replace('no people, ', '')}."
            ),
        })

    for prop_id, ja, desc in PROPS:
        items.append({
            "id": f"PROP-{prop_id}",
            "category": "prop",
            "ja": f"小物 / {ja}",
            "ref": None,
            "aspect": "1:1",
            "prompt": (
                f"{desc}. A single object centred on a plain flat mid-grey "
                "background, even studio lighting, no shadow on the "
                f"background. Photorealistic, {COMMON_RULES}."
            ),
        })

    for ui_id, ja, desc in UI_ITEMS:
        items.append({
            "id": f"UI-{ui_id}",
            "category": "ui",
            "ja": f"UI・キーアート / {ja}",
            "ref": None,
            "aspect": "16:9",
            "prompt": f"{desc}. {COMMON_RULES}.",
        })

    return items


def write(items):
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUT_DIR / "prompts.json").write_text(
        json.dumps({"branch": BRANCH, "items": items}, ensure_ascii=False, indent=2),
        encoding="utf-8")

    by_cat = {}
    for it in items:
        by_cat.setdefault(it["category"], []).append(it)

    cat_ja = {
        "background": "背景プレート（img2img・下絵を必ず添付）",
        "sky": "空・遠景パノラマ",
        "texture": "タイル用テクスチャ",
        "character": "キャラクター",
        "prop": "小物",
        "ui": "UI・キーアート",
    }

    lines = [
        "# 画像生成プロンプト集",
        "",
        f"全 {len(items)} 件。`tools/gen_prompts.py` が自動生成する"
        "（手で編集せず、スクリプト側を直して再実行）。",
        "",
        "**共通ルール**: 人物を背景に入れない / 文字・ロゴを入れない /",
        "実在の建物をそのまま写さない / 生成物は `incoming/` に置く。",
        "",
        "**ファイル名の規則**: `<ID>.png`（例 `BG-danchi-morning.png`）。",
        "同じIDで作り直したときは `<ID>_v2.png` のように末尾を足す。",
        "",
    ]
    for cat, cat_items in by_cat.items():
        lines.append(f"## {cat_ja.get(cat, cat)}（{len(cat_items)}件）")
        lines.append("")
        for it in cat_items:
            lines.append(f"### `{it['id']}` — {it['ja']}")
            lines.append("")
            if it["ref"]:
                lines.append(f"- **添付する下絵**: {it['ref']}")
            lines.append(f"- **アスペクト比**: {it['aspect']}")
            lines.append(f"- **出力ファイル名**: `{it['id']}.png`")
            lines.append("")
            lines.append("```")
            lines.append(it["prompt"])
            lines.append("```")
            lines.append("")
    (OUT_DIR / "PROMPTS.md").write_text("\n".join(lines), encoding="utf-8")
    return len(items)


if __name__ == "__main__":
    n = write(build())
    print(f"generated {n} prompts -> docs/prompts/")

using Godot;

/// <summary>
/// プロシージャルテクスチャを assets/textures/*.png に焼き込むビルダー。実行方法:
///   godot --headless --path . --script res://scenes/BuildTextures.cs
/// その後 `godot --headless --import` で取り込み、BuildSummer.cs が参照する。
/// 画像生成 API を使わない「リアル寄り」化の土台（草地・砂利・田んぼ・水面法線など）。
/// </summary>
public partial class BuildTextures : SceneTree
{
    public override void _Initialize()
    {
        DirAccess.MakeDirRecursiveAbsolute("res://assets/textures");

        // 草地: 低周波の色ムラ＋細かいスペックル
        SaveRamp("grass", Noise(1975, 0.012f), 512,
            new[]
            {
                (0.0f, new Color(0.24f, 0.36f, 0.15f)),
                (0.45f, new Color(0.33f, 0.48f, 0.2f)),
                (0.75f, new Color(0.45f, 0.6f, 0.27f)),
                (1.0f, new Color(0.58f, 0.68f, 0.33f)),
            },
            (x, y, v) => v + ((x * 31 + y * 17) % 7) / 7f * 0.08f - 0.04f);

        // 砂利道: セルラーノイズで石粒感
        var gravel = new FastNoiseLite
        {
            Seed = 8,
            Frequency = 0.09f,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular,
        };
        SaveRamp("dirt", gravel, 512,
            new[]
            {
                (0.0f, new Color(0.55f, 0.48f, 0.36f)),
                (0.5f, new Color(0.7f, 0.62f, 0.47f)),
                (1.0f, new Color(0.84f, 0.77f, 0.6f)),
            });

        // 田んぼ: 苗の列（横縞）を落とした緑
        SaveRamp("paddy", Noise(31, 0.03f), 256,
            new[]
            {
                (0.0f, new Color(0.2f, 0.36f, 0.14f)),
                (0.55f, new Color(0.32f, 0.5f, 0.18f)),
                (1.0f, new Color(0.52f, 0.66f, 0.28f)),
            },
            (x, y, v) => Mathf.Sin(y / 256f * Mathf.Tau * 14f) > 0.82f ? v * 0.5f : v);

        // 木の葉: 濃淡のまだら
        SaveRamp("leaf", Noise(7, 0.05f), 256,
            new[]
            {
                (0.0f, new Color(0.13f, 0.26f, 0.1f)),
                (0.5f, new Color(0.22f, 0.4f, 0.15f)),
                (1.0f, new Color(0.38f, 0.55f, 0.23f)),
            });

        // 漆喰壁: ごく薄いムラ
        SaveRamp("plaster", Noise(55, 0.04f), 256,
            new[]
            {
                (0.0f, new Color(0.82f, 0.78f, 0.68f)),
                (1.0f, new Color(0.93f, 0.9f, 0.82f)),
            });

        // 瓦屋根: 横方向の段々
        SaveRamp("roof", Noise(91, 0.06f), 256,
            new[]
            {
                (0.0f, new Color(0.26f, 0.28f, 0.34f)),
                (1.0f, new Color(0.42f, 0.45f, 0.52f)),
            },
            (x, y, v) => (y % 24) < 3 ? v * 0.45f : v);

        // アスファルト: 暗いグレーの粒
        SaveRamp("asphalt", Noise(404, 0.2f), 256,
            new[]
            {
                (0.0f, new Color(0.22f, 0.22f, 0.24f)),
                (0.6f, new Color(0.3f, 0.3f, 0.33f)),
                (1.0f, new Color(0.38f, 0.39f, 0.42f)),
            });

        // 歩道タイル: 明るいグレー＋目地の格子
        SaveRamp("paving", Noise(77, 0.08f), 256,
            new[]
            {
                (0.0f, new Color(0.5f, 0.48f, 0.45f)),
                (1.0f, new Color(0.66f, 0.64f, 0.6f)),
            },
            (x, y, v) => (x % 64) < 2 || (y % 64) < 2 ? v * 0.55f : v);

        // 団地の外壁: 1タイル = 1住戸1フロア（窓＋ベランダ帯）。建物側でタイルする
        SaveFacade();

        // 実写コンクリ（Poly Haven CC0）に雨だれの縦スジを焼き込んだ外壁
        SaveWeatheredWall();

        // 水面の法線マップ: ノイズ高さ場から変換
        Image waterHeight = Noise(1203, 0.02f).GetSeamlessImage(256, 256);
        waterHeight.BumpMapToNormalMap(4f);
        waterHeight.SavePng("res://assets/textures/water_normal.png");

        GD.Print("textures saved to res://assets/textures/");
        Quit(0);
    }

    private static FastNoiseLite Noise(int seed, float freq)
    {
        return new FastNoiseLite { Seed = seed, Frequency = freq };
    }

    private static void SaveFacade()
    {
        const int size = 128;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgb8);
        var concrete = new Color(0.72f, 0.7f, 0.66f);
        var concreteDark = new Color(0.62f, 0.6f, 0.56f);
        var windowGlass = new Color(0.2f, 0.26f, 0.32f);
        var windowFrame = new Color(0.85f, 0.85f, 0.85f);
        var rail = new Color(0.5f, 0.52f, 0.54f);
        FillRect(img, 0, 0, size, size, concrete);
        FillRect(img, 0, 0, size, 6, concreteDark);            // 階の境目（上端）
        FillRect(img, 22, 14, 84, 58, windowFrame);            // 窓枠
        FillRect(img, 26, 18, 76, 50, windowGlass);            // ガラス
        FillRect(img, 63, 18, 3, 50, windowFrame);             // 引き違いの桟
        FillRect(img, 0, 80, size, 40, rail);                  // ベランダ手すり帯
        for (int x = 0; x < size; x += 10)
            FillRect(img, x, 82, 2, 36, concreteDark);         // 手すりの縦格子
        FillRect(img, 0, 78, size, 3, windowFrame);            // 手すり上端
        // コンクリートの汚れ（簡易スペックル）
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if ((x * 31 + y * 17) % 23 == 0)
                {
                    Color c = img.GetPixel(x, y);
                    img.SetPixel(x, y, c * 0.93f);
                }
            }
        }
        img.SavePng("res://assets/textures/facade.png");
    }

    private static void SaveWeatheredWall()
    {
        Image src = Image.LoadFromFile("res://assets/textures/photo/concrete_wall_004.jpg");
        src.Convert(Image.Format.Rgb8);
        src.Resize(512, 512);
        var streak = new FastNoiseLite { Seed = 9, Frequency = 0.02f };
        for (int x = 0; x < 512; x++)
        {
            float sv = Mathf.Max(0f, streak.GetNoise2D(x, 0f)); // 縦スジは列単位の強度
            for (int y = 0; y < 512; y++)
            {
                Color c = src.GetPixel(x, y);
                float f = 1f - 0.3f * sv;
                // わずかにクリーム色へ寄せる（団地の吹付け塗装ふう）
                src.SetPixel(x, y, new Color(
                    Mathf.Clamp(c.R * f * 1.08f, 0f, 1f),
                    Mathf.Clamp(c.G * f * 1.05f, 0f, 1f),
                    Mathf.Clamp(c.B * f * 0.98f, 0f, 1f)));
            }
        }
        src.SavePng("res://assets/textures/wall_weathered.png");
    }

    private static void FillRect(Image img, int x, int y, int w, int h, Color c)
    {
        int x2 = Mathf.Min(x + w, img.GetWidth());
        int y2 = Mathf.Min(y + h, img.GetHeight());
        for (int yy = Mathf.Max(y, 0); yy < y2; yy++)
            for (int xx = Mathf.Max(x, 0); xx < x2; xx++)
                img.SetPixel(xx, yy, c);
    }

    private static void SaveRamp(
        string name, FastNoiseLite noise, int size,
        (float T, Color C)[] ramp, System.Func<int, int, float, float> post = null)
    {
        Image src = noise.GetSeamlessImage(size, size);
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgb8);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = src.GetPixel(x, y).R;
                if (post != null)
                    v = Mathf.Clamp(post(x, y, v), 0f, 1f);
                img.SetPixel(x, y, Sample(ramp, v));
            }
        }
        img.SavePng($"res://assets/textures/{name}.png");
    }

    private static Color Sample((float T, Color C)[] ramp, float v)
    {
        v = Mathf.Clamp(v, 0f, 1f);
        for (int i = 1; i < ramp.Length; i++)
        {
            if (v <= ramp[i].T)
                return ramp[i - 1].C.Lerp(ramp[i].C, (v - ramp[i - 1].T) / (ramp[i].T - ramp[i - 1].T));
        }
        return ramp[^1].C;
    }
}

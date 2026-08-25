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

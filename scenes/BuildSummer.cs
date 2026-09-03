using Godot;

/// <summary>
/// summer_main.tscn をヘッドレスで生成するビルダー。実行方法:
///   godot --headless --path . --script res://scenes/BuildTextures.cs   （テクスチャ生成）
///   godot --headless --import                                          （PNG/JPG 取り込み）
///   godot --headless --path . --script res://scenes/BuildSummer.cs     （このスクリプト）
/// ランタイムロジックは一切持たない（godot.md の規約）。
///
/// 舞台は「2000年ごろのニュータウンの1住区」（千里ニュータウン参考、
/// docs/RESEARCH.md コンセプト v2）:
///   西=板状住棟の団地と給水塔 / 北=近隣センターの商店街（アーケード）/
///   南=池とすべり台のある公園 / 東=土管のある空き地。中央に大通りと横断歩道。
/// 実写リアリティの要素（航空写真の分析より）:
///   彫りの深いベランダ・階段室・パラペット・布団と室外機・雨どい・電線・
///   メタセコイア並木・遠景の団地群とゴルフ練習場のネット。
/// 外壁・舗装は Poly Haven の CC0 実写テクスチャ（assets/textures/photo/）。
/// </summary>
public partial class BuildSummer : SceneTree
{
    private static readonly Color DarkWood = new(0.3f, 0.22f, 0.15f);
    private static readonly Color Trunk = new(0.4f, 0.28f, 0.18f);
    private static readonly Color Skin = new(0.98f, 0.85f, 0.72f);
    private static readonly Color Stone = new(0.58f, 0.58f, 0.6f);
    private static readonly Color Concrete = new(0.7f, 0.68f, 0.64f);
    private static readonly Color ConcreteDark = new(0.55f, 0.54f, 0.51f);
    private static readonly Color ShadowGlass = new(0.3f, 0.31f, 0.33f);
    private static readonly Color RailPanel = new(0.78f, 0.76f, 0.71f);
    private static readonly Color WireColor = new(0.14f, 0.14f, 0.15f);
    private static readonly Color[] FutonColors =
    {
        new(0.75f, 0.4f, 0.42f), new(0.4f, 0.5f, 0.7f), new(0.85f, 0.8f, 0.65f),
    };

    public override void _Initialize()
    {
        var root = new Node3D { Name = "SummerMain" };

        BuildEnvironment(root);
        BuildGroundAndRoads(root);
        BuildDanchi(root);
        BuildShotengai(root);
        BuildRadioTaiso(root);
        BuildPuddles(root);
        BuildAsagao(root);
        BuildResidents(root);
        BuildSapMark(root);
        BuildFestival(root);
        BuildNoticeBoard(root);
        BuildOkuribi(root);
        BuildPark(root);
        BuildVacantLot(root);
        BuildTrees(root);
        BuildPoles(root);
        BuildBackdrop(root);
        BuildPlayer(root);
        BuildCameras(root);
        BuildUi(root);

        var temp = new Node();
        temp.AddChild(root);
        root.SetScript(GD.Load<CSharpScript>("res://scripts/SummerMain.cs"));
        Node reRoot = temp.GetChild(0);
        reRoot.GetNode("Player").SetScript(GD.Load<CSharpScript>("res://scripts/PlayerController.cs"));

        PackAndSave(reRoot, "res://scenes/summer_main.tscn");
    }

    // --- 保存まわり（godot.md の silent-failure 対策そのまま） ---

    private void PackAndSave(Node root, string path)
    {
        SetOwnerRecursive(root, root);
        int expected = CountNodes(root);
        var packed = new PackedScene();
        if (packed.Pack(root) != Error.Ok)
        {
            GD.PushError("pack failed");
            Quit(1);
            return;
        }
        Node test = packed.Instantiate();
        int got = CountNodes(test);
        test.Free();
        if (got < expected)
        {
            GD.PushError($"nodes dropped: expected {expected}, got {got}");
            Quit(1);
            return;
        }
        ResourceSaver.Save(packed, path);
        GD.Print($"saved {path}: {expected} nodes");
        Quit(0);
    }

    private static void SetOwnerRecursive(Node node, Node owner)
    {
        foreach (Node child in node.GetChildren())
        {
            child.Owner = owner;
            if (string.IsNullOrEmpty(child.SceneFilePath))
                SetOwnerRecursive(child, owner);
        }
    }

    private static int CountNodes(Node node)
    {
        int n = 1;
        foreach (Node child in node.GetChildren())
            n += CountNodes(child);
        return n;
    }

    // --- 部品ヘルパー ---

    private static StandardMaterial3D Mat(Color color, bool unshaded = false)
    {
        var m = new StandardMaterial3D { AlbedoColor = color, Roughness = 1f };
        if (unshaded)
            m.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        return m;
    }

    /// <summary>
    /// 生成された実写があればそれを、無ければプロシージャル版を使う。
    /// 画像が届いていない環境でも壊れないようにするための切り替え。
    /// </summary>
    private static string BestTex(string generated, string fallback)
    {
        return ResourceLoader.Exists($"res://assets/textures/{generated}") ? generated : fallback;
    }

    /// <summary>拡張子つきなら photo/ 等の相対名をそのまま、無印なら .png を補って読む。</summary>
    private static StandardMaterial3D TexMat(string tex, Vector2 uvScale, Color? tint = null, float roughness = 1f)
    {
        string file = tex.Contains('.') ? tex : tex + ".png";
        return new StandardMaterial3D
        {
            AlbedoTexture = GD.Load<Texture2D>($"res://assets/textures/{file}"),
            AlbedoColor = tint ?? Colors.White,
            Roughness = roughness,
            Uv1Scale = new Vector3(uvScale.X, uvScale.Y, 1f),
            // 芝は 43×43 で敷くので縮小率が大きい。mipmap 無しだと遠くの芝が
            // 画素単位の砂嵐になる（視覚監査 #1）。.import 側で mipmap を作り、
            // 寝た面には異方性で読む
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
    }

    private static MeshInstance3D MeshI(Mesh mesh, Vector3 pos, Material mat, bool noShadow = false)
    {
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, Position = pos };
        if (noShadow)
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return mi;
    }

    private static MeshInstance3D MeshI(Mesh mesh, Vector3 pos, Color color, bool unshaded = false)
    {
        return MeshI(mesh, pos, Mat(color, unshaded), noShadow: unshaded);
    }

    private static MeshInstance3D Box(Vector3 size, Vector3 pos, Color color)
    {
        return MeshI(new BoxMesh { Size = size }, pos, color);
    }

    private static MeshInstance3D TexBox(Vector3 size, Vector3 pos, string tex, Vector2 uvScale)
    {
        return MeshI(new BoxMesh { Size = size }, pos, TexMat(tex, uvScale));
    }

    private static StaticBody3D Collider(Vector3 size, Vector3 pos)
    {
        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = pos });
        return body;
    }

    // --- 環境（グラデーション空＋太陽） ---

    private static void BuildEnvironment(Node3D root)
    {
        var skyMat = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.2f, 0.45f, 0.85f),
            SkyHorizonColor = new Color(0.65f, 0.82f, 0.95f),
            SkyCurve = 0.12f,
            GroundBottomColor = new Color(0.2f, 0.17f, 0.13f),
            GroundHorizonColor = new Color(0.65f, 0.82f, 0.95f),
            SunAngleMax = 25f,
            SunCurve = 0.12f,
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = skyMat },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.66f, 0.64f, 0.6f),   // 青に寄っていた（団地が (92,105,107)、参考画は (92,94,88)）
            FogEnabled = true,
            FogLightColor = new Color(0.75f, 0.85f, 0.95f),
            FogDensity = 0.003f,   // 0.006 は真昼でも 30m 先が白く沈んだ（監査 #8）
        };
        root.AddChild(new WorldEnvironment { Name = "Env", Environment = env });

        var sun = new DirectionalLight3D
        {
            Name = "Sun",
            ShadowEnabled = true,
            RotationDegrees = new Vector3(-55f, -60f, 0f),
            LightEnergy = 1.1f,
        };
        root.AddChild(sun);
    }

    // --- 地面・大通り・歩道・横断歩道 ---

    private static void BuildGroundAndRoads(Node3D root)
    {
        var ground = new StaticBody3D { Name = "Ground" };
        ground.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(200f, 1f, 200f) },
            Position = new Vector3(0f, -0.5f, 0f),
        });
        // 芝は画面の大半を占めるので、実写が届いていればそれを使う
        // 120m 角だと、遠景の円筒（半径62m）の内側で地面が終わり、
        // 高いカメラからは芝の縁が「世界の端」として弧に見えていた。
        // 円筒の外まで伸ばせば、縁は円筒の壁に隠れる
        // 参考画（docs/reference/park_pond_gpt.png）の芝は暗いオリーブ (98,119,28)。
        // 素のテクスチャはライム (184,232,134) で明度が 1.8 倍あった（監査で実測）
        ground.AddChild(MeshI(new PlaneMesh { Size = new Vector2(200f, 200f) }, Vector3.Zero,
            TexMat(BestTex("gen/TEX-grass_summer.jpg", "grass"), new Vector2(43f, 43f), new Color(0.7f, 0.68f, 0.46f))));
        root.AddChild(ground);

        var roads = new Node3D { Name = "Roads" };
        // 大通り（東西、z=8）— 実写アスファルト
        roads.AddChild(TexBox(new Vector3(80f, 0.06f, 4f), new Vector3(0f, 0.03f, 8f), "photo/asphalt_02.jpg", new Vector2(16f, 0.8f)));
        // 歩道（両側）— 実写コンクリ床
        roads.AddChild(TexBox(new Vector3(80f, 0.08f, 1.6f), new Vector3(0f, 0.04f, 5.4f), "photo/concrete_floor_01.jpg", new Vector2(40f, 0.8f)));
        roads.AddChild(TexBox(new Vector3(80f, 0.08f, 1.6f), new Vector3(0f, 0.04f, 10.6f), "photo/concrete_floor_01.jpg", new Vector2(40f, 0.8f)));
        // 横断歩道（x=2）
        for (int i = 0; i < 5; i++)
        {
            roads.AddChild(Box(new Vector3(1.8f, 0.021f, 0.45f),
                new Vector3(2f, 0.065f, 6.5f + i * 0.78f), new Color(0.88f, 0.88f, 0.86f)));
        }
        // 団地へ入る小道と公園への小道
        roads.AddChild(TexBox(new Vector3(2f, 0.05f, 10f), new Vector3(-12f, 0.025f, 0f), "photo/concrete_floor_01.jpg", new Vector2(1.6f, 8f)));
        roads.AddChild(TexBox(new Vector3(2f, 0.05f, 12f), new Vector3(2f, 0.05f, -1f), "photo/concrete_floor_01.jpg", new Vector2(1.6f, 10f)));
        root.AddChild(roads);
    }

    // --- 団地（彫りの深い板状住棟＋給水塔＋広場） ---

    /// <summary>
    /// 板状住棟。+z 面がベランダ側、-z 面が階段室側。
    /// detailed=false は遠景用の簡易版（テクスチャのみ）。
    /// </summary>
    private static Node3D DanchiBlock(Vector3 pos, float rotY, int floors, float length, bool detailed)
    {
        const float FloorH = 2.7f;
        float height = floors * FloorH;
        var b = new Node3D { Position = pos, RotationDegrees = new Vector3(0f, rotY, 0f) };

        if (!detailed)
        {
            b.AddChild(MeshI(new BoxMesh { Size = new Vector3(length, height, 5f) },
                new Vector3(0f, height / 2f, 0f), TexMat("facade", new Vector2(length / 2f, floors))));
            b.AddChild(Box(new Vector3(length + 0.4f, 0.4f, 5.4f), new Vector3(0f, height + 0.2f, 0f), ConcreteDark));
            return b;
        }

        // 本体（実写コンクリ＋雨だれ）と屋上（砂利防水・パラペット・塔屋・アンテナ）
        b.AddChild(MeshI(new BoxMesh { Size = new Vector3(length, height, 5f) },
            new Vector3(0f, height / 2f, 0f), TexMat("wall_weathered", new Vector2(4f, 3f))));
        b.AddChild(MeshI(new BoxMesh { Size = new Vector3(length + 0.4f, 0.3f, 5.6f) },
            new Vector3(0f, height + 0.15f, 0f), TexMat("photo/gravel_concrete.jpg", new Vector2(4f, 1.4f))));
        b.AddChild(Box(new Vector3(length + 0.5f, 0.5f, 0.15f), new Vector3(0f, height + 0.45f, 2.75f), Concrete));
        b.AddChild(Box(new Vector3(length + 0.5f, 0.5f, 0.15f), new Vector3(0f, height + 0.45f, -2.75f), Concrete));
        b.AddChild(Box(new Vector3(0.15f, 0.5f, 5.6f), new Vector3(-length / 2f - 0.17f, height + 0.45f, 0f), Concrete));
        b.AddChild(Box(new Vector3(0.15f, 0.5f, 5.6f), new Vector3(length / 2f + 0.17f, height + 0.45f, 0f), Concrete));
        b.AddChild(Box(new Vector3(2f, 1.5f, 2f), new Vector3(length / 2f - 2.2f, height + 1.05f, 0f), Concrete));
        b.AddChild(MeshI(new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 2.4f },
            new Vector3(-length / 2f + 2f, height + 1.5f, 0f), ConcreteDark));

        // ベランダ側（+z）: 各階に床スラブ・手すり・奥の影帯・仕切り・布団・室外機
        for (int f = 0; f < floors; f++)
        {
            float y = f * FloorH;
            b.AddChild(Box(new Vector3(length + 0.2f, 0.16f, 0.85f), new Vector3(0f, y + 0.08f, 2.85f), Concrete));
            b.AddChild(Box(new Vector3(length + 0.1f, 1.02f, 0.07f), new Vector3(0f, y + 0.67f, 3.24f), RailPanel));
            // 一枚の帯で通していたが、それだと夕方に灯りを点けたとき
            // 建物が「一本の光る棒」になった。住戸ごとに2枚の窓に割る。
            // 割ったことで、点く部屋と点かない部屋ができる
            float unitW = length / 4f;
            for (int u = 0; u < 4; u++)
            {
                float ux = -length / 2f + unitW * (u + 0.5f);
                float paneW = Mathf.Min(2.1f, unitW * 0.4f);
                foreach (float side in new[] { -1f, 1f })
                {
                    b.AddChild(Box(new Vector3(paneW, FloorH - 1.25f, 0.12f),
                        new Vector3(ux + side * paneW * 0.56f, y + 1.88f, 2.56f), ShadowGlass));
                }
                // 窓と窓のあいだの壁（帯を割ったので、間が抜けないように）
                b.AddChild(Box(new Vector3(unitW - paneW * 2.12f + 0.05f, FloorH - 1.25f, 0.1f),
                    new Vector3(ux, y + 1.88f, 2.55f), Concrete));
            }
            for (int d = 0; d <= 4; d++)
            {
                b.AddChild(Box(new Vector3(0.08f, 2.3f, 0.8f),
                    new Vector3(-length / 2f + d * (length / 4f), y + 1.25f, 2.85f), Concrete));
            }
            if ((f * 7 + (int)Mathf.Abs(pos.Z)) % 3 == 0)
            {
                float fx = -length / 2f + 2f + (f * 5f) % (length - 4f);
                b.AddChild(Box(new Vector3(1.3f, 0.85f, 0.12f), new Vector3(fx, y + 0.72f, 3.32f), FutonColors[f % FutonColors.Length]));
            }
            if ((f * 5 + (int)Mathf.Abs(pos.X)) % 4 == 0)
            {
                float ax = length / 2f - 2.5f - (f * 3f) % (length - 5f);
                b.AddChild(Box(new Vector3(0.6f, 0.5f, 0.28f), new Vector3(ax, y + 0.42f, 3.0f), new Color(0.82f, 0.82f, 0.8f)));
            }
        }

        // 階段室側（-z）: 窓テクスチャ＋階段室3本（縦スリット窓）
        b.AddChild(MeshI(new BoxMesh { Size = new Vector3(length, height, 0.06f) },
            new Vector3(0f, height / 2f, -2.54f), TexMat("facade", new Vector2(length / 2f, floors))));
        foreach (float sx in new[] { -length / 3f, 0f, length / 3f })
        {
            b.AddChild(Box(new Vector3(2f, height + 0.25f, 1f), new Vector3(sx, (height + 0.25f) / 2f, -3.05f), Concrete));
            b.AddChild(Box(new Vector3(0.7f, height - 1.2f, 0.08f), new Vector3(sx, height / 2f, -3.6f), ShadowGlass));
            b.AddChild(Box(new Vector3(1.4f, 2.1f, 0.2f), new Vector3(sx, 1.05f, -3.62f), ShadowGlass));
        }
        // 妻面の雨どい
        foreach (float ex in new[] { -length / 2f - 0.08f, length / 2f + 0.08f })
        {
            b.AddChild(MeshI(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = height },
                new Vector3(ex, height / 2f, 1.9f), ConcreteDark));
        }
        b.AddChild(Collider(new Vector3(length, height, 6.8f), new Vector3(0f, height / 2f, 0.3f)));
        return b;
    }

    private static void BuildDanchi(Node3D root)
    {
        var danchi = new Node3D { Name = "Danchi", Position = new Vector3(-19f, 0f, 0f) };
        // ベランダ面（+z）をカメラ側（-z）へ向けるため 180° 回す。2棟とも同じ向き（南面平行配置）
        danchi.AddChild(DanchiBlock(new Vector3(0f, 0f, -6f), 180f, 5, 16f, detailed: true));
        danchi.AddChild(DanchiBlock(new Vector3(0f, 0f, 6f), 180f, 5, 16f, detailed: true));

        // 給水塔（キノコ型）
        var tower = new Node3D { Position = new Vector3(-11f, 0f, 21f) };
        tower.AddChild(MeshI(new CylinderMesh { TopRadius = 1.3f, BottomRadius = 1.5f, Height = 9f },
            new Vector3(0f, 4.5f, 0f), TexMat("wall_weathered", new Vector2(2f, 2f))));
        tower.AddChild(MeshI(new CylinderMesh { TopRadius = 3.4f, BottomRadius = 3.4f, Height = 3.2f },
            new Vector3(0f, 10.6f, 0f), Concrete));
        tower.AddChild(MeshI(new SphereMesh { Radius = 3.4f, Height = 2.4f },
            new Vector3(0f, 12.2f, 0f), ConcreteDark));
        danchi.AddChild(tower);

        // --- 棟間の広場（ここが芝のままだと「草原に建物を置いただけ」に見える） ---

        // 棟間を東西に貫く舗装通路と、各棟の入口へ伸びる短い枝道
        danchi.AddChild(TexBox(new Vector3(22f, 0.06f, 3.2f), new Vector3(0f, 0.03f, 0f),
            "photo/concrete_floor_01.jpg", new Vector2(12f, 2f)));
        foreach (float px in new[] { -5f, 0f, 5f })
        {
            foreach (float pz in new[] { -2.6f, 2.6f })
            {
                danchi.AddChild(TexBox(new Vector3(1.6f, 0.05f, 2.6f), new Vector3(px, 0.028f, pz),
                    "photo/concrete_floor_01.jpg", new Vector2(1.2f, 2f)));
            }
        }

        // 通路の両脇の植え込み（低く刈り込んだ生垣）。
        // ここは起動して最初に映る画（CamDanchi）の手前を占めるのに、
        // 無地の緑の箱だった。葉のテクスチャを貼り、刈り込みの凹凸を足す。
        for (int i = 0; i < 6; i++)
        {
            float hx = -9f + i * 3.6f;
            foreach (float hz in new[] { -1.95f, 1.95f })
            {
                float tone = hz < 0f ? 0.92f : 1.0f;   // 南北で明るさを変える
                danchi.AddChild(MeshI(new BoxMesh { Size = new Vector3(3f, 0.6f, 0.55f) },
                    new Vector3(hx, 0.3f, hz),
                    TexMat(BestTex("gen/TEX-leaf_canopy.jpg", "leaf"), new Vector2(2.4f, 0.7f),
                           new Color(0.62f * tone, 0.78f * tone, 0.55f * tone))));
                // 刈り込みの天面は真っ平らにならない。小さな塊を3つ載せて縁を崩す
                for (int k = 0; k < 3; k++)
                {
                    float bx = hx - 0.95f + k * 0.95f;
                    // 隙間を空けると「並んだ箱」に見えるので、幅を重ねて連続させる
                    danchi.AddChild(MeshI(
                        new BoxMesh { Size = new Vector3(1.15f, 0.14f + (k % 2) * 0.06f, 0.56f) },
                        new Vector3(bx, 0.585f + (k % 2) * 0.02f, hz + ((k + i) % 2 == 0 ? 0.02f : -0.02f)),
                        TexMat(BestTex("gen/TEX-leaf_canopy.jpg", "leaf"), new Vector2(0.8f, 0.6f),
                               new Color(0.66f * tone, 0.82f * tone, 0.58f * tone))));
                }
                // 根元の土。緑の箱が地面から生えているように見えないのを直す
                danchi.AddChild(TexBox(new Vector3(3.1f, 0.06f, 0.72f), new Vector3(hx, 0.03f, hz),
                    "dirt", new Vector2(2f, 0.6f)));
            }
        }

        // 駐輪場（波板の屋根とラック、自転車を数台）
        var bikeShed = new Node3D { Position = new Vector3(-11.5f, 0f, 0f) };
        var shedColor = new Color(0.62f, 0.64f, 0.66f);
        foreach (float sx in new[] { -2.4f, 2.4f })
        {
            foreach (float sz in new[] { -1.3f, 1.3f })
                bikeShed.AddChild(Box(new Vector3(0.12f, 2.3f, 0.12f), new Vector3(sx, 1.15f, sz), ConcreteDark));
        }
        bikeShed.AddChild(Box(new Vector3(5.4f, 0.1f, 3.2f), new Vector3(0f, 2.35f, 0f), shedColor));
        for (int i = 0; i < 5; i++)
        {
            float bx = -1.9f + i * 0.95f;
            var body = new Color(0.2f + (i % 3) * 0.22f, 0.24f, 0.3f);
            bikeShed.AddChild(Box(new Vector3(0.08f, 0.55f, 1.3f), new Vector3(bx, 0.5f, 0f), body));   // フレーム
            bikeShed.AddChild(Box(new Vector3(0.5f, 0.06f, 0.06f), new Vector3(bx, 0.95f, -0.5f), body)); // ハンドル
            foreach (float wz in new[] { -0.55f, 0.55f })
            {
                var wheel = MeshI(new TorusMesh { InnerRadius = 0.24f, OuterRadius = 0.3f },
                    new Vector3(bx, 0.3f, wz), new Color(0.14f, 0.14f, 0.15f));
                wheel.RotationDegrees = new Vector3(90f, 0f, 0f);
                bikeShed.AddChild(wheel);
            }
        }
        danchi.AddChild(bikeShed);

        // ゴミ集積所（緑のネットをかけた囲い）
        var trash = new Node3D { Position = new Vector3(8.5f, 0f, -2.4f) };
        trash.AddChild(Box(new Vector3(2.6f, 0.7f, 1.6f), new Vector3(0f, 0.35f, 0f), new Color(0.55f, 0.57f, 0.55f)));
        var netMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.25f, 0.5f, 0.3f, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 1f,
        };
        var netBox = MeshI(new BoxMesh { Size = new Vector3(2.7f, 0.55f, 1.7f) }, new Vector3(0f, 0.95f, 0f), netMat);
        netBox.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        trash.AddChild(netBox);
        danchi.AddChild(trash);

        // 物干し場（棟の南側に竿を並べる。団地の記号）
        foreach (float dz in new[] { -3.4f, 3.4f })
        {
            for (int i = 0; i < 2; i++)
            {
                float dx = -6f + i * 12f;
                danchi.AddChild(Box(new Vector3(0.1f, 1.9f, 0.1f), new Vector3(dx - 1.4f, 0.95f, dz), Concrete));
                danchi.AddChild(Box(new Vector3(0.1f, 1.9f, 0.1f), new Vector3(dx + 1.4f, 0.95f, dz), Concrete));
                danchi.AddChild(Box(new Vector3(3f, 0.06f, 0.06f), new Vector3(dx, 1.85f, dz), Concrete));
            }
        }

        // 広場のベンチ
        danchi.AddChild(Box(new Vector3(1.8f, 0.4f, 0.5f), new Vector3(4.5f, 0.35f, -1f), DarkWood));
        danchi.AddChild(Box(new Vector3(1.8f, 0.4f, 0.5f), new Vector3(4.5f, 0.35f, 1.5f), DarkWood));
        root.AddChild(danchi);
    }

    // --- 商店街（近隣センター: 両側の店とアーケード屋根） ---

    private static void BuildShotengai(Node3D root)
    {
        var street = new Node3D { Name = "Shotengai" };
        street.AddChild(TexBox(new Vector3(36f, 0.07f, 6.4f), new Vector3(-2f, 0.035f, 15.9f), "photo/concrete_floor_01.jpg", new Vector2(18f, 3.2f)));

        Color[] awnings =
        {
            new(0.85f, 0.3f, 0.28f), new(0.25f, 0.55f, 0.5f), new(0.9f, 0.6f, 0.2f),
            new(0.3f, 0.45f, 0.7f), new(0.45f, 0.6f, 0.3f), new(0.85f, 0.5f, 0.55f),
            new(0.6f, 0.4f, 0.65f),
        };
        for (int i = 0; i < 7; i++)
        {
            float x = -16f + i * 4f;
            if (i == 5)
                street.AddChild(BuildDagashiya(new Vector3(x, 0f, 12.7f)));   // 開いている駄菓子屋
            else if (i != 3)
                street.AddChild(BuildShop(new Vector3(x, 0f, 12.7f), +1, awnings[i], i % 2 == 0, i));
            street.AddChild(BuildShop(new Vector3(x, 0f, 19.1f), -1, awnings[(i + 3) % 7], i % 2 == 1, i + 7));
        }
        street.AddChild(Collider(new Vector3(12f, 3f, 3f), new Vector3(-12f, 1.5f, 12.7f)));
        street.AddChild(Collider(new Vector3(12f, 3f, 3f), new Vector3(4f, 1.5f, 12.7f)));
        street.AddChild(Collider(new Vector3(28f, 3f, 3f), new Vector3(-4f, 1.5f, 19.1f)));
        street.AddChild(TexBox(new Vector3(2.6f, 0.05f, 3.6f), new Vector3(-4f, 0.045f, 12.6f), "photo/concrete_floor_01.jpg", new Vector2(1.8f, 2.4f)));

        // 0.45 では屋根が透けて蛍光灯が空に浮いて見えた（監査 #9）。
        // 波板らしく少し透かしつつ、梁を渡して屋根として読めるようにする
        var roofMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.9f, 0.8f, 0.72f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.4f,
        };
        var roof = MeshI(new BoxMesh { Size = new Vector3(34f, 0.12f, 5.2f) },
            new Vector3(-2f, 4.4f, 15.9f), roofMat);
        roof.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        street.AddChild(roof);
        for (float bx = -17f; bx <= 15f; bx += 4f)
            street.AddChild(Box(new Vector3(0.14f, 0.2f, 5.2f), new Vector3(bx, 4.28f, 15.9f), ConcreteDark));
        street.AddChild(Box(new Vector3(34f, 0.2f, 0.14f), new Vector3(-2f, 4.28f, 13.35f), ConcreteDark));
        street.AddChild(Box(new Vector3(34f, 0.2f, 0.14f), new Vector3(-2f, 4.28f, 18.45f), ConcreteDark));
        foreach (float x in new[] { -17f, -9f, -1f, 7f, 14f })
        {
            street.AddChild(Box(new Vector3(0.15f, 4.4f, 0.15f), new Vector3(x, 2.2f, 13.5f), ConcreteDark));
            street.AddChild(Box(new Vector3(0.15f, 4.4f, 0.15f), new Vector3(x, 2.2f, 18.3f), ConcreteDark));
        }

        // 望遠カメラ（東から西を覗く）は画角が狭く、写る高さは y=0〜3.5 ほど。
        // そこに一番大きく入るのが「列の端の店の横っ腹」で、無地のままだと
        // 画面の左右 1/3 がのっぺりした灰色の板になる。端の壁だけ作り込む。
        foreach ((float ex, float ez, int edir) in new[]
                 {
                     (9.95f, 12.7f, 1), (9.95f, 19.1f, 1),      // 東の入口側（望遠カメラの手前）
                     (-17.95f, 12.7f, -1), (-17.95f, 19.1f, -1), // 西の端
                     (-6.05f, 12.7f, 1), (-1.95f, 12.7f, -1),   // 南列のギャップ（入口）の両脇
                 })
        {
            street.AddChild(BuildEndWall(ex, ez, edir));
        }

        // 西の端。望遠カメラは西を向いているので、突き当りがそのまま
        // 画面の消失点になる。何も置かないと「そこで町が終わっている」に見える。
        // 東と対になる門柱を立て、その向こうに交差する道と家並みを置いて、
        // 「町はまだ続いていて、ただ今日は行かないだけ」にする。
        // 柱は通路の縁（z=14.0 / 17.8）に置く。z=13.3/18.5 だと店の列の裏に
        // 入ってしまい、東からの望遠では一度も見えない
        foreach (float gz in new[] { 14.0f, 17.8f })
        {
            street.AddChild(Box(new Vector3(0.42f, 4.2f, 0.42f), new Vector3(-19.4f, 2.1f, gz), ConcreteDark));
            street.AddChild(Box(new Vector3(0.46f, 1.5f, 0.46f), new Vector3(-19.4f, 3.15f, gz),
                new Color(0.3f, 0.5f, 0.62f)));
            street.AddChild(Collider(new Vector3(0.5f, 4.2f, 0.5f), new Vector3(-19.4f, 2.1f, gz)));
        }
        // 交差する道（南北）と横断歩道
        street.AddChild(TexBox(new Vector3(4.5f, 0.06f, 26f), new Vector3(-22.5f, 0.03f, 14f),
            "photo/asphalt_02.jpg", new Vector2(1.2f, 9f)));
        for (int i = 0; i < 4; i++)
        {
            street.AddChild(Box(new Vector3(0.45f, 0.021f, 1.6f),
                new Vector3(-24.2f + i * 0.78f, 0.065f, 15.9f), new Color(0.88f, 0.88f, 0.86f)));
        }
        // 交差点の向こうの家並み（切妻屋根の低い家を3軒）。遠景に奥行きを作る
        for (int i = 0; i < 3; i++)
        {
            float hz = 10.5f + i * 4.6f;
            var house = new Node3D { Position = new Vector3(-26.5f, 0f, hz) };
            house.AddChild(TexBox(new Vector3(3.6f, 2.5f, 3.4f), new Vector3(0f, 1.25f, 0f),
                "plaster", new Vector2(2f, 1.4f)));
            foreach (float side in new[] { -1f, 1f })
            {
                var slope = MeshI(new BoxMesh { Size = new Vector3(4.1f, 0.16f, 2.3f) },
                    new Vector3(0f, 2.9f, 0.95f * side), TexMat("roof", new Vector2(3f, 1.6f)));
                slope.RotationDegrees = new Vector3(28f * side, 0f, 0f);
                house.AddChild(slope);
            }
            street.AddChild(house);
        }

        // 入口の門柱。梁を渡すと画角の外（y=5 付近）に出てしまうので、
        // 柱と電飾看板だけを写る高さに置いて、望遠の絵に手前の枠を作る。
        foreach (float gz in new[] { 13.3f, 18.5f })
        {
            street.AddChild(Box(new Vector3(0.45f, 4.6f, 0.45f), new Vector3(10.7f, 2.3f, gz), ConcreteDark));
            street.AddChild(Box(new Vector3(0.5f, 1.9f, 0.5f), new Vector3(10.7f, 3.4f, gz),
                new Color(0.86f, 0.32f, 0.26f)));
            street.AddChild(Box(new Vector3(0.56f, 0.16f, 0.56f), new Vector3(10.7f, 2.42f, gz),
                new Color(0.93f, 0.9f, 0.82f)));
            street.AddChild(Collider(new Vector3(0.5f, 4.6f, 0.5f), new Vector3(10.7f, 2.3f, gz)));
        }

        // アーケードの照明。夕方になると団地の窓は灯るのに、商店街は
        // 陽が落ちるだけで真っ暗になり、シャッター街に見えていた。
        // 屋根の下に蛍光灯を並べ、SummerMain が時刻で点ける
        var lamps = new Node3D { Name = "StreetLights" };
        for (int i = 0; i < 7; i++)
        {
            float lx = -16f + i * 5.2f;
            // アーケードの屋根は影を落とさない設定なので、器具だけが影を落とすと
            // 昼間の通路に四角い影が点々と浮く（撮って気づいた）。器具も影を切る
            lamps.AddChild(MeshI(new BoxMesh { Size = new Vector3(1.5f, 0.1f, 0.26f) },
                new Vector3(lx, 4.24f, 15.9f), Mat(new Color(0.85f, 0.86f, 0.84f)), noShadow: true));
            lamps.AddChild(new OmniLight3D
            {
                Position = new Vector3(lx, 4.0f, 15.9f),
                LightColor = new Color(1f, 0.95f, 0.85f),
                LightEnergy = 0f,
                OmniRange = 8.5f,
                ShadowEnabled = false,
            });
        }
        street.AddChild(lamps);

        var vending = new Node3D { Position = new Vector3(14.5f, 0f, 10.8f) };
        vending.AddChild(Box(new Vector3(0.95f, 1.8f, 0.8f), new Vector3(0f, 0.9f, 0f), new Color(0.8f, 0.15f, 0.15f)));
        // 自販機の面。夜はここが光る（名前で拾って SummerMain が点ける）
        var panel = Box(new Vector3(0.8f, 0.9f, 0.05f), new Vector3(0f, 1.25f, -0.41f), new Color(0.9f, 0.9f, 0.92f));
        panel.Name = "VendingPanel";
        vending.AddChild(panel);
        // 自販機の下の十円玉。発見の独白が「ときどき 十円が おちて いる」と
        // 言うのに、拾えた ためしが無かった。日によって本当に落ちている
        var coin = new Node3D { Name = "Coin", Position = new Vector3(14.5f, 0f, 10.28f), Visible = false };
        // 実物大（直径2.3cm）だと歩いている高さからは見えない。
        // 拾えるものだと気づける大きさ・明るさにする
        var coinMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.85f, 0.66f, 0.34f),
            Metallic = 0.7f,
            Roughness = 0.25f,
        };
        var coinMesh = MeshI(new CylinderMesh { TopRadius = 0.085f, BottomRadius = 0.085f, Height = 0.012f },
            // 歩道は厚さ0.08の板なので、y=0.02 だと**舗装の中に埋まる**
            // （撮っても何も写らず、そこで初めて気づいた）
            new Vector3(0f, 0.095f, 0f), coinMat);
        coinMesh.RotationDegrees = new Vector3(4f, 0f, 7f);   // 少し傾いて落ちている
        coin.AddChild(coinMesh);
        street.AddChild(coin);

        street.AddChild(vending);
        root.AddChild(street);
    }

    /// <summary>
    /// 列の端で露出している店の側面を作り込む。dir は露出している面の向き（+1 = +X）。
    /// 腰壁・袖看板・室外機・雨どい・貼り紙・ビールケースで、無地の面を割る。
    /// </summary>
    private static Node3D BuildEndWall(float x, float z, int dir)
    {
        var w = new Node3D { Name = $"EndWall{(int)(x * 10)}_{(int)(z * 10)}" };
        float d = dir;

        // 腰壁（下半分のタイル帯）。無地の面はまず横に割ると嘘くささが減る
        w.AddChild(TexBox(new Vector3(0.07f, 0.85f, 2.96f), new Vector3(x + 0.05f * d, 0.43f, z),
            "photo/concrete_floor_01.jpg", new Vector2(3f, 1.2f)));
        w.AddChild(Box(new Vector3(0.1f, 0.07f, 2.98f), new Vector3(x + 0.06f * d, 0.86f, z), ConcreteDark));

        // 袖看板（壁から直角に張り出す縦看板）。商店街の横顔はほぼこれで決まる
        w.AddChild(Box(new Vector3(0.9f, 1.7f, 0.08f), new Vector3(x + 0.5f * d, 2.05f, z - 0.85f),
            new Color(0.93f, 0.91f, 0.85f)));
        w.AddChild(Box(new Vector3(0.94f, 0.26f, 0.1f), new Vector3(x + 0.5f * d, 2.78f, z - 0.85f),
            new Color(0.2f, 0.35f, 0.6f)));
        w.AddChild(Box(new Vector3(0.12f, 0.1f, 0.1f), new Vector3(x + 0.08f * d, 2.7f, z - 0.85f), ConcreteDark));

        // 室外機と架台
        w.AddChild(Box(new Vector3(0.5f, 0.58f, 0.78f), new Vector3(x + 0.3f * d, 1.28f, z + 0.8f), RailPanel));
        w.AddChild(Box(new Vector3(0.44f, 0.06f, 0.72f), new Vector3(x + 0.3f * d, 0.97f, z + 0.8f), ConcreteDark));
        w.AddChild(Box(new Vector3(0.06f, 0.34f, 0.06f), new Vector3(x + 0.5f * d, 0.8f, z + 1.1f), ConcreteDark));

        // 雨どい（縦に一本通すと壁の高さが読めるようになる）
        w.AddChild(Box(new Vector3(0.13f, 3.1f, 0.13f), new Vector3(x + 0.08f * d, 1.55f, z + 1.34f), ConcreteDark));

        // 貼り紙。少し傾け、色を散らす
        Color[] paper = { new(0.95f, 0.94f, 0.9f), new(0.95f, 0.85f, 0.4f), new(0.9f, 0.92f, 0.95f) };
        for (int i = 0; i < 3; i++)
        {
            var note = Box(new Vector3(0.04f, 0.4f, 0.28f),
                new Vector3(x + 0.045f * d, 1.45f + i * 0.42f, z - 0.15f + (i % 2) * 0.5f), paper[i]);
            note.RotationDegrees = new Vector3(0f, 0f, i == 1 ? 3.5f : -2.5f);
            w.AddChild(note);
        }

        // ビールケースの積み上げ（足元に物があると「使われている店」に見える）
        Color[] crate = { new(0.75f, 0.25f, 0.2f), new(0.2f, 0.35f, 0.55f), new(0.75f, 0.25f, 0.2f) };
        for (int i = 0; i < 3; i++)
        {
            w.AddChild(Box(new Vector3(0.42f, 0.28f, 0.42f),
                new Vector3(x + 0.32f * d, 0.14f + i * 0.28f, z - 1.15f + (i == 2 ? 0.06f : 0f)), crate[i]));
        }
        return w;
    }

    private static Node3D BuildShop(Vector3 pos, int facing, Color awning, bool glassFront, int idx)
    {
        // facing: +1 = 通路が +z 側（南列）、-1 = 通路が -z 側（北列）
        // 望遠カメラでは店の壁が画面の大半を占めるので、無地にせず
        // 「店先の情報」で埋める: 暖簾・平台と商品箱・縦看板・2階の窓・ビールケース。
        var shop = new Node3D { Position = pos };
        float front = 1.55f * facing;
        float f = facing;

        shop.AddChild(TexBox(new Vector3(3.9f, 3.1f, 3f), new Vector3(0f, 1.55f, 0f), "plaster", new Vector2(2f, 2f)));
        shop.AddChild(Box(new Vector3(3.9f, 0.3f, 3.1f), new Vector3(0f, 3.2f, 0f), ConcreteDark));

        // 店先: 開いている店はガラス戸、閉まっている店はシャッター（横筋つき）
        if (glassFront)
        {
            shop.AddChild(Box(new Vector3(2.6f, 2.2f, 0.1f), new Vector3(0f, 1.1f, front), new Color(0.42f, 0.5f, 0.55f)));
            shop.AddChild(Box(new Vector3(0.08f, 2.2f, 0.14f), new Vector3(0f, 1.1f, front + 0.03f * f), RailPanel));
            shop.AddChild(Box(new Vector3(2.7f, 0.1f, 0.14f), new Vector3(0f, 2.2f, front + 0.03f * f), RailPanel));
        }
        else
        {
            var shutter = new Color(0.66f, 0.62f, 0.56f);
            shop.AddChild(Box(new Vector3(2.6f, 2.2f, 0.1f), new Vector3(0f, 1.1f, front), shutter));
            for (int i = 0; i < 7; i++)
            {
                shop.AddChild(Box(new Vector3(2.6f, 0.05f, 0.13f),
                    new Vector3(0f, 0.25f + i * 0.3f, front + 0.02f * f), shutter * 0.82f));
            }
        }

        // 日よけ（テント庇）と、その下の看板
        var tent = MeshI(new BoxMesh { Size = new Vector3(3.6f, 0.08f, 1.1f) },
            new Vector3(0f, 2.55f, front + 0.5f * f), Mat(awning));
        tent.RotationDegrees = new Vector3(14f * facing, 0f, 0f);
        shop.AddChild(tent);
        shop.AddChild(Box(new Vector3(3.4f, 0.65f, 0.12f), new Vector3(0f, 2.85f, front + 0.05f * f),
            awning.Lerp(Colors.White, 0.55f)));

        // 暖簾（庇の下に垂らす布）。開いている店だけ
        if (glassFront)
        {
            for (int i = 0; i < 4; i++)
            {
                shop.AddChild(Box(new Vector3(0.62f, 0.55f, 0.03f),
                    new Vector3(-1.05f + i * 0.7f, 2.2f, front + 0.9f * f),
                    awning.Lerp(new Color(0.95f, 0.93f, 0.88f), 0.35f)));
            }
        }

        // 縦看板（壁に貼る色帯）。店ごとに位置を変える
        shop.AddChild(Box(new Vector3(0.42f, 1.5f, 0.08f),
            new Vector3(idx % 2 == 0 ? -1.6f : 1.6f, 1.9f, front + 0.02f * f),
            awning.Lerp(Colors.White, 0.2f)));

        // 2階の窓（無地の壁を割る）
        foreach (float wx in new[] { -1.1f, 1.1f })
        {
            shop.AddChild(Box(new Vector3(0.8f, 0.55f, 0.06f), new Vector3(wx, 2.72f, -front * 0.02f + front), ShadowGlass));
        }

        // 店先の平台と商品の箱（開いている店だけ）
        if (glassFront)
        {
            shop.AddChild(Box(new Vector3(2.2f, 0.08f, 0.75f), new Vector3(0f, 0.72f, front + 0.75f * f), DarkWood));
            shop.AddChild(Box(new Vector3(0.1f, 0.72f, 0.1f), new Vector3(-1f, 0.36f, front + 0.75f * f), DarkWood));
            shop.AddChild(Box(new Vector3(0.1f, 0.72f, 0.1f), new Vector3(1f, 0.36f, front + 0.75f * f), DarkWood));
            Color[] goods =
            {
                new(0.85f, 0.35f, 0.25f), new(0.95f, 0.8f, 0.3f), new(0.35f, 0.6f, 0.45f),
                new(0.9f, 0.6f, 0.7f), new(0.4f, 0.55f, 0.8f),
            };
            for (int i = 0; i < 3; i++)
            {
                shop.AddChild(Box(new Vector3(0.55f, 0.22f, 0.5f),
                    new Vector3(-0.75f + i * 0.75f, 0.87f, front + 0.75f * f),
                    goods[(idx * 3 + i) % goods.Length]));
            }
        }

        // 店先のビールケース（積み方を店ごとに変える）
        if (idx % 3 == 0)
        {
            var crate = new Color(0.25f, 0.4f, 0.55f);
            shop.AddChild(Box(new Vector3(0.5f, 0.3f, 0.4f), new Vector3(1.5f, 0.15f, front + 0.9f * f), crate));
            shop.AddChild(Box(new Vector3(0.5f, 0.3f, 0.4f), new Vector3(1.5f, 0.45f, front + 0.9f * f), crate * 1.15f));
        }

        return shop;
    }

    /// <summary>
    /// 駄菓子屋。商店街で唯一「中が見える・人がいる」店。
    /// 店先をガラス戸で塞がず、台と瓶とおばあさんを置いて、
    /// 近づいて話せる場所にする。町に人が一人もいないのが最大の嘘だった。
    /// </summary>
    private static Node3D BuildDagashiya(Vector3 pos)
    {
        var shop = new Node3D { Name = "Dagashiya", Position = pos };
        var awning = new Color(0.9f, 0.55f, 0.2f);
        float front = 1.55f;

        shop.AddChild(TexBox(new Vector3(3.9f, 3.1f, 3f), new Vector3(0f, 1.55f, 0f), "plaster", new Vector2(2f, 2f)));
        shop.AddChild(Box(new Vector3(3.9f, 0.3f, 3.1f), new Vector3(0f, 3.2f, 0f), ConcreteDark));

        // 店先は開けっ放し。奥を暗くして「中がある」ことを見せる
        shop.AddChild(Box(new Vector3(2.9f, 2.3f, 0.08f), new Vector3(0f, 1.15f, -0.6f), new Color(0.16f, 0.15f, 0.14f)));

        // 駄菓子の台と、色とりどりの瓶
        shop.AddChild(Box(new Vector3(2.9f, 0.1f, 0.85f), new Vector3(0f, 0.85f, front - 0.2f), DarkWood));
        shop.AddChild(Box(new Vector3(2.9f, 0.75f, 0.1f), new Vector3(0f, 0.42f, front + 0.15f), DarkWood));
        Color[] jars =
        {
            new(0.9f, 0.35f, 0.3f), new(0.95f, 0.8f, 0.25f), new(0.45f, 0.7f, 0.9f),
            new(0.55f, 0.8f, 0.45f), new(0.9f, 0.6f, 0.8f), new(0.85f, 0.5f, 0.25f),
        };
        for (int i = 0; i < 6; i++)
        {
            shop.AddChild(MeshI(new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.14f, Height = 0.3f },
                new Vector3(-1.15f + i * 0.46f, 1.05f, front - 0.2f), jars[i]));
        }

        // 日よけと看板
        var tent = MeshI(new BoxMesh { Size = new Vector3(3.7f, 0.08f, 1.3f) },
            new Vector3(0f, 2.5f, front + 0.6f), Mat(awning));
        tent.RotationDegrees = new Vector3(14f, 0f, 0f);
        shop.AddChild(tent);
        shop.AddChild(Box(new Vector3(3.4f, 0.7f, 0.12f), new Vector3(0f, 2.85f, front + 0.05f),
            awning.Lerp(Colors.White, 0.5f)));

        // おばあさん。台の奥に座っている
        var granny = new Node3D { Name = "Granny", Position = new Vector3(0.55f, 0f, front - 1.15f) };
        var kimono = new Color(0.55f, 0.55f, 0.62f);
        granny.AddChild(MeshI(new CapsuleMesh { Radius = 0.24f, Height = 0.62f },
            new Vector3(0f, 0.72f, 0f), kimono));
        granny.AddChild(MeshI(new SphereMesh { Radius = 0.21f, Height = 0.42f },
            new Vector3(0f, 1.16f, 0f), Skin));
        granny.AddChild(MeshI(new SphereMesh { Radius = 0.22f, Height = 0.26f },
            new Vector3(0f, 1.26f, -0.01f), new Color(0.86f, 0.86f, 0.88f)));   // 白髪
        foreach (float ex in new[] { -0.075f, 0.075f })
        {
            granny.AddChild(Box(new Vector3(0.07f, 0.012f, 0.03f), new Vector3(ex, 1.18f, 0.2f),
                new Color(0.15f, 0.13f, 0.12f)));                               // 細めた目
        }
        granny.AddChild(Box(new Vector3(0.5f, 0.35f, 0.35f), new Vector3(0f, 0.22f, 0f), kimono * 0.85f));
        shop.AddChild(granny);

        shop.AddChild(Collider(new Vector3(3.9f, 3f, 3f), new Vector3(0f, 1.5f, 0f)));
        return shop;
    }

    /// <summary>
    /// ラジオ体操の台（折りたたみ机・ラジカセ・出席カードの箱）。
    /// 「8/7で ラジオたいそうは おしまい。はんこが そろった」と game が
    /// 言っておきながら、押す場所がどこにも無かった。
    /// </summary>
    private static void BuildRadioTaiso(Node3D root)
    {
        var t = new Node3D { Name = "RadioTaiso", Position = new Vector3(-16f, 0f, 1.5f) };
        // 折りたたみ机
        t.AddChild(Box(new Vector3(1.5f, 0.06f, 0.65f), new Vector3(0f, 0.72f, 0f), new Color(0.82f, 0.78f, 0.7f)));
        foreach (float lx in new[] { -0.65f, 0.65f })
        {
            t.AddChild(Box(new Vector3(0.05f, 0.7f, 0.05f), new Vector3(lx, 0.36f, -0.25f), ConcreteDark));
            t.AddChild(Box(new Vector3(0.05f, 0.7f, 0.05f), new Vector3(lx, 0.36f, 0.25f), ConcreteDark));
        }
        // ラジカセ
        t.AddChild(Box(new Vector3(0.46f, 0.24f, 0.18f), new Vector3(-0.4f, 0.87f, 0f), new Color(0.2f, 0.2f, 0.22f)));
        t.AddChild(MeshI(new CylinderMesh { TopRadius = 0.075f, BottomRadius = 0.075f, Height = 0.03f },
            new Vector3(-0.52f, 0.87f, -0.1f), new Color(0.45f, 0.45f, 0.48f)));
        t.AddChild(Box(new Vector3(0.02f, 0.3f, 0.02f), new Vector3(-0.2f, 1.12f, 0f), Stone));   // アンテナ
        // 出席カードの箱と朱肉
        t.AddChild(Box(new Vector3(0.3f, 0.1f, 0.22f), new Vector3(0.35f, 0.8f, 0f), new Color(0.9f, 0.88f, 0.8f)));
        t.AddChild(Box(new Vector3(0.09f, 0.05f, 0.09f), new Vector3(0.62f, 0.77f, 0.05f), new Color(0.7f, 0.15f, 0.15f)));

        // のぼり旗。台の前に立って何をやっているか、**近づく前に**分かるようにする。
        // 初日の朝、目の前にあるのに「近づかないと分からない」のでは、
        // 最初の一歩の手がかりにならない。8/1〜8/7 のあいだだけ立てる
        var banners = new Node3D { Name = "Banners" };
        foreach ((float bx, Color cloth) in new[]
                 {
                     (-1.15f, new Color(0.88f, 0.25f, 0.22f)),
                     (1.15f, new Color(0.95f, 0.95f, 0.93f)),
                 })
        {
            // 14m 先の棟間カメラから読める大きさが要る。2.5m/幅0.4 では
            // 点にしか見えなかったので、3.4m/幅0.62 に上げた
            banners.AddChild(MeshI(new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.035f, Height = 3.4f },
                new Vector3(bx, 1.7f, 0f), new Color(0.72f, 0.7f, 0.66f)));
            banners.AddChild(Box(new Vector3(0.04f, 0.035f, 0.66f), new Vector3(bx, 3.34f, 0.31f), new Color(0.72f, 0.7f, 0.66f)));
            // 布。竿の片側に垂らす
            banners.AddChild(Box(new Vector3(0.02f, 2.2f, 0.62f), new Vector3(bx + 0.03f, 2.22f, 0.31f), cloth));
            // 帯（遠目に「文字が入っている」と読めるだけの横線）
            for (int k = 0; k < 3; k++)
            {
                banners.AddChild(Box(new Vector3(0.03f, 0.2f, 0.44f),
                    new Vector3(bx + 0.04f, 2.95f - k * 0.62f, 0.31f),
                    cloth.Lerp(new Color(0.15f, 0.15f, 0.18f), 0.75f)));
            }
            // 土台の重し
            banners.AddChild(MeshI(new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.2f, Height = 0.14f },
                new Vector3(bx, 0.07f, 0f), ConcreteDark));
        }
        t.AddChild(banners);
        root.AddChild(t);
    }

    /// <summary>
    /// 送り火。8/16 の夕方だけ、団地の各棟の入口に小さな火を焚く。
    /// 「8/16 送り火」は朝の台詞にあるのに、その日 町を歩いても
    /// 火が一つも無かった（言って見せない、がまた残っていた）。
    /// </summary>
    private static void BuildOkuribi(Node3D root)
    {
        var group = new Node3D { Name = "Okuribi", Visible = false };
        Vector3[] spots =
        {
            new(-22f, 0f, -2.6f), new(-17f, 0f, -2.6f),
            new(-19f, 0f, 2.6f), new(-13f, 0f, 2.6f),
        };
        for (int i = 0; i < spots.Length; i++)
        {
            var fire = new Node3D { Name = $"Fire{i}", Position = spots[i] };
            // 素焼きの皿と、井桁に組んだおがら
            fire.AddChild(MeshI(new CylinderMesh { TopRadius = 0.26f, BottomRadius = 0.22f, Height = 0.06f },
                new Vector3(0f, 0.03f, 0f), new Color(0.62f, 0.45f, 0.36f)));
            for (int k = 0; k < 4; k++)
            {
                var stick = MeshI(new CylinderMesh { TopRadius = 0.014f, BottomRadius = 0.014f, Height = 0.34f },
                    new Vector3(0f, 0.09f + (k / 2) * 0.045f, 0f), new Color(0.55f, 0.46f, 0.33f));
                stick.RotationDegrees = new Vector3(90f, k % 2 == 0 ? 0f : 90f, 0f);
                stick.Position += new Vector3(0f, 0f, k % 2 == 0 ? 0.05f : 0f);
                fire.AddChild(stick);
            }
            // 炎。外炎と内炎の2枚。SummerMain が毎フレーム大きさを揺らす
            var flame = new Node3D { Name = "Flame", Position = new Vector3(0f, 0.16f, 0f) };
            flame.AddChild(MeshI(new CylinderMesh { TopRadius = 0f, BottomRadius = 0.13f, Height = 0.42f },
                new Vector3(0f, 0.21f, 0f), new Color(0.98f, 0.55f, 0.15f), unshaded: true));
            flame.AddChild(MeshI(new CylinderMesh { TopRadius = 0f, BottomRadius = 0.07f, Height = 0.26f },
                new Vector3(0f, 0.13f, 0f), new Color(1f, 0.92f, 0.6f), unshaded: true));
            fire.AddChild(flame);
            fire.AddChild(new OmniLight3D
            {
                Name = "Light",
                Position = new Vector3(0f, 0.4f, 0f),
                LightColor = new Color(1f, 0.68f, 0.34f),
                LightEnergy = 2.2f,
                OmniRange = 5.5f,
                ShadowEnabled = false,
            });
            group.AddChild(fire);
        }
        root.AddChild(group);
    }

    /// <summary>
    /// 町内会の掲示板と、立てかけた自転車。
    /// 塞いでいた木をどけたら、大通りの画の右半分が芝だけの空白になった。
    /// 中景に人の暮らしの物が要る。ラジオ体操の予定表や回覧板が貼ってある板。
    /// </summary>
    private static void BuildNoticeBoard(Node3D root)
    {
        var n = new Node3D { Name = "NoticeBoard", Position = new Vector3(5.2f, 0f, 3.4f) };
        n.RotationDegrees = new Vector3(0f, -28f, 0f);

        foreach (float px in new[] { -0.85f, 0.85f })
            n.AddChild(Box(new Vector3(0.1f, 1.9f, 0.1f), new Vector3(px, 0.95f, 0f), DarkWood));
        n.AddChild(Box(new Vector3(1.95f, 1.05f, 0.07f), new Vector3(0f, 1.35f, 0f), new Color(0.75f, 0.72f, 0.66f)));
        // 小さな屋根（雨よけ）
        var cap = MeshI(new BoxMesh { Size = new Vector3(2.15f, 0.06f, 0.42f) },
            new Vector3(0f, 1.95f, -0.1f), TexMat("roof", new Vector2(2f, 1f)));
        cap.RotationDegrees = new Vector3(-16f, 0f, 0f);
        n.AddChild(cap);
        // 貼り紙。大きさと色を散らす
        Color[] paper = { new(0.96f, 0.95f, 0.92f), new(0.95f, 0.88f, 0.5f), new(0.9f, 0.93f, 0.96f) };
        for (int i = 0; i < 4; i++)
        {
            var note = Box(new Vector3(0.36f + (i % 2) * 0.1f, 0.44f, 0.02f),
                new Vector3(-0.62f + i * 0.42f, 1.3f + (i % 2) * 0.1f, 0.05f), paper[i % 3]);
            note.RotationDegrees = new Vector3(0f, 0f, i % 2 == 0 ? 2.5f : -2f);
            n.AddChild(note);
        }

        // 立てかけた自転車（駐輪場のものと同じ作り）
        var bike = new Node3D { Position = new Vector3(1.5f, 0f, 0.5f), RotationDegrees = new Vector3(0f, 74f, 8f) };
        var body = new Color(0.28f, 0.34f, 0.46f);
        bike.AddChild(Box(new Vector3(0.08f, 0.55f, 1.3f), new Vector3(0f, 0.5f, 0f), body));
        bike.AddChild(Box(new Vector3(0.5f, 0.06f, 0.06f), new Vector3(0f, 0.95f, -0.5f), body));
        bike.AddChild(Box(new Vector3(0.28f, 0.05f, 0.2f), new Vector3(0f, 0.82f, 0.35f), DarkWood));  // 荷台
        foreach (float wz in new[] { -0.55f, 0.55f })
        {
            var wheel = MeshI(new TorusMesh { InnerRadius = 0.24f, OuterRadius = 0.3f },
                new Vector3(0f, 0.3f, wz), new Color(0.14f, 0.14f, 0.15f));
            wheel.RotationDegrees = new Vector3(90f, 0f, 0f);
            bike.AddChild(wheel);
        }
        n.AddChild(bike);
        // 掲示板は開けた芝の上なので、当たり判定を付けても挟まれる心配がない
        // （人に付けなかったのは狭い通路で動けなくなるため。事情が違う）
        n.AddChild(Collider(new Vector3(2.1f, 1.9f, 0.35f), new Vector3(0f, 0.95f, 0f)));
        root.AddChild(n);
    }

    /// <summary>
    /// 夏まつりの屋台と提灯。8/24 だけ出す（SummerMain が Visible を切り替える）。
    /// 駄菓子屋のおばあさんが「うちも 屋台を 出すよ」と言うのに、
    /// 当日ずっと何も出ていなかった。
    /// </summary>
    private static void BuildFestival(Node3D root)
    {
        var f = new Node3D { Name = "Festival", Visible = false };

        // 提灯。商店街の通路に沿って吊るす
        var lampMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.72f, 0.42f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        // 東端まで吊ると、望遠カメラ（x=25）の 10m 手前に来た1個が
        // 画面上部を横切る太い棒になる。x=8 までにする
        // 提灯と裸電球は「灯り」なので、昼は紙のまま・夜に灯る。
        // Lights の下にまとめて、SummerMain が時刻で色を動かす
        var fesLights = new Node3D { Name = "Lights" };
        for (int i = 0; i < 13; i++)
        {
            float lx = -17f + i * 2.1f;
            fesLights.AddChild(MeshI(new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.22f },
                new Vector3(lx, 3.5f, 15.9f), lampMat.Duplicate() as StandardMaterial3D, noShadow: true));
            f.AddChild(Box(new Vector3(0.02f, 0.35f, 0.02f), new Vector3(lx, 3.78f, 15.9f), ConcreteDark));
        }
        f.AddChild(fesLights);

        // 屋台3軒。天幕・台・のれん・裸電球
        (float X, Color Cloth, Color Goods)[] stalls =
        {
            (-7.5f, new Color(0.85f, 0.3f, 0.28f), new Color(0.9f, 0.25f, 0.3f)),   // りんご飴
            (0.5f, new Color(0.35f, 0.55f, 0.8f), new Color(0.75f, 0.9f, 0.95f)),   // かき氷
            (7.5f, new Color(0.9f, 0.85f, 0.4f), new Color(0.95f, 0.55f, 0.25f)),   // 金魚すくい
        };
        foreach ((float sx, Color cloth, Color goods) in stalls)
        {
            var st = new Node3D { Position = new Vector3(sx, 0f, 16.6f) };
            st.AddChild(Box(new Vector3(2.4f, 0.1f, 0.9f), new Vector3(0f, 0.86f, 0f), DarkWood));
            st.AddChild(Box(new Vector3(2.4f, 0.8f, 0.08f), new Vector3(0f, 0.44f, -0.4f), DarkWood));
            foreach (float px in new[] { -1.1f, 1.1f })
            {
                st.AddChild(Box(new Vector3(0.07f, 2.1f, 0.07f), new Vector3(px, 1.05f, -0.4f), ConcreteDark));
                st.AddChild(Box(new Vector3(0.07f, 2.1f, 0.07f), new Vector3(px, 1.05f, 0.4f), ConcreteDark));
            }
            var roof = MeshI(new BoxMesh { Size = new Vector3(2.8f, 0.08f, 1.4f) },
                new Vector3(0f, 2.15f, 0f), Mat(cloth));
            st.AddChild(roof);
            // 紅白の垂れ幕
            for (int k = 0; k < 4; k++)
            {
                st.AddChild(Box(new Vector3(0.62f, 0.4f, 0.03f),
                    new Vector3(-0.93f + k * 0.62f, 1.9f, 0.68f),
                    k % 2 == 0 ? cloth : new Color(0.96f, 0.95f, 0.92f)));
            }
            // 台の上の品
            for (int k = 0; k < 5; k++)
            {
                st.AddChild(MeshI(new SphereMesh { Radius = 0.09f, Height = 0.18f },
                    new Vector3(-0.8f + k * 0.4f, 1.0f, 0.05f), goods));
            }
            // 裸電球（これも Lights の下に置く。位置は屋台の中なので絶対座標で）
            fesLights.AddChild(MeshI(new SphereMesh { Radius = 0.07f, Height = 0.14f },
                new Vector3(sx, 1.95f, 16.8f), new Color(1f, 0.95f, 0.75f), unshaded: true));
            f.AddChild(st);
        }
        root.AddChild(f);
    }

    /// <summary>
    /// 樹液の跡。空き地そばの木 (19,0,5) の幹に、黒く濡れた染みと
    /// 集まる小虫を付ける。ここに甲虫が来るという手がかりが要る。
    /// </summary>
    private static void BuildSapMark(Node3D root)
    {
        var m = new Node3D { Name = "SapTree", Position = new Vector3(19f, 0f, 5f) };
        var wet = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.08f, 0.05f),
            Roughness = 0.15f,
            Metallic = 0.35f,
        };
        // 幹（半径 0.2 前後）に沿わせた縦長の染み
        // 円筒で巻くと「幹に黒い輪をはめた」絵になった（実際そう見えた）。
        // 幹の一面に貼る板にして、樹液が流れた跡として縦に垂らす
        (float Ang, float Y, float H, float W)[] runs =
        {
            (0.0f, 1.02f, 0.62f, 0.2f), (0.55f, 0.86f, 0.4f, 0.13f), (-0.7f, 0.72f, 0.3f, 0.1f),
        };
        foreach ((float ang, float y, float h, float w) in runs)
        {
            var slab = MeshI(new BoxMesh { Size = new Vector3(w, h, 0.03f) },
                new Vector3(Mathf.Sin(ang) * 0.24f, y, Mathf.Cos(ang) * 0.24f), wet);
            slab.RotationDegrees = new Vector3(0f, ang * 57.3f, 0f);
            m.AddChild(slab);
        }
        // 集まっている小虫。近づくと分かる程度の点
        for (int i = 0; i < 6; i++)
        {
            float th = i * 1.05f;
            m.AddChild(MeshI(new SphereMesh { Radius = 0.014f, Height = 0.022f },
                new Vector3(Mathf.Cos(th) * 0.26f, 0.8f + (i % 3) * 0.2f, Mathf.Sin(th) * 0.26f),
                new Color(0.2f, 0.18f, 0.14f)));
        }
        root.AddChild(m);
    }

    /// <summary>
    /// 座っている人・立ち話をしている人。動かないが、居るだけで町が変わる。
    /// 商店街も棟間も無人で、平日の昼間なのにゴーストタウンに見えていた。
    /// 主人公と同じ組み立て（胴・頭・腕）で、服の色と姿勢だけ変える。
    /// </summary>
    private static Node3D Resident(string name, Vector3 pos, float rotY, Color wear, bool seated)
    {
        var r = new Node3D { Name = name, Position = pos, RotationDegrees = new Vector3(0f, rotY, 0f) };
        float hip = seated ? 0.42f : 0.4f;
        float chest = seated ? 0.78f : 0.9f;

        r.AddChild(MeshI(new CapsuleMesh { Radius = 0.23f, Height = 0.66f },
            new Vector3(0f, chest, 0f), wear));
        r.AddChild(MeshI(new SphereMesh { Radius = 0.2f, Height = 0.4f },
            new Vector3(0f, chest + 0.42f, 0f), Skin));
        // 髪（後頭部だけ）。真横や後ろから見たときに頭が球のままにならないように
        r.AddChild(MeshI(new SphereMesh { Radius = 0.205f, Height = 0.3f },
            new Vector3(0f, chest + 0.5f, 0.02f), new Color(0.22f, 0.2f, 0.2f)));

        foreach (float ax in new[] { -0.26f, 0.26f })
        {
            var arm = MeshI(new CapsuleMesh { Radius = 0.06f, Height = 0.36f },
                new Vector3(ax, chest - 0.06f, seated ? -0.1f : 0f), Skin);
            arm.RotationDegrees = new Vector3(seated ? -35f : 6f, 0f, 0f);
            r.AddChild(arm);
        }
        foreach (float lx in new[] { -0.12f, 0.12f })
        {
            var leg = MeshI(new CapsuleMesh { Radius = 0.075f, Height = 0.38f },
                seated ? new Vector3(lx, hip - 0.02f, -0.24f) : new Vector3(lx, hip - 0.2f, 0f),
                seated ? new Color(0.32f, 0.34f, 0.4f) : new Color(0.28f, 0.3f, 0.36f));
            leg.RotationDegrees = new Vector3(seated ? -78f : 0f, 0f, 0f);
            r.AddChild(leg);
        }
        return r;
    }

    private static void BuildResidents(Node3D root)
    {
        var people = new Node3D { Name = "Residents" };

        // 商店街のベンチに座って新聞を読んでいるおじいさん
        // 望遠カメラ（x=25）から 34m 先だと点になる。26m ほどの位置に置く
        var bench = new Node3D { Position = new Vector3(-1.2f, 0f, 15.3f) };
        bench.AddChild(Box(new Vector3(1.6f, 0.08f, 0.42f), new Vector3(0f, 0.42f, 0f), DarkWood));
        bench.AddChild(Box(new Vector3(1.6f, 0.4f, 0.07f), new Vector3(0f, 0.62f, 0.2f), DarkWood));
        foreach (float bx in new[] { -0.65f, 0.65f })
            bench.AddChild(Box(new Vector3(0.08f, 0.42f, 0.36f), new Vector3(bx, 0.21f, 0f), ConcreteDark));
        people.AddChild(bench);
        var grandpa = Resident("Grandpa", new Vector3(-1.5f, 0f, 15.2f), 172f,
                               new Color(0.78f, 0.78f, 0.74f), seated: true);
        grandpa.AddChild(Box(new Vector3(0.34f, 0.02f, 0.26f), new Vector3(0f, 0.86f, -0.28f),
                             new Color(0.9f, 0.89f, 0.84f)));   // 新聞
        people.AddChild(grandpa);

        // 団地の広場で立ち話をしている二人（買い物かごを提げている）
        var talkA = Resident("Neighbor1", new Vector3(-18.6f, 0f, -1.1f), 118f,
                             new Color(0.7f, 0.5f, 0.55f), seated: false);
        talkA.AddChild(Box(new Vector3(0.26f, 0.22f, 0.16f), new Vector3(0.3f, 0.55f, 0.06f),
                           new Color(0.85f, 0.82f, 0.7f)));      // 買い物かご
        people.AddChild(talkA);
        people.AddChild(Resident("Neighbor2", new Vector3(-19.6f, 0f, -0.2f), -62f,
                                 new Color(0.5f, 0.58f, 0.66f), seated: false));

        root.AddChild(people);
    }

    /// <summary>
    /// あさがおの鉢（夏休みの観察日記）。8/7 でラジオ体操が終わると
    /// 朝にすることが無くなるので、31日かけて育つものを置く。
    /// 段階ごとの部品を全部作っておき、SummerMain がその日の分だけ表示する。
    /// </summary>
    private static void BuildAsagao(Node3D root)
    {
        // 生垣（z=±1.95）の外に置くと、寄って見ることも近づくこともできない。
        // 通路の中、北の生垣のすぐ手前に置く
        var a = new Node3D { Name = "Asagao", Position = new Vector3(-13.6f, 0f, 1.35f) };
        var terracotta = new Color(0.66f, 0.36f, 0.24f);

        // 鉢と土（いつでも出す）
        a.AddChild(MeshI(new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.24f, Height = 0.34f },
            new Vector3(0f, 0.17f, 0f), terracotta));
        a.AddChild(MeshI(new CylinderMesh { TopRadius = 0.34f, BottomRadius = 0.34f, Height = 0.05f },
            new Vector3(0f, 0.345f, 0f), terracotta * 0.9f));
        a.AddChild(MeshI(new CylinderMesh { TopRadius = 0.29f, BottomRadius = 0.29f, Height = 0.03f },
            new Vector3(0f, 0.35f, 0f), TexMat("dirt", new Vector2(1f, 1f))));

        // 支柱（あんどん仕立ての3本）。つるが伸びてから出す
        var poles = new Node3D { Name = "Poles" };
        for (int i = 0; i < 3; i++)
        {
            float th = i * Mathf.Tau / 3f;
            var pole = MeshI(new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.014f, Height = 1.15f },
                new Vector3(Mathf.Cos(th) * 0.2f, 0.92f, Mathf.Sin(th) * 0.2f),
                new Color(0.72f, 0.64f, 0.42f));
            pole.RotationDegrees = new Vector3(Mathf.Sin(th) * 5f, 0f, -Mathf.Cos(th) * 5f);
            poles.AddChild(pole);
        }
        a.AddChild(poles);

        // 双葉。芽が出た直後の姿。つるが伸びるまでの数日はこれだけ
        var futaba = new Node3D { Name = "Futaba" };
        futaba.AddChild(MeshI(new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.01f, Height = 0.1f },
            new Vector3(0f, 0.41f, 0f), new Color(0.45f, 0.6f, 0.34f)));
        foreach (float fx in new[] { -0.06f, 0.06f })
        {
            var leaf = MeshI(new SphereMesh { Radius = 0.055f, Height = 0.02f },
                new Vector3(fx, 0.46f, 0f), new Color(0.52f, 0.72f, 0.4f));
            leaf.Scale = new Vector3(1f, 1f, 0.7f);
            futaba.AddChild(leaf);
        }
        a.AddChild(futaba);

        // つる。下から順に見せていくので、1節ずつ別ノードにする
        var vine = new Node3D { Name = "Vine" };
        var leafMat = TexMat(BestTex("gen/TEX-leaf_canopy.jpg", "leaf"), new Vector2(1f, 1f),
                             new Color(0.5f, 0.72f, 0.42f));
        for (int i = 0; i < 12; i++)
        {
            float t = i / 12f;
            float th = t * Mathf.Tau * 1.6f;
            float y = 0.4f + t * 1.25f;
            var node = new Node3D { Name = $"V{i}", Position = new Vector3(Mathf.Cos(th) * 0.19f, y, Mathf.Sin(th) * 0.19f) };
            node.AddChild(MeshI(new CylinderMesh { TopRadius = 0.011f, BottomRadius = 0.013f, Height = 0.14f },
                Vector3.Zero, new Color(0.42f, 0.58f, 0.32f)));
            // 葉は交互に外へ振り出す
            var leaf = MeshI(new BoxMesh { Size = new Vector3(0.2f, 0.012f, 0.17f) },
                new Vector3(Mathf.Cos(th) * 0.14f, 0.03f, Mathf.Sin(th) * 0.14f), leafMat);
            leaf.RotationDegrees = new Vector3(-18f, th * 57.3f, 0f);
            node.AddChild(leaf);
            vine.AddChild(node);
        }
        a.AddChild(vine);

        // つぼみと花。日付で数を変えるので、こちらも1つずつ別ノード
        var buds = new Node3D { Name = "Buds" };
        var flowers = new Node3D { Name = "Flowers" };
        Color[] petal = { new(0.45f, 0.4f, 0.78f), new(0.72f, 0.45f, 0.8f), new(0.4f, 0.55f, 0.85f) };
        for (int i = 0; i < 5; i++)
        {
            float th = i * 1.31f;
            var at = new Vector3(Mathf.Cos(th) * 0.24f, 1.02f + (i % 3) * 0.16f, Mathf.Sin(th) * 0.24f);

            var bud = MeshI(new CapsuleMesh { Radius = 0.026f, Height = 0.1f }, at, new Color(0.5f, 0.62f, 0.4f));
            bud.RotationDegrees = new Vector3(28f, 0f, 0f);
            bud.Name = $"B{i}";
            buds.AddChild(bud);

            var f = new Node3D { Name = $"F{i}", Position = at };
            // 朝顔はラッパ。上が開いた円錐で表す
            var cup = MeshI(new CylinderMesh { TopRadius = 0.105f, BottomRadius = 0.012f, Height = 0.11f },
                new Vector3(0f, 0.05f, 0f), petal[i % 3]);
            cup.RotationDegrees = new Vector3(20f, 0f, 0f);
            f.AddChild(cup);
            f.AddChild(MeshI(new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.01f, Height = 0.03f },
                new Vector3(0f, 0.1f, 0f), new Color(0.95f, 0.95f, 0.9f)));
            flowers.AddChild(f);
        }
        a.AddChild(buds);
        a.AddChild(flowers);
        root.AddChild(a);
    }

    /// <summary>
    /// 水たまり。雨の日だけ出す（SummerMain が Visible を切り替える）。
    /// 雨粒の線だけでは「雨の日」に見えず、地面に水が残って初めて伝わる。
    /// 舗装のくぼみに溜まるものなので、道と広場の上にだけ置く。
    /// </summary>
    private static void BuildPuddles(Node3D root)
    {
        var group = new Node3D { Name = "Puddles", Visible = false };
        var mat = new StandardMaterial3D
        {
            // 0.85 では舗装が隠れて「置いた金属板」に見えた（監査 #11）。透かす
            AlbedoColor = new Color(0.36f, 0.40f, 0.45f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.05f,
            Metallic = 0.5f,
            NormalEnabled = true,
            NormalTexture = GD.Load<Texture2D>("res://assets/textures/water_normal.png"),
            NormalScale = 0.35f,
            Uv1Scale = new Vector3(2f, 2f, 1f),
        };
        // (x, z, 長径, 短径)。大通り・歩道・団地の広場・商店街の通路
        (float X, float Z, float W, float H)[] spots =
        {
            (-3f, 8.4f, 3.2f, 1.5f), (9f, 7.6f, 2.4f, 1.1f), (-17f, 8.2f, 2.8f, 1.3f),
            (-12f, 2.5f, 1.8f, 1.0f), (-16f, -1.5f, 2.2f, 1.2f),
            (2f, -3.5f, 1.6f, 1.4f), (-4f, 13.6f, 2.0f, 1.1f), (6f, 15.9f, 2.6f, 1.2f),
            (20f, 8.6f, 2.2f, 1.2f),
        };
        foreach ((float x, float z, float w, float h) in spots)
        {
            group.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.01f, RadialSegments = 20,
                },
                MaterialOverride = mat,
                Position = new Vector3(x, 0.075f, z),
                Scale = new Vector3(w, 1f, h),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        root.AddChild(group);
    }

    // --- 公園（池・すべり台・ブランコ・砂場・ベンチ） ---

    private static void BuildPark(Node3D root)
    {
        var park = new Node3D { Name = "Park" };

        // 参考画（docs/reference/park_pond_gpt.png）の池: 濁った緑がかった水が
        // 石張りの低いふちの上端近くまで張っている。青いプールの水ではない
        var waterMat = new StandardMaterial3D
        {
            // 金属にすると空を映して白い円盤になった（撮って確認）。濁った水は
            // 反射より色で見せる
            // 反射ゼロだと土の円盤に見えた。少しだけ空を映し、底の暗さを透かす
            // 参考画の水は (84,94,60) の濁ったオリーブ。鏡面 0.45 では空が全面に乗って
            // ターコイズ (121,164,158) になった（監査で実測）。鏡面を絞って色で見せる
            // 鏡面 0.12・粗さ 0.5 では波も艶も消えて緑の円盤になった。中間に置く
            AlbedoColor = new Color(0.2f, 0.27f, 0.15f, 0.94f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.4f,
            Metallic = 0.03f,
            MetallicSpecular = 0.25f,
            NormalEnabled = true,
            NormalTexture = GD.Load<Texture2D>("res://assets/textures/water_normal.png"),
            NormalScale = 1.2f,
            Uv1Scale = new Vector3(2.5f, 2.5f, 1f),
        };
        var water = MeshI(new CylinderMesh { TopRadius = 6.4f, BottomRadius = 6.4f, Height = 0.06f },
            new Vector3(6f, 0.09f, -15f), waterMat);
        water.Name = "PondWater";   // SummerMain が時刻で色を掛ける
        park.AddChild(water);
        // ふち: 玉石を目地で固めた、丸みのある低い壁（高さ 0.32m・幅 0.9m）。
        // 土のトーラスは「盛り土」にしか見えなかった
        var rim = MeshI(new TorusMesh { InnerRadius = 6.2f, OuterRadius = 7.1f, Rings = 64, RingSegments = 24 },
            // 参考画のふちは玉石ではなく砂利の洗い出しコンクリート（粒は数 mm、暖かい灰茶）。
            // 玉石を貼ると石垣に読めた
            new Vector3(6f, 0.02f, -15f), TexMat("photo/gravel_concrete.jpg", new Vector2(60f, 3f), new Color(0.56f, 0.54f, 0.47f), roughness: 1f));
        rim.Scale = new Vector3(1f, 0.7f, 1f);
        park.AddChild(rim);
        // ふちの内側の立ち上がり（水面との境が線で見えるように）
        // 蓋を付けたままだと、この円柱の上面が池全体を石の円盤で覆う（撮って気づいた）
        park.AddChild(MeshI(new CylinderMesh { TopRadius = 6.25f, BottomRadius = 6.25f, Height = 0.3f, CapTop = false, CapBottom = false },
            new Vector3(6f, 0.02f, -15f), TexMat("photo/gravel_concrete.jpg", new Vector2(60f, 0.8f), new Color(0.44f, 0.43f, 0.38f), roughness: 1f)));
        park.AddChild(MeshI(new CylinderMesh { TopRadius = 6.1f, BottomRadius = 6.1f, Height = 0.2f },
            new Vector3(6f, -0.1f, -15f), Mat(new Color(0.1f, 0.12f, 0.08f))));   // 水の下の暗い底

        // 池には入れない（ふちの内側に丸い当たり）。動画で主人公がふちの上を
        // 歩いていた。ふちの外側の帯（6.85〜8.8m）が釣りの立ち位置
        var pondBody = new StaticBody3D { Position = new Vector3(6f, 0.5f, -15f) };
        pondBody.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 6.7f, Height = 1f } });
        park.AddChild(pondBody);
        Node3D slideNode = BuildSlide(new Vector3(-6f, 0f, -11f));
        slideNode.RotationDegrees = new Vector3(0f, 180f, 0f);   // 参考画は右にはしご・左へ降りる
        park.AddChild(slideNode);

        var swing = new Node3D { Name = "Swing", Position = new Vector3(-2f, 0f, -17f) };
        var frame = new Color(0.35f, 0.55f, 0.65f);
        swing.AddChild(Box(new Vector3(0.15f, 2.4f, 0.15f), new Vector3(-1.6f, 1.2f, 0f), frame));
        swing.AddChild(Box(new Vector3(0.15f, 2.4f, 0.15f), new Vector3(1.6f, 1.2f, 0f), frame));
        swing.AddChild(Box(new Vector3(3.5f, 0.15f, 0.15f), new Vector3(0f, 2.4f, 0f), frame));
        // 座席と鎖は上の横棒を支点にした節にまとめる。乗ると SummerMain が節ごと揺らす
        int seatIdx = 0;
        foreach (float sx in new[] { -0.8f, 0.8f })
        {
            var pivot = new Node3D { Name = $"Seat{seatIdx++}", Position = new Vector3(sx, 2.4f, 0f) };
            pivot.AddChild(Box(new Vector3(0.05f, 1.8f, 0.05f), new Vector3(-0.25f, -0.95f, 0f), ConcreteDark));
            pivot.AddChild(Box(new Vector3(0.05f, 1.8f, 0.05f), new Vector3(0.25f, -0.95f, 0f), ConcreteDark));
            pivot.AddChild(Box(new Vector3(0.6f, 0.06f, 0.25f), new Vector3(0f, -1.85f, 0f), DarkWood));
            swing.AddChild(pivot);
        }
        park.AddChild(swing);

        // 集会所の生垣（z=-8.4）と重なっていたので南東へ。砂は参考画の明るいベージュ
        var sandbox = new Node3D { Position = new Vector3(16f, 0f, -10f) };
        sandbox.AddChild(MeshI(new BoxMesh { Size = new Vector3(3.6f, 0.08f, 3.6f) }, new Vector3(0f, 0.05f, 0f),
            TexMat("dirt", new Vector2(3f, 3f), new Color(1.25f, 1.2f, 1.0f))));
        sandbox.AddChild(Box(new Vector3(4f, 0.25f, 0.25f), new Vector3(0f, 0.12f, -1.9f), DarkWood));
        sandbox.AddChild(Box(new Vector3(4f, 0.25f, 0.25f), new Vector3(0f, 0.12f, 1.9f), DarkWood));
        sandbox.AddChild(Box(new Vector3(0.25f, 0.25f, 3.6f), new Vector3(-1.9f, 0.12f, 0f), DarkWood));
        sandbox.AddChild(Box(new Vector3(0.25f, 0.25f, 3.6f), new Vector3(1.9f, 0.12f, 0f), DarkWood));
        park.AddChild(sandbox);
        // 園路: 砂場の脇から池の北を回って団地の広場へ。参考画の左から奥へ延びる
        // コンクリートの小道。池の手前は芝のまま
        park.AddChild(TexBox(new Vector3(26f, 0.05f, 1.5f), new Vector3(1f, 0.028f, -7.4f),
            "photo/concrete_floor_01.jpg", new Vector2(14f, 1f)));
        park.AddChild(TexBox(new Vector3(1.5f, 0.05f, 4f), new Vector3(-9f, 0.028f, -5.2f),
            "photo/concrete_floor_01.jpg", new Vector2(1f, 2.5f)));
        park.AddChild(BuildParkLamp(new Vector3(11f, 0f, -8.8f)));
        // 芝のまだら。参考画の芝は踏まれた所と伸びた所で色が違う。
        // 一様な緑の上に、薄い濃緑の斑を寝かせる
        var mottle = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.36f, 0.3f, 0.12f, 0.42f),   // 踏まれて土が透けた茶の斑
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 1f,
        };
        (float X, float Z, float W, float H, float Rot)[] patches =
        {
            (14f, -19f, 3.2f, 1.6f, 20f), (9f, -23.5f, 4f, 1.8f, -15f), (-2f, -21f, 3.5f, 2.2f, 40f),
            (-8f, -14f, 2.6f, 1.4f, 10f), (2f, -8.5f, 3f, 1.5f, -30f), (16f, -12f, 2.4f, 2.4f, 0f),
            (-12f, -20f, 3.8f, 1.6f, 55f), (6f, -4.5f, 2.8f, 1.3f, -50f),
        };
        // ふちの外周は一番踏まれる。薄い土色の輪を一枚寝かせる（参考画の踏み跡）
        var worn = MeshI(new TorusMesh { InnerRadius = 7.1f, OuterRadius = 8.3f, Rings = 48, RingSegments = 6 },
            new Vector3(6f, -0.55f, -15f), new StandardMaterial3D
            {
                AlbedoColor = new Color(0.45f, 0.4f, 0.22f, 0.32f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 1f,
            });
        worn.Scale = new Vector3(1f, 0.95f, 1f);   // 上面だけ地面から 0.02 覗く
        worn.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        park.AddChild(worn);
        foreach ((float x, float z, float w, float h, float rot) in patches)
        {
            var patch = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.01f, RadialSegments = 14 },
                MaterialOverride = mottle,
                Position = new Vector3(x, 0.012f, z),
                Scale = new Vector3(w, 1f, h),
                RotationDegrees = new Vector3(0f, rot, 0f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            park.AddChild(patch);
        }
        // 位置は2つのカメラの画角から決めた。(9,-0.5) だと大通りカメラ（CamPlaza）の
        // 右4割を無地の壁で塞いだ。(12,-4) なら CamPark の左端に残り、CamPlaza には
        // 端に少し掛かるだけ
        park.AddChild(BuildCommunityHouse(new Vector3(12f, 0f, -4f)));

        foreach (float bx in new[] { -10f, 16f })
        {
            var bench = new Node3D { Position = new Vector3(bx, 0f, -16f) };
            bench.AddChild(Box(new Vector3(1.8f, 0.08f, 0.5f), new Vector3(0f, 0.45f, 0f), DarkWood));
            bench.AddChild(Box(new Vector3(0.15f, 0.45f, 0.45f), new Vector3(-0.7f, 0.22f, 0f), ConcreteDark));
            bench.AddChild(Box(new Vector3(0.15f, 0.45f, 0.45f), new Vector3(0.7f, 0.22f, 0f), ConcreteDark));
            park.AddChild(bench);
        }
        // 参考画の右奥に写る白い 4 階建て。団地の南西、遠景の手前
        var white4 = new Node3D { Name = "WhiteBlock", Position = new Vector3(-34f, 0f, -17f), RotationDegrees = new Vector3(0f, 20f, 0f) };
        var whiteWall = TexMat("plaster", new Vector2(6f, 4f), new Color(0.93f, 0.92f, 0.88f), roughness: 0.9f);
        white4.AddChild(MeshI(new BoxMesh { Size = new Vector3(14f, 11.6f, 8f) }, new Vector3(0f, 5.8f, 0f), whiteWall));
        white4.AddChild(Box(new Vector3(14.4f, 0.3f, 8.4f), new Vector3(0f, 11.75f, 0f), new Color(0.6f, 0.6f, 0.58f)));
        for (int f = 0; f < 4; f++)
        {
            for (int u = 0; u < 5; u++)
            {
                white4.AddChild(Box(new Vector3(1.6f, 1.3f, 0.08f), new Vector3(-5.6f + u * 2.8f, f * 2.9f + 1.7f, 4.02f), ShadowGlass));
                white4.AddChild(Box(new Vector3(1.6f, 1.3f, 0.08f), new Vector3(-5.6f + u * 2.8f, f * 2.9f + 1.7f, -4.02f), ShadowGlass));
            }
        }
        white4.AddChild(Collider(new Vector3(14f, 11.6f, 8f), new Vector3(0f, 5.8f, 0f)));
        park.AddChild(white4);
        root.AddChild(park);
    }

    /// <summary>
    /// 参考画のすべり台: 水色の鋼管のはしごと手すり、ステンレスの滑走面、
    /// 上に小さな踊り場。段を積んだ石ではなく、鉄の遊具として作る。
    /// </summary>
    private static Node3D BuildSlide(Vector3 pos)
    {
        var slide = new Node3D { Position = pos };
        var blue = new Color(0.2f, 0.45f, 0.8f);
        var steelMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.78f, 0.8f, 0.82f), Roughness = 0.35f, Metallic = 0.6f,
        };
        const float top = 2.0f;   // 踊り場の高さ
        // はしご（+z 側）: 2本の支柱と5段の横桟、手すり
        foreach (float sx in new[] { -0.4f, 0.4f })
        {
            var post = MeshI(new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.035f, Height = 2.75f },
                new Vector3(sx, 1.3f, 0.95f), Mat(blue));
            post.RotationDegrees = new Vector3(-18f, 0f, 0f);
            slide.AddChild(post);
            slide.AddChild(MeshI(new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = top + 0.9f },
                new Vector3(sx, (top + 0.9f) / 2f, -0.35f), Mat(blue)));                 // 踊り場の柱
            slide.AddChild(Box(new Vector3(0.05f, 0.05f, 1.35f), new Vector3(sx, top + 0.85f, 0.3f), blue)); // 手すり上
        }
        for (int r = 0; r < 5; r++)
        {
            float t = (r + 1) / 6f;
            var rung = MeshI(new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.8f },
                new Vector3(0f, t * top, 1.35f - t * 1.4f), Mat(steelMat.AlbedoColor));
            rung.RotationDegrees = new Vector3(0f, 0f, 90f);
            slide.AddChild(rung);
        }
        // 踊り場と背もたれ
        slide.AddChild(Box(new Vector3(0.9f, 0.06f, 0.8f), new Vector3(0f, top, -0.35f), new Color(0.7f, 0.72f, 0.74f)));
        slide.AddChild(Box(new Vector3(0.9f, 0.05f, 0.05f), new Vector3(0f, top + 0.85f, -0.75f), blue));
        // 滑走面（-z へ降りる）: 板と両側の立ち上がり、下端の支え
        float len = 3.6f;
        float angle = -28f;
        var chute = new Node3D { Position = new Vector3(0f, top - 0.02f, -0.75f), RotationDegrees = new Vector3(angle, 0f, 0f) };
        chute.AddChild(MeshI(new BoxMesh { Size = new Vector3(0.7f, 0.05f, len) }, new Vector3(0f, 0f, -len / 2f), steelMat));
        foreach (float sx in new[] { -0.36f, 0.36f })
            chute.AddChild(MeshI(new BoxMesh { Size = new Vector3(0.04f, 0.22f, len) }, new Vector3(sx, 0.1f, -len / 2f), steelMat));
        slide.AddChild(chute);
        float endZ = -0.75f - len * Mathf.Cos(Mathf.DegToRad(-angle));
        float endY = top - len * Mathf.Sin(Mathf.DegToRad(-angle));
        foreach (float sx in new[] { -0.3f, 0.3f })
        {
            slide.AddChild(MeshI(new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = endY },
                new Vector3(sx, endY / 2f, endZ + 0.3f), Mat(blue)));
        }
        slide.AddChild(Collider(new Vector3(1.1f, 2.2f, 2.4f), new Vector3(0f, 1.1f, 0.2f)));
        return slide;
    }

    /// <summary>
    /// 参考画の左に写る白い2階建て（集会所）。上階に白い手すりのベランダ、
    /// 前に刈り込んだ生垣。団地の灰色と対になる白い箱があると、公園の絵が締まる。
    /// </summary>
    private static Node3D BuildCommunityHouse(Vector3 pos)
    {
        var house = new Node3D { Name = "CommunityHouse", Position = pos };
        var white = TexMat("plaster", new Vector2(4f, 2f), new Color(0.96f, 0.95f, 0.92f), roughness: 0.9f);
        const float w = 7f, d = 5f, fh = 2.9f;
        house.AddChild(MeshI(new BoxMesh { Size = new Vector3(w, fh * 2f, d) }, new Vector3(0f, fh, 0f), white));
        house.AddChild(Box(new Vector3(w + 0.3f, 0.25f, d + 0.3f), new Vector3(0f, fh * 2f + 0.12f, 0f), new Color(0.6f, 0.6f, 0.58f)));
        // 上階ベランダ（-z が公園側）
        house.AddChild(Box(new Vector3(w - 0.6f, 0.14f, 1.1f), new Vector3(0f, fh + 0.07f, -d / 2f - 0.55f), Concrete));
        house.AddChild(Box(new Vector3(w - 0.6f, 0.9f, 0.06f), new Vector3(0f, fh + 0.6f, -d / 2f - 1.1f), RailPanel));
        for (int i = 0; i <= 6; i++)
            house.AddChild(Box(new Vector3(0.05f, 1.0f, 0.05f), new Vector3(-w / 2f + 0.3f + i * (w - 0.6f) / 6f, fh + 0.6f, -d / 2f - 1.1f), new Color(0.9f, 0.9f, 0.9f)));
        // 窓（両階）と入口
        foreach (float y in new[] { 1.5f, fh + 1.6f })
        {
            foreach (float x in new[] { -2.6f, 0f, 2.6f })
                house.AddChild(Box(new Vector3(1.5f, 1.2f, 0.08f), new Vector3(x, y, -d / 2f - 0.02f), ShadowGlass));
        }
        house.AddChild(Box(new Vector3(1.2f, 2.1f, 0.1f), new Vector3(w / 2f - 1.2f, 1.05f, d / 2f + 0.02f), new Color(0.35f, 0.38f, 0.4f)));
        // 西の壁（大通りカメラから見える面）にも窓と雨どい。無地の壁が画面に出ない
        foreach (float y in new[] { 1.5f, fh + 1.6f })
        {
            foreach (float z in new[] { -1.4f, 1.2f })
                house.AddChild(Box(new Vector3(0.08f, 1.1f, 1.3f), new Vector3(-w / 2f - 0.02f, y, z), ShadowGlass));
        }
        house.AddChild(MeshI(new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = fh * 2f },
            new Vector3(-w / 2f - 0.08f, fh, d / 2f - 0.3f), Mat(ConcreteDark)));
        // 前の生垣
        for (int i = 0; i < 3; i++)
        {
            house.AddChild(MeshI(new BoxMesh { Size = new Vector3(1.7f, 0.7f, 0.6f) },
                new Vector3(-w / 2f + 1.1f + i * 1.9f, 0.35f, -d / 2f - 1.9f),
                TexMat(BestTex("gen/TEX-leaf_canopy.jpg", "leaf"), new Vector2(1.4f, 0.6f), new Color(0.6f, 0.76f, 0.5f))));
        }
        house.AddChild(Collider(new Vector3(w, fh * 2f, d + 1.4f), new Vector3(0f, fh, -0.6f)));
        return house;
    }

    /// <summary>公園の街灯。灰色の鋼管に小さな灯具。参考画の園路脇の一本。</summary>
    private static Node3D BuildParkLamp(Vector3 pos)
    {
        var lamp = new Node3D { Position = pos };
        var grey = new Color(0.6f, 0.62f, 0.63f);
        lamp.AddChild(MeshI(new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.07f, Height = 4.2f }, new Vector3(0f, 2.1f, 0f), Mat(grey)));
        lamp.AddChild(Box(new Vector3(0.6f, 0.06f, 0.06f), new Vector3(0.3f, 4.15f, 0f), grey));
        lamp.AddChild(Box(new Vector3(0.45f, 0.22f, 0.3f), new Vector3(0.6f, 4.05f, 0f), new Color(0.85f, 0.86f, 0.84f)));
        return lamp;
    }

    // --- 空き地（土管） ---

    private static void BuildVacantLot(Node3D root)
    {
        var lot = new Node3D { Name = "VacantLot", Position = new Vector3(23f, 0f, 0f) };
        StandardMaterial3D pipeMat = TexMat("wall_weathered", new Vector2(1.5f, 1.5f));
        for (int i = 0; i < 3; i++)
        {
            Vector3 pos = i < 2
                ? new Vector3(0f, 0.8f, -0.85f + i * 1.7f)
                : new Vector3(0f, 2.15f, 0f);
            var pipe = MeshI(new CylinderMesh { TopRadius = 0.8f, BottomRadius = 0.8f, Height = 2.6f }, pos, pipeMat);
            pipe.RotationDegrees = new Vector3(0f, 0f, 90f);
            lot.AddChild(pipe);
        }
        lot.AddChild(Collider(new Vector3(2.8f, 3f, 3.4f), new Vector3(0f, 1.5f, 0f)));
        root.AddChild(lot);
    }

    // --- 樹木（3種: 広葉樹・メタセコイア・イチョウ）。セミの湧き先は Trees 直下 ---

    private static Node3D MakeTree(int i, Vector3 pos)
    {
        float s = 0.85f + (i * 37 % 4) * 0.13f;
        var tree = new Node3D
        {
            Position = pos,
            RotationDegrees = new Vector3(0f, i * 47f, 0f),
            Scale = new Vector3(s, s, s),
        };
        int species = i % 3;
        if (species == 1)
        {
            // 段刈りの針葉樹（参考画の池のまわりの木）。円錐を3段に積み、
            // 段のあいだに幹が少し見える。一本の円錐だとクリスマスツリーになる
            // 参考画の針葉樹は濃い緑 (31,42,14) の 2 段: 下に広いスカート、上に細い円錐、
            // 間に幹が見える。4 段の青緑では別の木だった（監査で実測）
            var dark = TexMat(BestTex("gen/TEX-leaf_canopy.jpg", "leaf"),
                              new Vector2(3f, 3f), new Color(0.3f, 0.42f, 0.22f));
            tree.AddChild(MeshI(new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.3f, Height = 6.2f },
                new Vector3(0f, 3.1f, 0f), Trunk));
            (float y, float r, float h)[] tiers = { (1.3f, 1.9f, 2.4f), (4.1f, 1.05f, 2.6f) };
            foreach ((float ty, float tr, float th) in tiers)
            {
                tree.AddChild(MeshI(new CylinderMesh { TopRadius = tr * 0.35f, BottomRadius = tr, Height = th },
                    new Vector3(0f, ty + th / 2f, 0f), dark));
            }
        }
        else
        {
            Color? tint = species == 2 ? new Color(1.15f, 1.2f, 0.6f) : null; // イチョウは黄緑
            var leaf = TexMat(BestTex("gen/TEX-leaf_canopy.jpg", "leaf"), new Vector2(3f, 3f), tint);
            tree.AddChild(MeshI(new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.38f, Height = 3f },
                new Vector3(0f, 1.5f, 0f), Trunk));
            tree.AddChild(MeshI(new SphereMesh { Radius = 1.8f, Height = 3.2f }, new Vector3(0f, 3.9f, 0f), leaf));
            tree.AddChild(MeshI(new SphereMesh { Radius = 1.2f, Height = 2.2f }, new Vector3(0.9f, 3.1f, 0.4f), leaf));
            tree.AddChild(MeshI(new SphereMesh { Radius = 1.0f, Height = 1.8f }, new Vector3(-0.8f, 3.3f, -0.4f), leaf));
        }
        return tree;
    }

    private static void BuildTrees(Node3D root)
    {
        var trees = new Node3D { Name = "Trees" };
        Vector3[] spots =
        {
            new(-12f, 0f, 4.2f),                     // 団地前の街路樹
            new(-26f, 0f, -11f),   // 棟間の通路を塞ぐので南へ移動
            // (8,0,4.2) は大通りカメラ (12,8,10.5) から 7.5m しか離れておらず、
            // 樹冠が画面の4割を塞いでいた。西へ移して同じ並木の一本にする
            new(-9.5f, 0f, 5.8f), new(-6f, 0f, 4.6f),
            new(-4f, 0f, -9f), new(-8f, 0f, -18f),   // 公園
            new(15f, 0f, -13f), new(0f, 0f, -20f),
            new(19f, 0f, 5f), new(27f, 0f, -4f),     // 空き地まわり
        };
        for (int i = 0; i < spots.Length; i++)
            trees.AddChild(MakeTree(i, spots[i]));
        root.AddChild(trees);

        // セミの湧かない飾りの木（メタセコイア並木と植え込み）
        var deco = new Node3D { Name = "DecoTrees" };
        // 団地南の並木。針葉樹 5 本の列だと CamPark の右中央に群れとして写り、
        // 参考画で空き地と白い建物がある場所を塞いだ。丸い広葉樹 3 本に
        for (int k = 0; k < 3; k++)
            deco.AddChild(MakeTree(k * 3, new Vector3(-26f + k * 5.5f, 0f, -11.5f)));
        for (int k = 0; k < 4; k++)
            deco.AddChild(MakeTree(k * 3 + 1, new Vector3(-30f, 0f, 4f + k * 4f)));      // 西縁の並木
        // 商店街の通路（z=14〜18）の真西に立っていて、望遠カメラの
        // 突き当りをこの1本が塞いでいた。北へ寄せて視線を通す
        deco.AddChild(MakeTree(6, new Vector3(-24f, 0f, 21.5f)));
        deco.AddChild(MakeTree(8, new Vector3(24f, 0f, 14f)));
        root.AddChild(deco);

        BuildKomorebi(root, spots);
    }

    /// <summary>
    /// 木漏れ日。木の下の地面に葉の影を落とす。
    /// いまは木の足元が均一な芝で、木が「地面に置いた置物」に見えている。
    /// 影が落ちて初めて、木と地面が同じ光の下にあるように見える。
    /// 濃さはアルファで持つので、SummerMain が天気と時刻で動かせる。
    /// </summary>
    private static void BuildKomorebi(Node3D root, Vector3[] spots)
    {
        if (!ResourceLoader.Exists("res://assets/textures/gen/komorebi.png"))
            return;   // 焼き直し（tools/gen_decals.py）がまだなら何も出さない

        var group = new Node3D { Name = "Komorebi" };
        var tex = GD.Load<Texture2D>("res://assets/textures/gen/komorebi.png");
        for (int i = 0; i < spots.Length; i++)
        {
            // 影は木ごとに別の材質にする。1枚を共有すると、
            // 回転や濃さを個別に動かせなくなる
            var mat = new StandardMaterial3D
            {
                AlbedoTexture = tex,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Back,
                Roughness = 1f,
            };
            // 太陽は南西寄り（Sun の Y 回転 -60°）なので、影は北東へ少しずれる
            float size = 5.6f + (i % 3) * 0.9f;
            var patch = new MeshInstance3D
            {
                Name = $"Komorebi{i}",
                Mesh = new PlaneMesh { Size = new Vector2(size, size) },
                MaterialOverride = mat,
                Position = new Vector3(spots[i].X + 0.9f, 0.03f, spots[i].Z + 0.7f),
                RotationDegrees = new Vector3(0f, i * 47f, 0f),   // 同じ模様の繰り返しに見せない
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            group.AddChild(patch);
        }
        root.AddChild(group);
    }

    // --- 電柱と電線（大通り沿い） ---

    private static void AddWireSegment(Node3D parent, Vector3 a, Vector3 b)
    {
        var seg = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.022f, Height = a.DistanceTo(b) },
            MaterialOverride = Mat(WireColor),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        Vector3 dir = (b - a).Normalized();
        seg.Transform = new Transform3D(new Basis(new Quaternion(Vector3.Up, dir)), (a + b) / 2f);
        parent.AddChild(seg);
    }

    private static void BuildPoles(Node3D root)
    {
        var poles = new Node3D { Name = "Poles" };
        var poleColor = new Color(0.45f, 0.44f, 0.42f);
        float[] xs = { -20f, -6f, 10f, 24f };
        const float armY = 6.2f;
        const float z = 11.6f;
        foreach (float x in xs)
        {
            var pole = new Node3D { Position = new Vector3(x, 0f, z) };
            pole.AddChild(MeshI(new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.14f, Height = 7f },
                new Vector3(0f, 3.5f, 0f), poleColor));
            pole.AddChild(Box(new Vector3(1.8f, 0.13f, 0.13f), new Vector3(0f, armY, 0f), poleColor));
            poles.AddChild(pole);
        }
        // 電線（隣り合う電柱の腕木の両端をたるませて結ぶ）
        for (int i = 0; i < xs.Length - 1; i++)
        {
            foreach (float off in new[] { -0.7f, 0.7f })
            {
                var a = new Vector3(xs[i] + off, armY, z);
                var b = new Vector3(xs[i + 1] + off, armY, z);
                Vector3 mid = (a + b) / 2f + Vector3.Down * 0.55f;
                AddWireSegment(poles, a, mid);
                AddWireSegment(poles, mid, b);
            }
        }
        root.AddChild(poles);
    }

    // --- 遠景（団地群・スカイライン・ゴルフ練習場・丘） ---

    private static void BuildBackdrop(Node3D root)
    {
        var backdrop = new Node3D { Name = "Backdrop" };

        // 生成した実写写真が届いていれば、シーンを囲む円筒パノラマとして使う。
        // 下絵と遠近が合わなかったので床に敷くプレートには使えないが、
        // 遠景なら画角の一致は要らない。平面マット絵だと見下ろしカメラの
        // 画角から外れるため、全カメラに効く円筒にする。
        // UV は上半分（空と団地）だけを使い、写真の地面部分は捨てる。
        const string PlatePath = "res://assets/plates/BG-danchi-noon.jpg";
        bool hasPlate = ResourceLoader.Exists(PlatePath);
        if (hasPlate)
        {
            var mat = new StandardMaterial3D
            {
                AlbedoTexture = GD.Load<Texture2D>(PlatePath),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                Uv1Scale = new Vector3(5f, 0.72f, 1f),   // 横に5回まわす / 縦は上72%だけ
            };
            var pano = new MeshInstance3D
            {
                Name = "Panorama",
                Mesh = new CylinderMesh
                {
                    TopRadius = 62f, BottomRadius = 62f, Height = 46f,
                    RadialSegments = 48, CapTop = false, CapBottom = false,
                },
                MaterialOverride = mat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0f, 20f, 0f), // 上端(y=43)を各カメラの画角外へ
            };
            backdrop.AddChild(pano);

            // 夜版が届いていれば、少し内側に重ねて時刻で透明度を動かす。
            // GPT は下絵には追従しないが、自分が作った昼と夜では
            // 同じ町・同じ建物を保っていたので、この2枚は対にして使える。
            const string NightPath = "res://assets/plates/BG-danchi-night.jpg";
            if (ResourceLoader.Exists(NightPath))
            {
                var nightMat = new StandardMaterial3D
                {
                    AlbedoTexture = GD.Load<Texture2D>(NightPath),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    AlbedoColor = new Color(1f, 1f, 1f, 0f), // 昼は完全に透明
                    Uv1Scale = new Vector3(5f, 0.72f, 1f),
                };
                var night = new MeshInstance3D
                {
                    Name = "PanoramaNight",
                    Mesh = new CylinderMesh
                    {
                        TopRadius = 61.5f, BottomRadius = 61.5f, Height = 46f,
                        RadialSegments = 48, CapTop = false, CapBottom = false,
                    },
                    MaterialOverride = nightMat,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = new Vector3(0f, 20f, 0f),
                };
                backdrop.AddChild(night);
            }

            // 夕焼け空。町のパノラマ（y=-3..43）の上半分だけを覆う位置に置く。
            // 町並みの帯とは高さで住み分けるので、遠景と喧嘩しない。
            // 一番内側の半径にして、夕方だけ前面に出す。
            const string SunsetPath = "res://assets/sky/SKY-sunset.jpg";
            if (ResourceLoader.Exists(SunsetPath))
            {
                var duskMat = new StandardMaterial3D
                {
                    AlbedoTexture = GD.Load<Texture2D>(SunsetPath),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    AlbedoColor = new Color(1f, 1f, 1f, 0f),   // 昼は完全に透明
                    // 写真の下端は暗い陸地なので、上 82% だけを使う
                    Uv1Scale = new Vector3(3f, 0.82f, 1f),
                };
                var dusk = new MeshInstance3D
                {
                    Name = "PanoramaDusk",
                    Mesh = new CylinderMesh
                    {
                        TopRadius = 61f, BottomRadius = 61f, Height = 42f,
                        RadialSegments = 48, CapTop = false, CapBottom = false,
                    },
                    MaterialOverride = duskMat,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = new Vector3(0f, 39f, 0f),   // y=18..60。町の帯より上
                };
                backdrop.AddChild(dusk);
            }

            // 雨雲。遠景の実写は晴れの日に撮った1枚しか無いので、
            // 雨の日でも空が青いままだった（暗くしても青は青のまま）。
            // 写真を足す代わりに、上空を覆う灰色の帯を重ねて空だけ差し替える。
            // 地平線側は透けさせる。全面を塗ると遠景の町まで消えてしまうので、
            // 上は雲で覆い、下は霞ませて町を残す
            var cloud = new Gradient();
            // 下端を透かしすぎると、目線の低いカメラ（CamLot 4.6m）の地平線に
            // 晴れの日の入道雲がそのまま見えた（監査 #6）。透ける帯を短くする
            cloud.SetColor(0, new Color(0.72f, 0.74f, 0.76f, 0.55f));   // 地平側（霞）
            cloud.SetColor(1, new Color(0.34f, 0.37f, 0.42f, 1f));   // 天頂側
            cloud.AddPoint(0.12f, new Color(0.66f, 0.68f, 0.71f, 0.9f));
            cloud.AddPoint(0.42f, new Color(0.58f, 0.61f, 0.64f, 0.95f));
            var rainMat = new StandardMaterial3D
            {
                AlbedoTexture = new GradientTexture2D
                {
                    Gradient = cloud,
                    Width = 8,
                    Height = 256,
                    Fill = GradientTexture2D.FillEnum.Linear,
                    FillFrom = new Vector2(0f, 1f),   // 下が明るい（地平線側）
                    FillTo = new Vector2(0f, 0f),
                },
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = new Color(1f, 1f, 1f, 0f),   // 晴れは完全に透明
            };
            backdrop.AddChild(new MeshInstance3D
            {
                Name = "PanoramaRain",
                Mesh = new CylinderMesh
                {
                    TopRadius = 60.5f, BottomRadius = 60.5f, Height = 56f,
                    RadialSegments = 48, CapTop = false, CapBottom = false,
                },
                MaterialOverride = rainMat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0f, 30f, 0f),   // y=2..58。水平の視線も覆う高さまで下げる
            });
        }

        // 西〜北西の遠景団地群（簡易版住棟）。マット絵がある側は間引く
        (Vector3 pos, float rot, int floors, float len)[] blocks =
        {
            (new Vector3(-44f, 0f, -12f), 0f, 5, 18f),
            (new Vector3(-46f, 0f, 0f), 0f, 4, 16f),
            (new Vector3(-44f, 0f, 12f), 0f, 5, 18f),
            (new Vector3(-38f, 0f, 26f), 20f, 4, 14f),
            (new Vector3(-16f, 0f, 30f), 0f, 5, 16f),
            (new Vector3(4f, 0f, 33f), -12f, 4, 16f),
            (new Vector3(26f, 0f, 30f), 8f, 5, 14f),
        };
        foreach (var (pos, rot, floors, len) in blocks)
        {
            if (hasPlate && pos.X < -35f)
                continue; // マット絵の手前に立って邪魔になる
            backdrop.AddChild(DanchiBlock(pos, rot, floors, len, detailed: false));
        }

        // 北の遠景: 高層住宅のシルエット
        (float x, float h, float w)[] towers =
        {
            (-30f, 16f, 8f), (-4f, 14f, 10f), (12f, 28f, 8f), (36f, 22f, 7f),
        };
        var towerColor = new Color(0.6f, 0.64f, 0.7f);
        foreach (var (x, h, w) in towers)
            backdrop.AddChild(Box(new Vector3(w, h, 6f), new Vector3(x, h / 2f, 48f), towerColor));

        // ゴルフ練習場（緑のネットと支柱。写真右上のランドマーク）
        var golf = new Node3D { Position = new Vector3(44f, 0f, 18f) };
        var netMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.25f, 0.5f, 0.3f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 1f,
        };
        var net = MeshI(new BoxMesh { Size = new Vector3(0.2f, 18f, 26f) }, new Vector3(0f, 9f, 0f), netMat);
        net.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        golf.AddChild(net);
        for (int i = 0; i < 4; i++)
        {
            golf.AddChild(MeshI(new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.18f, Height = 19f },
                new Vector3(0f, 9.5f, -12f + i * 8f), ConcreteDark));
        }
        backdrop.AddChild(golf);

        // 南の丘（千里丘陵ふう、霞んだ緑）。
        // 円錐1つだと「水色の三角の切り紙」にしか見えなかった（実際そう写った）。
        // 丸みのある塊を3つ重ねて稜線を崩し、Unshaded にして
        // 陽が当たって白飛びしないようにする（遠景は光より霞で決まる）。
        (Vector3 Pos, float R, float H, Color C)[] hills =
        {
            (new Vector3(-20f, 0f, -58f), 24f, 11f, new Color(0.36f, 0.46f, 0.42f)),
            (new Vector3(16f, 0f, -62f), 28f, 14f, new Color(0.41f, 0.51f, 0.5f)),
            (new Vector3(48f, 0f, -50f), 18f, 8f, new Color(0.38f, 0.48f, 0.44f)),
        };
        foreach ((Vector3 pos, float r, float h, Color c) in hills)
        {
            // (横のずれ, 大きさ, 高さの比)。少しずつずらして稜線を2〜3山にする
            (float Dx, float S, float Hy)[] lumps =
            {
                (0f, 1f, 1f), (-r * 0.62f, 0.72f, 0.78f), (r * 0.68f, 0.6f, 0.66f),
            };
            foreach ((float dx, float sc, float hy) in lumps)
            {
                var lump = MeshI(new SphereMesh { Radius = r * sc, Height = r * sc * 2f },
                    pos + new Vector3(dx, 0f, (dx == 0f ? 0f : 6f)), c, unshaded: true);
                // つぶして丘の丸みにする。球のままだと山が団子になる
                lump.Scale = new Vector3(1f, h * hy / (r * sc), 0.85f);
                backdrop.AddChild(lump);
                // 麓は霞に溶ける。一色の塊だと塗り絵に見えた（視覚監査 #13）。
                // 低く広い霞色の層を手前に重ね、稜線だけ濃い緑が残るようにする
                var foot = MeshI(new SphereMesh { Radius = r * sc * 1.12f, Height = r * sc * 2.24f },
                    pos + new Vector3(dx, 0f, (dx == 0f ? 0f : 6f) + 3f), c.Lerp(new Color(0.72f, 0.78f, 0.8f), 0.55f), unshaded: true);
                foot.Scale = new Vector3(1f, h * hy * 0.45f / (r * sc * 1.12f), 0.85f);
                backdrop.AddChild(foot);
            }
        }
        // 入道雲
        // 写真の空があるときは球の雲を作らない（CamPark の左上に白い玉として写った）
        Vector3[] clouds = hasPlate
            ? System.Array.Empty<Vector3>()
            : new Vector3[] { new(-24f, 22f, -36f), new(8f, 25f, -40f), new(34f, 21f, -30f), new(-8f, 26f, 36f) };
        foreach (Vector3 pos in clouds)
        {
            var cloud = new Node3D { Position = pos };
            cloud.AddChild(MeshI(new SphereMesh { Radius = 4f, Height = 6f }, Vector3.Zero, Colors.White, true));
            cloud.AddChild(MeshI(new SphereMesh { Radius = 2.8f, Height = 4.4f }, new Vector3(3.4f, -0.6f, 0.4f), Colors.White, true));
            cloud.AddChild(MeshI(new SphereMesh { Radius = 2.4f, Height = 3.6f }, new Vector3(-3.2f, -0.8f, -0.3f), Colors.White, true));
            backdrop.AddChild(cloud);
        }
        root.AddChild(backdrop);
    }

    // --- プレイヤー（2000年の子供: 赤いキャップ・白T・紺の半ズボン） ---

    private static void BuildPlayer(Node3D root)
    {
        // プレイヤーが一番長く見るものなので、町と同じだけ手を入れる。
        // 腕と脚は「振る根元」を Node3D で作り、その下にぶら下げる。
        // こうしないと歩行アニメで肩や腰から回せない。
        var player = new CharacterBody3D { Name = "Player", Position = new Vector3(-14f, 0.1f, 0f) };
        player.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.2f },
            Position = new Vector3(0f, 0.6f, 0f),
        });

        var cap = new Color(0.8f, 0.2f, 0.2f);
        var shirt = new Color(0.97f, 0.97f, 0.98f);
        var shorts = new Color(0.2f, 0.28f, 0.5f);
        var shoe = new Color(0.92f, 0.92f, 0.93f);

        player.AddChild(MeshI(new CapsuleMesh { Radius = 0.28f, Height = 0.78f },
            new Vector3(0f, 0.72f, 0f), shirt));                              // 白T
        player.AddChild(Box(new Vector3(0.48f, 0.3f, 0.3f), new Vector3(0f, 0.4f, 0f), shorts)); // 半ズボン
        player.AddChild(MeshI(new SphereMesh { Radius = 0.26f, Height = 0.52f },
            new Vector3(0f, 1.24f, 0f), Skin));                               // 顔
        player.AddChild(MeshI(new SphereMesh { Radius = 0.27f, Height = 0.3f },
            new Vector3(0f, 1.4f, 0.02f), cap));                              // キャップの山
        player.AddChild(Box(new Vector3(0.32f, 0.04f, 0.28f), new Vector3(0f, 1.38f, -0.28f), cap)); // つば

        // 目。正面（-Z）が分かるようになり、後ろ姿との区別がつく
        var eye = new Color(0.09f, 0.08f, 0.09f);
        foreach (float ex in new[] { -0.095f, 0.095f })
        {
            player.AddChild(MeshI(new SphereMesh { Radius = 0.033f, Height = 0.058f },
                new Vector3(ex, 1.25f, -0.235f), eye));
        }

        // 腕（肩を支点に振る）
        foreach ((string name, float sx) in new[] { ("ArmL", -0.31f), ("ArmR", 0.31f) })
        {
            var pivot = new Node3D { Name = name, Position = new Vector3(sx, 0.98f, 0f) };
            pivot.AddChild(MeshI(new CapsuleMesh { Radius = 0.068f, Height = 0.4f },
                new Vector3(0f, -0.2f, 0f), Skin));
            // 右手に虫あみ。腕の子にしてあるので、歩けば一緒に揺れ、
            // 振れば一緒に振れる。持っていないのに音だけ鳴るのは嘘になる
            if (name == "ArmR")
            {
                pivot.AddChild(BuildNet());
                pivot.AddChild(BuildRod());   // 釣りのときだけ出す（既定は非表示）
            }
            player.AddChild(pivot);
        }

        // 脚（腰を支点に振る）。足先だけ靴の色にして地面との接点を見せる
        foreach ((string name, float sx) in new[] { ("LegL", -0.13f), ("LegR", 0.13f) })
        {
            var pivot = new Node3D { Name = name, Position = new Vector3(sx, 0.4f, 0f) };
            pivot.AddChild(MeshI(new CapsuleMesh { Radius = 0.075f, Height = 0.36f },
                new Vector3(0f, -0.18f, 0f), Skin));
            pivot.AddChild(Box(new Vector3(0.16f, 0.08f, 0.24f), new Vector3(0f, -0.36f, -0.03f), shoe));
            player.AddChild(pivot);
        }

        root.AddChild(player);
    }

    /// <summary>虫あみ。竹の柄・輪っか・白い袋。腕にぶら下げて使う。</summary>
    private static Node3D BuildNet()
    {
        // 柄は前方（-Z）へ向ける。X軸まわりに -75° 回すと局所+Yが前を向く
        var net = new Node3D { Name = "Net", Position = new Vector3(0f, -0.42f, 0f) };
        net.RotationDegrees = new Vector3(-75f, 0f, 0f);

        var bamboo = new Color(0.78f, 0.7f, 0.42f);
        net.AddChild(MeshI(new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.024f, Height = 0.92f },
            new Vector3(0f, 0.46f, 0f), bamboo));
        net.AddChild(MeshI(new TorusMesh { InnerRadius = 0.17f, OuterRadius = 0.2f },
            new Vector3(0f, 0.96f, 0f), new Color(0.55f, 0.56f, 0.58f)));

        var bagMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.93f, 0.94f, 0.9f, 0.5f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 1f,
        };
        net.AddChild(MeshI(new CylinderMesh { TopRadius = 0.185f, BottomRadius = 0.03f, Height = 0.3f },
            new Vector3(0f, 1.12f, 0f), bagMat, noShadow: true));
        return net;
    }

    /// <summary>
    /// ザリガニつりの竿。柄・糸・ウキ。既定は非表示で、糸を垂らす間だけ出す。
    /// 池のふちで虫あみを振っていると、操作と絵が食い違って嘘になる。
    /// </summary>
    private static Node3D BuildRod()
    {
        // 虫あみと同じ支点・同じ向きの取り方。前方（-Z）へ、やや水平に構える
        var rod = new Node3D { Name = "Rod", Position = new Vector3(0f, -0.42f, 0f), Visible = false };
        rod.RotationDegrees = new Vector3(-64f, 0f, 0f);

        rod.AddChild(MeshI(new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.016f, Height = 1.15f },
            new Vector3(0f, 0.57f, 0f), new Color(0.5f, 0.38f, 0.22f)));

        // 糸とウキは水面の一点に置く（PlayerController が毎フレーム張り直す）。
        // 竿の子にぶら下げると、立つ位置によって宙に浮いたり水に届かなかったりする。
        rod.AddChild(new Node3D { Name = "Tip", Position = new Vector3(0f, 1.15f, 0f) });
        return rod;
    }

    private static Camera3D Cam(string name, Vector3 pos, Vector3 target, float fov, bool current = false)
    {
        return new Camera3D
        {
            Name = name,
            Fov = fov,
            Current = current,
            Transform = new Transform3D(Basis.LookingAt(target - pos, Vector3.Up), pos),
        };
    }

    private static void BuildCameras(Node3D root)
    {
        var cams = new Node3D { Name = "Cameras" };
        // 棟間（2棟のあいだの広場）を東から覗き込む。妻面（のっぺりした横っ腹）を
        // 正面に置くと広場も主人公も隠れてしまうので、通路の軸に沿わせる。
        cams.AddChild(Cam("CamDanchi", new Vector3(-1.5f, 7.5f, 0.9f), new Vector3(-24f, 1.6f, 0.1f), 40f, current: true));
        cams.AddChild(Cam("CamStreet", new Vector3(25f, 1.7f, 16.2f), new Vector3(-6f, 1.3f, 15.8f), 20f));
        // 参考画と同じ目線（池のふち越しに団地を見る、高さ 2.4m）
        cams.AddChild(Cam("CamPark", new Vector3(17f, 2.4f, -21.5f), new Vector3(-1f, 1.2f, -11f), 55f));
        // 空き地を CamLot に渡したぶん、大通りの画を寄せられる（実測 6.6% → ）
        cams.AddChild(Cam("CamPlaza", new Vector3(12f, 8f, 10.5f), new Vector3(0f, 0.6f, 1f), 44f));
        // 空き地（土管のある東端）。CamPlaza は東へ行くほどカメラに近づき、
        // 実測で主人公が画面の142%（＝カメラを追い越す）になっていた。専用の画を置く。
        // 北から土管の列に沿って見下ろす。西からだと (19,0,5) の木が
        // ちょうど視線上に立って土管を隠してしまう
        cams.AddChild(Cam("CamLot", new Vector3(20f, 4.6f, 15f), new Vector3(23f, 0.9f, 0f), 40f));
        // 公園の西半分（すべり台・砂場）。CamPark は池の対岸からなので、
        // 西の端では主人公が画面の4%まで縮んでいた。
        // 公園の南（歩ける範囲の外）から北を見る。団地の棟が背景に入る。
        // 北側に置くと団地の建物の内側にカメラが入ってしまう
        cams.AddChild(Cam("CamParkWest", new Vector3(-16f, 5.0f, -27f), new Vector3(-12f, 0.8f, -13f), 42f));
        root.AddChild(cams);
    }

    private static void BuildUi(Node3D root)
    {
        var ui = new CanvasLayer { Name = "UI" };

        var font = new SystemFont
        {
            FontNames = new[] { "Noto Sans CJK JP", "Noto Sans JP", "Hiragino Sans", "Yu Gothic", "Meiryo", "sans-serif" },
        };

        Label MakeLabel(string name, int size)
        {
            var label = new Label { Name = name };
            label.AddThemeFontOverride("font", font);
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.1f, 0.15f));
            label.AddThemeConstantOverride("outline_size", 8);
            return label;
        }

        Label date = MakeLabel("DateLabel", 30);
        date.Position = new Vector2(24f, 16f);
        ui.AddChild(date);

        Label bug = MakeLabel("BugLabel", 30);
        bug.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        bug.OffsetLeft = -660f;   // 「セミ／ずかん／はっけん」の3項目が入る幅
        bug.OffsetTop = 16f;
        bug.OffsetRight = -24f;
        bug.OffsetBottom = 60f;
        bug.HorizontalAlignment = HorizontalAlignment.Right;
        ui.AddChild(bug);

        var fade = new ColorRect
        {
            Name = "Fade",
            Color = new Color(0f, 0f, 0f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        ui.AddChild(fade);

        Label message = MakeLabel("MessageLabel", 34);
        message.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        message.OffsetLeft = 80f;      // 左右に余白を取り、長い日記でも画面内に収める
        message.OffsetRight = -80f;
        message.OffsetBottom = -36f;
        message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        message.HorizontalAlignment = HorizontalAlignment.Center;
        message.VerticalAlignment = VerticalAlignment.Bottom;
        ui.AddChild(message);

        root.AddChild(ui);

        // --- タイトル画面 ---
        // 届いたキーアート（UI-title_key.jpg）を出す場所がここしかない。
        // 起動していきなり導入が始まると、何のゲームか分からないまま黒画面が続く。
        var title = new CanvasLayer { Name = "Title", Layer = 2 };
        if (ResourceLoader.Exists("res://assets/ui/UI-title_key.jpg"))
        {
            var art = new TextureRect
            {
                Name = "Art",
                Texture = GD.Load<Texture2D>("res://assets/ui/UI-title_key.jpg"),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            };
            art.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            title.AddChild(art);
        }
        else
        {
            var bg = new ColorRect { Name = "Art", Color = new Color(0.06f, 0.08f, 0.12f) };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            title.AddChild(bg);
        }

        // 文字を写真の上に置くので、下側を暗く落として可読性を確保する。
        // 単色の矩形だと境目に一本線が入って写真が切れて見えるので、縦グラデにする
        var grad = new Gradient();
        grad.SetColor(0, new Color(0f, 0f, 0f, 0f));
        grad.SetColor(1, new Color(0f, 0f, 0f, 0.55f));
        var scrim = new TextureRect
        {
            Name = "Scrim",
            Texture = new GradientTexture2D
            {
                Gradient = grad,
                Width = 8,
                Height = 256,
                Fill = GradientTexture2D.FillEnum.Linear,
                FillFrom = new Vector2(0f, 0f),
                FillTo = new Vector2(0f, 1f),
            },
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        scrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        scrim.OffsetTop = 210f;
        title.AddChild(scrim);

        Label heading = MakeLabel("Heading", 72);
        heading.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        heading.OffsetTop = 330f;
        heading.OffsetBottom = -180f;
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        heading.VerticalAlignment = VerticalAlignment.Center;
        heading.Text = "ニュータウンの なつやすみ";
        title.AddChild(heading);

        Label sub = MakeLabel("Sub", 26);
        sub.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        sub.OffsetTop = 430f;
        sub.OffsetBottom = -120f;
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        sub.VerticalAlignment = VerticalAlignment.Center;
        sub.Text = "二〇〇〇年 八月";
        title.AddChild(sub);

        Label prompt = MakeLabel("Prompt", 30);
        prompt.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        prompt.OffsetTop = 520f;
        prompt.OffsetBottom = -40f;
        prompt.HorizontalAlignment = HorizontalAlignment.Center;
        prompt.VerticalAlignment = VerticalAlignment.Center;
        prompt.Text = "スペースで はじめる";
        title.AddChild(prompt);

        // 操作はここで渡す。本編の途中で操作説明を出すと、雰囲気を壊す
        Label keys = MakeLabel("Keys", 21);
        keys.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        keys.OffsetTop = 592f;
        keys.HorizontalAlignment = HorizontalAlignment.Center;
        keys.VerticalAlignment = VerticalAlignment.Center;
        keys.Modulate = new Color(1f, 1f, 1f, 0.78f);
        keys.Text = "やじるし あるく　　Shift はしる　　スペース むしあみ・つり・はなす　　Ｚ ずかん";
        title.AddChild(keys);

        root.AddChild(title);

        // --- ずかん ---
        // 31日かけて9種を集めるのに、数（3/9）しか見られず「何を捕ったか」を
        // 一度も見返せなかった。集める遊びの答えを返す場所を作る。
        var dex = new CanvasLayer { Name = "Dex", Layer = 3, Visible = false };
        var dexBg = new ColorRect { Name = "Bg", Color = new Color(0.06f, 0.09f, 0.07f, 0.88f) };
        dexBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dex.AddChild(dexBg);

        Label dexTitle = MakeLabel("Title", 40);
        dexTitle.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dexTitle.OffsetTop = 34f;
        dexTitle.OffsetBottom = -560f;
        dexTitle.HorizontalAlignment = HorizontalAlignment.Center;
        dex.AddChild(dexTitle);

        Label dexList = MakeLabel("List", 28);
        dexList.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dexList.OffsetLeft = 150f;
        dexList.OffsetRight = -150f;
        dexList.OffsetTop = 100f;
        dexList.OffsetBottom = -70f;
        dex.AddChild(dexList);

        Label dexFoot = MakeLabel("Foot", 24);
        dexFoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dexFoot.OffsetTop = 556f;
        dexFoot.HorizontalAlignment = HorizontalAlignment.Center;
        dexFoot.VerticalAlignment = VerticalAlignment.Center;
        dexFoot.Text = "Ｚ で とじる";
        dex.AddChild(dexFoot);

        root.AddChild(dex);

        // --- 絵日記 ---
        // 一日の締めが真っ黒な画面に白文字だけで、31日ぶん毎晩それを見る。
        // 「日記」と名乗っていながら絵が一枚も無かった。
        // その日の最後の画面をそのまま貼るので、絵は毎日ちがう。
        var diary = new CanvasLayer { Name = "Diary", Layer = 4, Visible = false };
        // 紙の後ろは暗い机。世界が透けると「ページを見ている」感じにならない
        var desk = new ColorRect { Name = "Desk", Color = new Color(0.05f, 0.05f, 0.07f) };
        desk.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        diary.AddChild(desk);

        var paper = new ColorRect { Name = "Paper", Color = new Color(0.93f, 0.9f, 0.82f) };
        paper.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        paper.OffsetLeft = 96f;
        paper.OffsetRight = -96f;
        paper.OffsetTop = 22f;
        paper.OffsetBottom = -22f;
        diary.AddChild(paper);

        // 綴じ側の赤い罫と、紙の左端の穴。ノートに見せるための最小限
        var margin = new ColorRect { Name = "Margin", Color = new Color(0.78f, 0.45f, 0.42f, 0.55f) };
        margin.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        margin.OffsetLeft = 54f;
        margin.OffsetRight = 56f;
        margin.OffsetTop = 12f;
        margin.OffsetBottom = -12f;
        paper.AddChild(margin);

        // その日の絵（最後の画面を貼る）。枠を付けて写真らしく見せる
        // TextureRect は既定でははみ出すので、枠側で切る
        var frame = new ColorRect { Name = "Frame", Color = new Color(0.99f, 0.98f, 0.95f), ClipContents = true };
        frame.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        frame.OffsetLeft = 84f;
        frame.OffsetRight = -40f;
        frame.OffsetTop = 26f;
        frame.OffsetBottom = -266f;
        paper.AddChild(frame);

        var shot = new TextureRect
        {
            Name = "Shot",
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = new Color(0.97f, 0.94f, 0.86f),   // 紙に焼いた色味に寄せる
        };
        shot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        shot.OffsetLeft = 9f;
        shot.OffsetRight = -9f;
        shot.OffsetTop = 9f;
        shot.OffsetBottom = -9f;
        frame.AddChild(shot);

        Label diaryText = MakeLabel("Text", 28);
        diaryText.AddThemeColorOverride("font_color", new Color(0.16f, 0.15f, 0.2f));
        diaryText.AddThemeConstantOverride("outline_size", 0);
        diaryText.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        diaryText.OffsetLeft = 84f;
        diaryText.OffsetRight = -40f;
        diaryText.OffsetTop = 352f;
        diaryText.OffsetBottom = -18f;
        diaryText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        diaryText.HorizontalAlignment = HorizontalAlignment.Left;
        diaryText.VerticalAlignment = VerticalAlignment.Top;
        paper.AddChild(diaryText);

        root.AddChild(diary);
    }
}

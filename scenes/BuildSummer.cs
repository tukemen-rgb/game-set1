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
            AmbientLightColor = new Color(0.6f, 0.65f, 0.7f),
            FogEnabled = true,
            FogLightColor = new Color(0.75f, 0.85f, 0.95f),
            FogDensity = 0.006f,
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
            Shape = new BoxShape3D { Size = new Vector3(120f, 1f, 120f) },
            Position = new Vector3(0f, -0.5f, 0f),
        });
        // 芝は画面の大半を占めるので、実写が届いていればそれを使う
        ground.AddChild(MeshI(new PlaneMesh { Size = new Vector2(120f, 120f) }, Vector3.Zero,
            TexMat(BestTex("gen/TEX-grass_summer.jpg", "grass"), new Vector2(26f, 26f))));
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
            b.AddChild(Box(new Vector3(length, FloorH - 1.25f, 0.12f), new Vector3(0f, y + 1.88f, 2.56f), ShadowGlass));
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

        // 通路の両脇の植え込み（低く刈り込んだ生垣）
        var hedge = new Color(0.24f, 0.4f, 0.22f);
        for (int i = 0; i < 6; i++)
        {
            float hx = -9f + i * 3.6f;
            danchi.AddChild(Box(new Vector3(3f, 0.6f, 0.55f), new Vector3(hx, 0.3f, -1.95f), hedge));
            danchi.AddChild(Box(new Vector3(3f, 0.6f, 0.55f), new Vector3(hx, 0.3f, 1.95f), hedge * 1.1f));
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

        var roofMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.9f, 0.8f, 0.45f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.4f,
        };
        var roof = MeshI(new BoxMesh { Size = new Vector3(34f, 0.12f, 5.2f) },
            new Vector3(-2f, 4.4f, 15.9f), roofMat);
        roof.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        street.AddChild(roof);
        foreach (float x in new[] { -17f, -9f, -1f, 7f, 14f })
        {
            street.AddChild(Box(new Vector3(0.15f, 4.4f, 0.15f), new Vector3(x, 2.2f, 13.5f), ConcreteDark));
            street.AddChild(Box(new Vector3(0.15f, 4.4f, 0.15f), new Vector3(x, 2.2f, 18.3f), ConcreteDark));
        }

        var vending = new Node3D { Position = new Vector3(14.5f, 0f, 10.8f) };
        vending.AddChild(Box(new Vector3(0.95f, 1.8f, 0.8f), new Vector3(0f, 0.9f, 0f), new Color(0.8f, 0.15f, 0.15f)));
        vending.AddChild(Box(new Vector3(0.8f, 0.9f, 0.05f), new Vector3(0f, 1.25f, -0.41f), new Color(0.9f, 0.9f, 0.92f)));
        street.AddChild(vending);
        root.AddChild(street);
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

    // --- 公園（池・すべり台・ブランコ・砂場・ベンチ） ---

    private static void BuildPark(Node3D root)
    {
        var park = new Node3D { Name = "Park" };

        var waterMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.25f, 0.45f, 0.6f),
            Roughness = 0.08f,
            Metallic = 0.4f,
            NormalEnabled = true,
            NormalTexture = GD.Load<Texture2D>("res://assets/textures/water_normal.png"),
            NormalScale = 1.6f,
            Uv1Scale = new Vector3(3f, 3f, 1f),
        };
        park.AddChild(MeshI(new CylinderMesh { TopRadius = 6.4f, BottomRadius = 6.4f, Height = 0.1f },
            new Vector3(6f, -0.02f, -15f), waterMat));
        park.AddChild(MeshI(new TorusMesh { InnerRadius = 6.2f, OuterRadius = 7f },
            new Vector3(6f, 0f, -15f), TexMat("dirt", new Vector2(8f, 1f))));

        var slide = new Node3D { Position = new Vector3(-6f, 0f, -11f) };
        slide.AddChild(Box(new Vector3(1f, 0.25f, 0.8f), new Vector3(0f, 0.55f, 0.9f), Stone));
        slide.AddChild(Box(new Vector3(1f, 0.25f, 0.8f), new Vector3(0f, 1.05f, 0.3f), Stone));
        slide.AddChild(Box(new Vector3(1f, 0.25f, 0.9f), new Vector3(0f, 1.55f, -0.35f), new Color(0.85f, 0.45f, 0.3f)));
        var chute = MeshI(new BoxMesh { Size = new Vector3(0.9f, 0.1f, 3.4f) },
            new Vector3(0f, 0.95f, -1.9f), Mat(new Color(0.75f, 0.78f, 0.8f)));
        chute.RotationDegrees = new Vector3(-24f, 0f, 0f);
        slide.AddChild(chute);
        park.AddChild(slide);

        var swing = new Node3D { Position = new Vector3(-2f, 0f, -17f) };
        var frame = new Color(0.35f, 0.55f, 0.65f);
        swing.AddChild(Box(new Vector3(0.15f, 2.4f, 0.15f), new Vector3(-1.6f, 1.2f, 0f), frame));
        swing.AddChild(Box(new Vector3(0.15f, 2.4f, 0.15f), new Vector3(1.6f, 1.2f, 0f), frame));
        swing.AddChild(Box(new Vector3(3.5f, 0.15f, 0.15f), new Vector3(0f, 2.4f, 0f), frame));
        foreach (float sx in new[] { -0.8f, 0.8f })
        {
            swing.AddChild(Box(new Vector3(0.05f, 1.8f, 0.05f), new Vector3(sx - 0.25f, 1.45f, 0f), ConcreteDark));
            swing.AddChild(Box(new Vector3(0.05f, 1.8f, 0.05f), new Vector3(sx + 0.25f, 1.45f, 0f), ConcreteDark));
            swing.AddChild(Box(new Vector3(0.6f, 0.06f, 0.25f), new Vector3(sx, 0.55f, 0f), DarkWood));
        }
        park.AddChild(swing);

        var sandbox = new Node3D { Position = new Vector3(13f, 0f, -8f) };
        sandbox.AddChild(TexBox(new Vector3(3.6f, 0.08f, 3.6f), new Vector3(0f, 0.05f, 0f), "dirt", new Vector2(3f, 3f)));
        sandbox.AddChild(Box(new Vector3(4f, 0.25f, 0.25f), new Vector3(0f, 0.12f, -1.9f), DarkWood));
        sandbox.AddChild(Box(new Vector3(4f, 0.25f, 0.25f), new Vector3(0f, 0.12f, 1.9f), DarkWood));
        sandbox.AddChild(Box(new Vector3(0.25f, 0.25f, 3.6f), new Vector3(-1.9f, 0.12f, 0f), DarkWood));
        sandbox.AddChild(Box(new Vector3(0.25f, 0.25f, 3.6f), new Vector3(1.9f, 0.12f, 0f), DarkWood));
        park.AddChild(sandbox);
        foreach (float bx in new[] { -10f, 16f })
        {
            var bench = new Node3D { Position = new Vector3(bx, 0f, -16f) };
            bench.AddChild(Box(new Vector3(1.8f, 0.08f, 0.5f), new Vector3(0f, 0.45f, 0f), DarkWood));
            bench.AddChild(Box(new Vector3(0.15f, 0.45f, 0.45f), new Vector3(-0.7f, 0.22f, 0f), ConcreteDark));
            bench.AddChild(Box(new Vector3(0.15f, 0.45f, 0.45f), new Vector3(0.7f, 0.22f, 0f), ConcreteDark));
            park.AddChild(bench);
        }
        root.AddChild(park);
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
            // メタセコイア（円錐の針葉樹）
            var dark = TexMat(BestTex("gen/TEX-leaf_canopy.jpg", "leaf"),
                              new Vector2(3f, 3f), new Color(0.55f, 0.75f, 0.55f));
            tree.AddChild(MeshI(new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.28f, Height = 4f },
                new Vector3(0f, 2f, 0f), Trunk));
            tree.AddChild(MeshI(new CylinderMesh { TopRadius = 0.15f, BottomRadius = 1.5f, Height = 3f },
                new Vector3(0f, 3.2f, 0f), dark));
            tree.AddChild(MeshI(new CylinderMesh { TopRadius = 0.05f, BottomRadius = 1.1f, Height = 2.6f },
                new Vector3(0f, 5.2f, 0f), dark));
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
            new(-9.5f, 0f, 5.8f), new(8f, 0f, 4.2f), // 大通り沿い（棟間カメラの手前を塞がない位置へ）
            new(-4f, 0f, -9f), new(-8f, 0f, -18f),   // 公園
            new(15f, 0f, -13f), new(0f, 0f, -20f),
            new(19f, 0f, 5f), new(27f, 0f, -4f),     // 空き地まわり
        };
        for (int i = 0; i < spots.Length; i++)
            trees.AddChild(MakeTree(i, spots[i]));
        root.AddChild(trees);

        // セミの湧かない飾りの木（メタセコイア並木と植え込み）
        var deco = new Node3D { Name = "DecoTrees" };
        for (int k = 0; k < 5; k++)
            deco.AddChild(MakeTree(k * 3 + 1, new Vector3(-27f + k * 3.4f, 0f, -11.5f))); // 団地南の並木
        for (int k = 0; k < 4; k++)
            deco.AddChild(MakeTree(k * 3 + 1, new Vector3(-30f, 0f, 4f + k * 4f)));      // 西縁の並木
        deco.AddChild(MakeTree(6, new Vector3(-24f, 0f, 16f)));
        deco.AddChild(MakeTree(8, new Vector3(24f, 0f, 14f)));
        root.AddChild(deco);
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

        // 南の丘（千里丘陵ふう、霞んだ緑）
        (Vector3 pos, float r, float h, Color c)[] hills =
        {
            (new Vector3(-18f, 0f, -50f), 22f, 12f, new Color(0.42f, 0.55f, 0.5f)),
            (new Vector3(14f, 0f, -52f), 26f, 15f, new Color(0.5f, 0.62f, 0.62f)),
            (new Vector3(44f, 0f, -44f), 16f, 9f, new Color(0.44f, 0.57f, 0.5f)),
        };
        foreach (var (pos, r, h, c) in hills)
        {
            backdrop.AddChild(MeshI(new CylinderMesh { TopRadius = 0f, BottomRadius = r, Height = h },
                pos + new Vector3(0f, h / 2f, 0f), c));
        }
        // 入道雲
        Vector3[] clouds = { new(-24f, 22f, -36f), new(8f, 25f, -40f), new(34f, 21f, -30f), new(-8f, 26f, 36f) };
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
                pivot.AddChild(BuildNet());
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
        cams.AddChild(Cam("CamPark", new Vector3(17f, 3.2f, -21f), new Vector3(0f, 0.4f, -12f), 55f));
        cams.AddChild(Cam("CamPlaza", new Vector3(16f, 12f, 12f), new Vector3(0f, 0f, 2f), 50f));
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
    }
}

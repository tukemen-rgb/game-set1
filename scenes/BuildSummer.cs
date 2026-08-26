using Godot;

/// <summary>
/// summer_main.tscn をヘッドレスで生成するビルダー。実行方法:
///   godot --headless --path . --script res://scenes/BuildTextures.cs   （テクスチャ生成）
///   godot --headless --import                                          （PNG 取り込み）
///   godot --headless --path . --script res://scenes/BuildSummer.cs     （このスクリプト）
/// ランタイムロジックは一切持たない（godot.md の規約）。
///
/// 舞台は「2000年ごろのニュータウンの1住区」（千里ニュータウン参考、
/// docs/RESEARCH.md コンセプト v2）:
///   西=板状住棟の団地と給水塔 / 北=近隣センターの商店街（アーケード）/
///   南=池とすべり台のある公園 / 東=土管のある空き地。中央に大通りと横断歩道。
/// カメラは場面ごとの固定4台。
/// </summary>
public partial class BuildSummer : SceneTree
{
    private static readonly Color DarkWood = new(0.3f, 0.22f, 0.15f);
    private static readonly Color Trunk = new(0.4f, 0.28f, 0.18f);
    private static readonly Color Skin = new(0.98f, 0.85f, 0.72f);
    private static readonly Color Stone = new(0.58f, 0.58f, 0.6f);
    private static readonly Color Concrete = new(0.7f, 0.68f, 0.64f);
    private static readonly Color ConcreteDark = new(0.55f, 0.54f, 0.51f);

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

        // スクリプトは最後に付ける（SetScript は C# ラッパーを破棄するため、
        // ルートは一時ノード経由で付け直して取得し直す）
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

    private static StandardMaterial3D TexMat(string tex, Vector2 uvScale, float roughness = 1f)
    {
        return new StandardMaterial3D
        {
            AlbedoTexture = GD.Load<Texture2D>($"res://assets/textures/{tex}.png"),
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
            Shape = new BoxShape3D { Size = new Vector3(80f, 1f, 80f) },
            Position = new Vector3(0f, -0.5f, 0f),
        });
        ground.AddChild(MeshI(new PlaneMesh { Size = new Vector2(80f, 80f) }, Vector3.Zero,
            TexMat("grass", new Vector2(14f, 14f))));
        root.AddChild(ground);

        var roads = new Node3D { Name = "Roads" };
        // 大通り（東西、z=8）
        roads.AddChild(TexBox(new Vector3(80f, 0.06f, 4f), new Vector3(0f, 0.03f, 8f), "asphalt", new Vector2(20f, 1f)));
        // 歩道（両側）
        roads.AddChild(TexBox(new Vector3(80f, 0.08f, 1.6f), new Vector3(0f, 0.04f, 5.4f), "paving", new Vector2(50f, 1f)));
        roads.AddChild(TexBox(new Vector3(80f, 0.08f, 1.6f), new Vector3(0f, 0.04f, 10.6f), "paving", new Vector2(50f, 1f)));
        // 横断歩道（x=2）
        for (int i = 0; i < 5; i++)
        {
            roads.AddChild(Box(new Vector3(1.8f, 0.021f, 0.45f),
                new Vector3(2f, 0.065f, 6.5f + i * 0.78f), new Color(0.88f, 0.88f, 0.86f)));
        }
        // 団地へ入る小道と公園への小道
        roads.AddChild(TexBox(new Vector3(2f, 0.05f, 10f), new Vector3(-12f, 0.025f, 0f), "paving", new Vector2(2f, 12f)));
        roads.AddChild(TexBox(new Vector3(2f, 0.05f, 12f), new Vector3(2f, 0.05f, -1f), "paving", new Vector2(2f, 14f)));
        root.AddChild(roads);
    }

    // --- 団地（板状住棟2棟＋給水塔＋広場） ---

    private static void BuildDanchi(Node3D root)
    {
        var danchi = new Node3D { Name = "Danchi", Position = new Vector3(-19f, 0f, 0f) };

        foreach (float z in new[] { -6f, 6f })
        {
            var block = new Node3D { Position = new Vector3(0f, 0f, z) };
            // 住棟本体（外壁テクスチャ: 横8戸×5階でタイル）
            block.AddChild(MeshI(new BoxMesh { Size = new Vector3(16f, 13.5f, 5f) },
                new Vector3(0f, 6.75f, 0f), TexMat("facade", new Vector2(8f, 5f))));
            // 屋上スラブと基礎
            block.AddChild(Box(new Vector3(16.4f, 0.5f, 5.4f), new Vector3(0f, 13.75f, 0f), ConcreteDark));
            block.AddChild(Box(new Vector3(16.4f, 0.6f, 5.4f), new Vector3(0f, 0.3f, 0f), ConcreteDark));
            // 入口ポーチ（南面に3つ）
            foreach (float px in new[] { -5f, 0f, 5f })
            {
                block.AddChild(Box(new Vector3(1.8f, 2.4f, 0.6f), new Vector3(px, 1.2f, 2.75f), Concrete));
                block.AddChild(Box(new Vector3(1.4f, 2f, 0.2f), new Vector3(px, 1f, 3.0f), new Color(0.25f, 0.28f, 0.3f)));
            }
            block.AddChild(Collider(new Vector3(16f, 13.5f, 5f), new Vector3(0f, 6.75f, 0f)));
            danchi.AddChild(block);
        }

        // 給水塔（キノコ型: 柱＋タンク＋丸屋根）。商店街の正面軸から外した位置
        var tower = new Node3D { Position = new Vector3(-11f, 0f, 21f) };
        tower.AddChild(MeshI(new CylinderMesh { TopRadius = 1.3f, BottomRadius = 1.5f, Height = 9f },
            new Vector3(0f, 4.5f, 0f), Concrete));
        tower.AddChild(MeshI(new CylinderMesh { TopRadius = 3.4f, BottomRadius = 3.4f, Height = 3.2f },
            new Vector3(0f, 10.6f, 0f), Concrete));
        tower.AddChild(MeshI(new SphereMesh { Radius = 3.4f, Height = 2.4f },
            new Vector3(0f, 12.2f, 0f), ConcreteDark));
        danchi.AddChild(tower);

        // 広場のベンチと物干し場らしき柵
        danchi.AddChild(Box(new Vector3(1.8f, 0.4f, 0.5f), new Vector3(4.5f, 0.35f, -1f), DarkWood));
        danchi.AddChild(Box(new Vector3(1.8f, 0.4f, 0.5f), new Vector3(4.5f, 0.35f, 1.5f), DarkWood));
        root.AddChild(danchi);
    }

    // --- 商店街（近隣センター: 両側の店とアーケード屋根） ---

    private static void BuildShotengai(Node3D root)
    {
        var street = new Node3D { Name = "Shotengai" };
        // 通路の舗装
        street.AddChild(TexBox(new Vector3(36f, 0.07f, 6.4f), new Vector3(-2f, 0.035f, 15.9f), "paving", new Vector2(22f, 4f)));

        Color[] awnings =
        {
            new(0.85f, 0.3f, 0.28f), new(0.25f, 0.55f, 0.5f), new(0.9f, 0.6f, 0.2f),
            new(0.3f, 0.45f, 0.7f), new(0.45f, 0.6f, 0.3f), new(0.85f, 0.5f, 0.55f),
            new(0.6f, 0.4f, 0.65f),
        };
        // 南列（z=12.9, 通路に向いて北向き）と北列（z=19, 南向き）。
        // 南列の i=3（x=-4）は抜いて大通りからの入口にする
        for (int i = 0; i < 7; i++)
        {
            float x = -16f + i * 4f;
            if (i != 3)
                street.AddChild(BuildShop(new Vector3(x, 0f, 12.7f), +1, awnings[i], i % 2 == 0));
            street.AddChild(BuildShop(new Vector3(x, 0f, 19.1f), -1, awnings[(i + 3) % 7], i % 2 == 1));
        }
        street.AddChild(Collider(new Vector3(12f, 3f, 3f), new Vector3(-12f, 1.5f, 12.7f)));
        street.AddChild(Collider(new Vector3(12f, 3f, 3f), new Vector3(4f, 1.5f, 12.7f)));
        street.AddChild(Collider(new Vector3(28f, 3f, 3f), new Vector3(-4f, 1.5f, 19.1f)));
        // 入口の舗装
        street.AddChild(TexBox(new Vector3(2.6f, 0.05f, 3.6f), new Vector3(-4f, 0.045f, 12.6f), "paving", new Vector2(2f, 3f)));

        // アーケード屋根（半透明・影は落とさない）
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

        // 自販機（大通り角）
        var vending = new Node3D { Position = new Vector3(14.5f, 0f, 10.8f) };
        vending.AddChild(Box(new Vector3(0.95f, 1.8f, 0.8f), new Vector3(0f, 0.9f, 0f), new Color(0.8f, 0.15f, 0.15f)));
        vending.AddChild(Box(new Vector3(0.8f, 0.9f, 0.05f), new Vector3(0f, 1.25f, -0.41f), new Color(0.9f, 0.9f, 0.92f)));
        street.AddChild(vending);
        root.AddChild(street);
    }

    private static Node3D BuildShop(Vector3 pos, int facing, Color awning, bool glassFront)
    {
        // facing: +1 = 通路が +z 側（南列）、-1 = 通路が -z 側（北列）
        var shop = new Node3D { Position = pos };
        shop.AddChild(TexBox(new Vector3(3.9f, 3.1f, 3f), new Vector3(0f, 1.55f, 0f), "plaster", new Vector2(2f, 2f)));
        shop.AddChild(Box(new Vector3(3.9f, 0.3f, 3.1f), new Vector3(0f, 3.2f, 0f), ConcreteDark));
        float front = 1.55f * facing;
        // 店先（ガラス戸とシャッターを交互に）・テント庇・看板
        Color frontColor = glassFront ? new Color(0.22f, 0.26f, 0.3f) : new Color(0.66f, 0.62f, 0.56f);
        shop.AddChild(Box(new Vector3(2.6f, 2.2f, 0.12f), new Vector3(0f, 1.1f, front), frontColor));
        var tent = MeshI(new BoxMesh { Size = new Vector3(3.6f, 0.08f, 1.1f) },
            new Vector3(0f, 2.55f, front + 0.5f * facing), Mat(awning));
        tent.RotationDegrees = new Vector3(14f * facing, 0f, 0f);
        shop.AddChild(tent);
        shop.AddChild(Box(new Vector3(3.4f, 0.65f, 0.12f), new Vector3(0f, 2.85f, front + 0.05f * facing),
            awning.Lerp(Colors.White, 0.55f)));
        return shop;
    }

    // --- 公園（池・すべり台・ブランコ・砂場・ベンチ） ---

    private static void BuildPark(Node3D root)
    {
        var park = new Node3D { Name = "Park" };

        // 池（掘り下げ＋縁石）
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
        // 水面は地面よりわずかに上（地面平面に隠れないように）
        var pond = MeshI(new CylinderMesh { TopRadius = 6.4f, BottomRadius = 6.4f, Height = 0.1f },
            new Vector3(6f, -0.02f, -15f), waterMat);
        park.AddChild(pond);
        // 土手はリング（トーラス）。円柱だと上面の円盤が水面に蓋をしてしまう
        var rim = MeshI(new TorusMesh { InnerRadius = 6.2f, OuterRadius = 7f },
            new Vector3(6f, 0f, -15f), TexMat("dirt", new Vector2(8f, 1f)));
        park.AddChild(rim);

        // すべり台（階段＋斜面）
        var slide = new Node3D { Position = new Vector3(-6f, 0f, -11f) };
        slide.AddChild(Box(new Vector3(1f, 0.25f, 0.8f), new Vector3(0f, 0.55f, 0.9f), Stone));
        slide.AddChild(Box(new Vector3(1f, 0.25f, 0.8f), new Vector3(0f, 1.05f, 0.3f), Stone));
        slide.AddChild(Box(new Vector3(1f, 0.25f, 0.9f), new Vector3(0f, 1.55f, -0.35f), new Color(0.85f, 0.45f, 0.3f)));
        var chute = MeshI(new BoxMesh { Size = new Vector3(0.9f, 0.1f, 3.4f) },
            new Vector3(0f, 0.95f, -1.9f), Mat(new Color(0.75f, 0.78f, 0.8f)));
        chute.RotationDegrees = new Vector3(-24f, 0f, 0f);
        slide.AddChild(chute);
        park.AddChild(slide);

        // ブランコ（フレーム＋座板2つ）
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

        // 砂場（木枠）とベンチ
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
        StandardMaterial3D pipeMat = Mat(Concrete);
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

    // --- 街路樹と公園の木（セミの湧き先 = Trees 直下のノード位置） ---

    private static void BuildTrees(Node3D root)
    {
        var trees = new Node3D { Name = "Trees" };
        Vector3[] spots =
        {
            new(-12f, 0f, 4.2f),                     // 団地前の街路樹
            new(-26f, 0f, 1f),
            new(-8f, 0f, 4.2f), new(8f, 0f, 4.2f),   // 大通り沿い
            new(-4f, 0f, -9f), new(-8f, 0f, -18f),   // 公園
            new(15f, 0f, -13f), new(0f, 0f, -20f),
            new(19f, 0f, 5f), new(27f, 0f, -4f),     // 空き地まわり
        };
        StandardMaterial3D leaf = TexMat("leaf", new Vector2(3f, 3f));
        for (int i = 0; i < spots.Length; i++)
        {
            float s = 0.85f + (i * 37 % 4) * 0.13f;
            var tree = new Node3D
            {
                Position = spots[i],
                RotationDegrees = new Vector3(0f, i * 47f, 0f),
                Scale = new Vector3(s, s, s),
            };
            tree.AddChild(MeshI(new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.38f, Height = 3f },
                new Vector3(0f, 1.5f, 0f), Trunk));
            tree.AddChild(MeshI(new SphereMesh { Radius = 1.8f, Height = 3.2f }, new Vector3(0f, 3.9f, 0f), leaf));
            tree.AddChild(MeshI(new SphereMesh { Radius = 1.2f, Height = 2.2f }, new Vector3(0.9f, 3.1f, 0.4f), leaf));
            tree.AddChild(MeshI(new SphereMesh { Radius = 1.0f, Height = 1.8f }, new Vector3(-0.8f, 3.3f, -0.4f), leaf));
            trees.AddChild(tree);
        }
        root.AddChild(trees);
    }

    // --- 電柱（大通り沿い） ---

    private static void BuildPoles(Node3D root)
    {
        var poles = new Node3D { Name = "Poles" };
        var poleColor = new Color(0.45f, 0.44f, 0.42f); // コンクリート柱
        foreach (float x in new[] { -20f, -6f, 10f, 24f })
        {
            var pole = new Node3D { Position = new Vector3(x, 0f, 11.6f) };
            pole.AddChild(MeshI(new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.14f, Height = 7f },
                new Vector3(0f, 3.5f, 0f), poleColor));
            pole.AddChild(Box(new Vector3(1.8f, 0.13f, 0.13f), new Vector3(0f, 6.2f, 0f), poleColor));
            poles.AddChild(pole);
        }
        root.AddChild(poles);
    }

    // --- 遠景（マンション群のスカイラインと丘） ---

    private static void BuildBackdrop(Node3D root)
    {
        var backdrop = new Node3D { Name = "Backdrop" };
        // 北の遠景: 高層住宅のシルエット
        (float x, float h, float w)[] towers =
        {
            (-30f, 16f, 8f), (-18f, 24f, 7f), (-4f, 14f, 10f), (10f, 28f, 8f), (24f, 18f, 9f), (36f, 22f, 7f),
        };
        var towerColor = new Color(0.6f, 0.64f, 0.7f);
        foreach (var (x, h, w) in towers)
            backdrop.AddChild(Box(new Vector3(w, h, 6f), new Vector3(x, h / 2f, 44f), towerColor));
        // 南の丘（千里丘陵ふう、霞んだ緑）
        (Vector3 pos, float r, float h, Color c)[] hills =
        {
            (new Vector3(-18f, 0f, -46f), 22f, 12f, new Color(0.42f, 0.55f, 0.5f)),
            (new Vector3(14f, 0f, -48f), 26f, 15f, new Color(0.5f, 0.62f, 0.62f)),
            (new Vector3(42f, 0f, -40f), 16f, 9f, new Color(0.44f, 0.57f, 0.5f)),
        };
        foreach (var (pos, r, h, c) in hills)
        {
            backdrop.AddChild(MeshI(new CylinderMesh { TopRadius = 0f, BottomRadius = r, Height = h },
                pos + new Vector3(0f, h / 2f, 0f), c));
        }
        // 入道雲（ライティングに影響されない・影も落とさない）
        Vector3[] clouds = { new(-20f, 21f, -32f), new(8f, 24f, -36f), new(30f, 20f, -26f), new(-6f, 25f, 30f) };
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
        var player = new CharacterBody3D { Name = "Player", Position = new Vector3(-14f, 0.1f, 0f) };
        player.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.2f },
            Position = new Vector3(0f, 0.6f, 0f),
        });
        var cap = new Color(0.8f, 0.2f, 0.2f);
        player.AddChild(MeshI(new CapsuleMesh { Radius = 0.3f, Height = 0.9f },
            new Vector3(0f, 0.55f, 0f), Colors.White));                       // 白T
        player.AddChild(Box(new Vector3(0.5f, 0.35f, 0.32f), new Vector3(0f, 0.2f, 0f), new Color(0.2f, 0.28f, 0.5f))); // 半ズボン
        player.AddChild(MeshI(new SphereMesh { Radius = 0.28f, Height = 0.56f },
            new Vector3(0f, 1.22f, 0f), Skin));                               // 顔
        player.AddChild(MeshI(new SphereMesh { Radius = 0.29f, Height = 0.32f },
            new Vector3(0f, 1.42f, 0.02f), cap));                             // キャップの山
        player.AddChild(Box(new Vector3(0.34f, 0.04f, 0.3f), new Vector3(0f, 1.4f, -0.3f), cap)); // つば（前 = -Z）
        root.AddChild(player);
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
        cams.AddChild(Cam("CamDanchi", new Vector3(5f, 13f, -11f), new Vector3(-19f, 3.5f, 3f), 45f, current: true));
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
        bug.OffsetLeft = -280f;
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
        message.OffsetBottom = -36f;
        message.HorizontalAlignment = HorizontalAlignment.Center;
        message.VerticalAlignment = VerticalAlignment.Bottom;
        ui.AddChild(message);

        root.AddChild(ui);
    }
}

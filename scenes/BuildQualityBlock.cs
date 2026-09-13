using Godot;

/// <summary>
/// 1990年代後半〜2000年ごろの千里ニュータウン風「団地広場＋公園入口」品質基準区画。
/// 震災・災害表現は扱わない。通常の経年劣化と生活感だけで古さを出す。
/// 実行:
///   godot --headless --path . --script res://scenes/BuildQualityBlock.cs
/// 出力:
///   res://scenes/quality_block_1990s.tscn
/// </summary>
public partial class BuildQualityBlock : SceneTree
{
    private readonly Color Concrete = new(0.68f, 0.67f, 0.63f);
    private readonly Color ConcreteDark = new(0.48f, 0.48f, 0.45f);
    private readonly Color Rail = new(0.54f, 0.56f, 0.56f);
    private readonly Color Glass = new(0.18f, 0.24f, 0.28f);
    private readonly Color Soil = new(0.43f, 0.34f, 0.22f);
    private readonly Color Grass = new(0.29f, 0.40f, 0.16f);
    private readonly Color Trunk = new(0.28f, 0.19f, 0.12f);

    public override void _Initialize()
    {
        var root = new Node3D { Name = "QualityBlock1990s" };
        BuildEnvironment(root);
        BuildGround(root);
        BuildDanchi(root);
        BuildParkEntrance(root);
        BuildTrees(root);
        BuildStreetFurniture(root);
        BuildCamera(root);

        var temp = new Node();
        temp.AddChild(root);
        SetOwnerRecursive(root, root);

        var packed = new PackedScene();
        if (packed.Pack(root) != Error.Ok)
        {
            GD.PushError("quality block pack failed");
            Quit(1);
            return;
        }
        ResourceSaver.Save(packed, "res://scenes/quality_block_1990s.tscn");
        GD.Print("saved res://scenes/quality_block_1990s.tscn");
        Quit(0);
    }

    private void BuildEnvironment(Node3D root)
    {
        var skyMat = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.10f, 0.34f, 0.76f),
            SkyHorizonColor = new Color(0.63f, 0.82f, 0.96f),
            GroundBottomColor = new Color(0.25f, 0.22f, 0.17f),
            GroundHorizonColor = new Color(0.63f, 0.82f, 0.96f),
            SkyCurve = 0.12f,
        };
        root.AddChild(new WorldEnvironment
        {
            Name = "Environment",
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky { SkyMaterial = skyMat },
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.73f, 0.70f, 0.64f),
                AmbientLightEnergy = 0.72f,
                FogEnabled = true,
                FogDensity = 0.0015f,
                FogLightColor = new Color(0.78f, 0.86f, 0.95f),
            }
        });
        root.AddChild(new DirectionalLight3D
        {
            Name = "SummerSun",
            RotationDegrees = new Vector3(-58f, -38f, 0f),
            LightEnergy = 1.22f,
            ShadowEnabled = true,
            ShadowBlur = 1.7f,
            DirectionalShadowMaxDistance = 90f,
        });
    }

    private void BuildGround(Node3D root)
    {
        var ground = new Node3D { Name = "Ground" };
        ground.AddChild(Mesh(new BoxMesh { Size = new Vector3(54f, 0.15f, 36f) },
            new Vector3(0f, -0.075f, 0f), Mat(Grass, 0.94f)));

        // 団地広場: 1990年代のニュータウンに多い明灰色の平板舗装。少し暖色へ。
        ground.AddChild(Mesh(new BoxMesh { Size = new Vector3(24f, 0.06f, 13f) },
            new Vector3(-8f, 0.03f, 1f), Mat(new Color(0.56f, 0.55f, 0.51f), 0.92f)));

        // 公園入口へ続く歩道。
        ground.AddChild(Mesh(new BoxMesh { Size = new Vector3(5.2f, 0.07f, 22f) },
            new Vector3(8.7f, 0.035f, 0f), Mat(new Color(0.62f, 0.61f, 0.57f), 0.9f)));

        // 踏まれて芝が薄くなった土。災害由来ではなく日常使用による摩耗。
        ground.AddChild(Mesh(new BoxMesh { Size = new Vector3(4.4f, 0.025f, 8.5f) },
            new Vector3(2.5f, 0.022f, -5.8f), Mat(Soil, 1.0f)));
        ground.AddChild(Mesh(new BoxMesh { Size = new Vector3(2.0f, 0.027f, 6.0f) },
            new Vector3(13f, 0.024f, 7.5f), Mat(Soil.Lerp(Grass, 0.18f), 1.0f)));
        root.AddChild(ground);
    }

    private void BuildDanchi(Node3D root)
    {
        var group = new Node3D { Name = "Danchi" };
        // 5階建て、幅約25m。外形よりベランダの奥行きと階段室の張り出しを優先。
        group.AddChild(Mesh(new BoxMesh { Size = new Vector3(26f, 13.2f, 8.4f) },
            new Vector3(-8f, 6.6f, -11.5f), Mat(Concrete, 0.96f)));

        // 南面の深いベランダ。各階を別体にして陰影を作る。
        for (int floor = 0; floor < 5; floor++)
        {
            float y = 1.55f + floor * 2.55f;
            for (int bay = 0; bay < 6; bay++)
            {
                float x = -18.3f + bay * 4.15f;
                // ベランダ床・天井
                group.AddChild(Mesh(new BoxMesh { Size = new Vector3(3.65f, 0.12f, 1.15f) },
                    new Vector3(x, y - 0.85f, -6.78f), Mat(ConcreteDark, 0.98f)));
                // 奥まったサッシ面
                group.AddChild(Mesh(new BoxMesh { Size = new Vector3(2.15f, 1.55f, 0.10f) },
                    new Vector3(x, y, -7.34f), Mat(Glass, 0.42f, 0.08f)));
                // 手すり上桟
                group.AddChild(Mesh(new BoxMesh { Size = new Vector3(3.55f, 0.09f, 0.09f) },
                    new Vector3(x, y - 0.02f, -6.18f), Mat(Rail, 0.78f, 0.22f)));
                for (int r = -3; r <= 3; r++)
                    group.AddChild(Mesh(new BoxMesh { Size = new Vector3(0.045f, 0.88f, 0.045f) },
                        new Vector3(x + r * 0.48f, y - 0.46f, -6.18f), Mat(Rail, 0.78f, 0.22f)));

                // 室外機: 全戸均一にせず、生活感をばらす。
                if ((floor + bay) % 2 == 0)
                    group.AddChild(Mesh(new BoxMesh { Size = new Vector3(0.68f, 0.48f, 0.26f) },
                        new Vector3(x + 1.15f, y - 0.53f, -7.12f), Mat(new Color(0.76f, 0.75f, 0.70f), 0.84f, 0.12f)));

                // 布団: 一部だけ。実在ロゴ・文字なし。
                if ((floor == 2 && bay == 1) || (floor == 3 && bay == 4))
                    group.AddChild(Mesh(new BoxMesh { Size = new Vector3(1.15f, 0.82f, 0.035f) },
                        new Vector3(x - 0.55f, y - 0.48f, -6.12f),
                        Mat(new Color(0.53f, 0.63f, 0.73f), 0.95f)));
            }
        }

        // 階段室の張り出し。板状住棟の単調さを崩す重要な輪郭。
        group.AddChild(Mesh(new BoxMesh { Size = new Vector3(3.2f, 13.8f, 2.0f) },
            new Vector3(-8f, 6.9f, -6.35f), Mat(new Color(0.61f, 0.60f, 0.56f), 0.97f)));
        for (int floor = 0; floor < 5; floor++)
        {
            float y = 1.55f + floor * 2.55f;
            group.AddChild(Mesh(new BoxMesh { Size = new Vector3(1.28f, 1.35f, 0.08f) },
                new Vector3(-8f, y, -5.31f), Mat(new Color(0.24f, 0.31f, 0.33f), 0.48f, 0.04f)));
        }

        // 雨どい（通常の雨風による経年の象徴。災害表現ではない）。
        group.AddChild(Mesh(new CylinderMesh { Height = 13.1f, TopRadius = 0.07f, BottomRadius = 0.07f },
            new Vector3(4.45f, 6.55f, -6.55f), Mat(new Color(0.43f, 0.44f, 0.42f), 0.72f, 0.16f)));
        root.AddChild(group);
    }

    private void BuildParkEntrance(Node3D root)
    {
        var park = new Node3D { Name = "ParkEntrance" };
        // 青い鋼管すべり台。既存の時代感に合わせて彩度を抑える。
        var blue = Mat(new Color(0.19f, 0.48f, 0.62f), 0.58f, 0.34f);
        for (int i = 0; i < 2; i++)
        {
            float x = 12.0f + i * 1.25f;
            var leg = Mesh(new CylinderMesh { Height = 2.1f, TopRadius = 0.055f, BottomRadius = 0.055f },
                new Vector3(x, 1.05f, -5.0f), blue);
            leg.RotationDegrees = new Vector3(0, 0, 0);
            park.AddChild(leg);
        }
        park.AddChild(Mesh(new BoxMesh { Size = new Vector3(1.9f, 0.09f, 1.5f) },
            new Vector3(12.6f, 2.05f, -5.2f), blue));
        var chute = Mesh(new BoxMesh { Size = new Vector3(1.0f, 0.09f, 4.4f) },
            new Vector3(12.6f, 1.05f, -2.8f), Mat(new Color(0.54f, 0.62f, 0.64f), 0.48f, 0.42f));
        chute.RotationDegrees = new Vector3(-24f, 0f, 0f);
        park.AddChild(chute);

        // ベンチ: 木板＋金属脚。
        park.AddChild(Mesh(new BoxMesh { Size = new Vector3(3.1f, 0.16f, 0.62f) },
            new Vector3(6.4f, 0.52f, -1.6f), Mat(new Color(0.36f, 0.24f, 0.13f), 0.92f)));
        park.AddChild(Mesh(new BoxMesh { Size = new Vector3(0.10f, 0.52f, 0.52f) },
            new Vector3(5.3f, 0.26f, -1.6f), Mat(ConcreteDark, 0.96f)));
        park.AddChild(Mesh(new BoxMesh { Size = new Vector3(0.10f, 0.52f, 0.52f) },
            new Vector3(7.5f, 0.26f, -1.6f), Mat(ConcreteDark, 0.96f)));
        root.AddChild(park);
    }

    private void BuildTrees(Node3D root)
    {
        var trees = new Node3D { Name = "Trees" };
        Vector3[] positions =
        {
            new(-20f,0f,-1.5f), new(-15f,0f,5.5f), new(-3.5f,0f,7.5f),
            new(3.5f,0f,-2f), new(15.5f,0f,2.2f), new(19f,0f,9.5f)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            float h = 5.6f + (i % 3) * 0.9f;
            trees.AddChild(Mesh(new CylinderMesh { Height = h, TopRadius = 0.18f, BottomRadius = 0.34f },
                positions[i] + new Vector3(0, h/2f, 0), Mat(Trunk, 1f)));

            // 単一球ではなく3〜4クラスタで樹冠を崩す。
            Color leaf = i % 2 == 0 ? new Color(0.16f,0.31f,0.11f) : new Color(0.20f,0.37f,0.13f);
            for (int c = 0; c < 4; c++)
            {
                float ox = (c - 1.5f) * 0.75f;
                float oz = ((c * 7) % 3 - 1) * 0.72f;
                var crown = Mesh(new SphereMesh { Radius = 1.75f + 0.15f * (c % 2), Height = 3.2f },
                    positions[i] + new Vector3(ox, h + 0.55f + 0.36f*(c%2), oz), Mat(leaf, 1f));
                crown.Scale = new Vector3(1.15f, 0.92f, 1.0f);
                trees.AddChild(crown);
            }
        }
        root.AddChild(trees);
    }

    private void BuildStreetFurniture(Node3D root)
    {
        var f = new Node3D { Name = "StreetFurniture" };
        // 4.2m街灯。意匠は架空、1990年代のニュータウンで違和感のないシンプルな形。
        for (int i = 0; i < 3; i++)
        {
            float z = -5f + i * 6.5f;
            f.AddChild(Mesh(new CylinderMesh { Height = 4.2f, TopRadius = 0.065f, BottomRadius = 0.10f },
                new Vector3(9.9f, 2.1f, z), Mat(new Color(0.36f,0.38f,0.37f), 0.72f, 0.18f)));
            f.AddChild(Mesh(new SphereMesh { Radius = 0.28f, Height = 0.56f },
                new Vector3(9.9f, 4.38f, z), Mat(new Color(0.83f,0.84f,0.78f), 0.43f, 0.04f)));
        }
        // 掲示板。文字は入れない。
        f.AddChild(Mesh(new BoxMesh { Size = new Vector3(2.3f, 1.5f, 0.11f) },
            new Vector3(2.2f, 1.45f, 4.2f), Mat(new Color(0.20f,0.30f,0.20f), 0.86f)));
        f.AddChild(Mesh(new BoxMesh { Size = new Vector3(0.12f, 2.1f, 0.12f) },
            new Vector3(1.25f, 1.05f, 4.2f), Mat(new Color(0.32f,0.27f,0.20f), 0.95f)));
        f.AddChild(Mesh(new BoxMesh { Size = new Vector3(0.12f, 2.1f, 0.12f) },
            new Vector3(3.15f, 1.05f, 4.2f), Mat(new Color(0.32f,0.27f,0.20f), 0.95f)));
        root.AddChild(f);
    }

    private void BuildCamera(Node3D root)
    {
        var camera = new Camera3D
        {
            Name = "QualityCamera",
            Position = new Vector3(18.5f, 3.1f, 16.5f),
            Fov = 52f,
            Current = true,
        };
        camera.LookAtFromPosition(camera.Position, new Vector3(-4f, 3.6f, -5.5f));
        root.AddChild(camera);
    }

    private static StandardMaterial3D Mat(Color color, float roughness, float metallic = 0f)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = roughness,
            Metallic = metallic,
        };
    }

    private static MeshInstance3D Mesh(PrimitiveMesh mesh, Vector3 pos, Material mat)
    {
        return new MeshInstance3D
        {
            Mesh = mesh,
            Position = pos,
            MaterialOverride = mat,
        };
    }

    private static void SetOwnerRecursive(Node node, Node owner)
    {
        foreach (Node child in node.GetChildren())
        {
            child.Owner = owner;
            SetOwnerRecursive(child, owner);
        }
    }
}

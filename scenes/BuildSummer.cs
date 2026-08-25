using Godot;

/// <summary>
/// summer_main.tscn をヘッドレスで生成するビルダー。実行方法:
///   godot --headless --path . --script res://scenes/BuildSummer.cs
/// ランタイムロジックは一切持たない（godot.md の規約）。
/// 世界は「昭和の夏の田舎」のジオラマ: 家と庭 / 田舎道とひまわり / 川辺 / 原っぱ。
/// カメラは場面ごとの固定4台（docs/RESEARCH.md のカメラ文法の値）。
/// </summary>
public partial class BuildSummer : SceneTree
{
    private static readonly Color Grass = new(0.45f, 0.62f, 0.32f);
    private static readonly Color Dirt = new(0.78f, 0.68f, 0.5f);
    private static readonly Color Water = new(0.3f, 0.55f, 0.8f);
    private static readonly Color Wood = new(0.55f, 0.4f, 0.28f);
    private static readonly Color Wall = new(0.92f, 0.88f, 0.78f);
    private static readonly Color Roof = new(0.45f, 0.32f, 0.3f);
    private static readonly Color Trunk = new(0.4f, 0.28f, 0.18f);
    private static readonly Color Leaf = new(0.3f, 0.55f, 0.25f);
    private static readonly Color LeafDark = new(0.24f, 0.46f, 0.22f);
    private static readonly Color Straw = new(0.93f, 0.82f, 0.45f);
    private static readonly Color Skin = new(0.98f, 0.85f, 0.72f);

    public override void _Initialize()
    {
        var root = new Node3D { Name = "SummerMain" };

        BuildEnvironment(root);
        BuildTerrain(root);
        BuildHouse(root);
        BuildTrees(root);
        BuildSunflowers(root);
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

    private static MeshInstance3D MeshI(Mesh mesh, Vector3 pos, Color color, bool unshaded = false)
    {
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = Mat(color, unshaded), Position = pos };
        if (unshaded)
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; // 雲の影が地形に落ちないように
        return mi;
    }

    private static MeshInstance3D Box(Vector3 size, Vector3 pos, Color color)
    {
        return MeshI(new BoxMesh { Size = size }, pos, color);
    }

    // --- 環境・地形 ---

    private static void BuildEnvironment(Node3D root)
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.45f, 0.72f, 0.95f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.6f, 0.65f),
            FogEnabled = true,
            FogLightColor = new Color(0.75f, 0.85f, 0.95f),
            FogDensity = 0.005f,
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

    private static void BuildTerrain(Node3D root)
    {
        // 地面（当たり判定つき）。上面が y=0
        var ground = new StaticBody3D { Name = "Ground" };
        ground.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(80f, 1f, 80f) },
            Position = new Vector3(0f, -0.5f, 0f),
        });
        ground.AddChild(MeshI(new PlaneMesh { Size = new Vector2(80f, 80f) }, Vector3.Zero, Grass));
        root.AddChild(ground);

        // 砂利道（東西方向、z=8）とあぜ道
        root.AddChild(Box(new Vector3(80f, 0.06f, 3.2f), new Vector3(0f, 0.03f, 8f), Dirt));
        root.AddChild(Box(new Vector3(2f, 0.05f, 12f), new Vector3(-6f, 0.03f, 0f), new Color(0.72f, 0.64f, 0.48f)));

        // 川（東西方向、z=-16）と岸の石
        root.AddChild(Box(new Vector3(80f, 0.04f, 7f), new Vector3(0f, 0.02f, -16f), Water));
        for (int i = 0; i < 8; i++)
        {
            float x = -28f + i * 8f;
            root.AddChild(MeshI(new SphereMesh { Radius = 0.5f, Height = 0.7f },
                new Vector3(x, 0.1f, -12.6f), new Color(0.6f, 0.6f, 0.62f)));
        }
    }

    private static void BuildHouse(Node3D root)
    {
        var house = new Node3D { Name = "House", Position = new Vector3(-10f, 0f, 1f) };
        house.AddChild(Box(new Vector3(6f, 3f, 5f), new Vector3(0f, 1.5f, 0f), Wall));
        house.AddChild(MeshI(new PrismMesh { Size = new Vector3(7.2f, 2.2f, 6f) },
            new Vector3(0f, 4.1f, 0f), Roof));
        // 縁側（南向き）と引き戸
        house.AddChild(Box(new Vector3(5f, 0.4f, 1.4f), new Vector3(0f, 0.2f, 3.1f), Wood));
        house.AddChild(Box(new Vector3(2.2f, 2.2f, 0.1f), new Vector3(0f, 1.2f, 2.56f), new Color(0.35f, 0.3f, 0.25f)));
        // 家の当たり判定
        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(6f, 3f, 5f) },
            Position = new Vector3(0f, 1.5f, 0f),
        });
        house.AddChild(body);
        root.AddChild(house);
    }

    private static void BuildTrees(Node3D root)
    {
        var trees = new Node3D { Name = "Trees" };
        Vector3[] spots =
        {
            new(-15f, 0f, -3f), new(-5f, 0f, -4f),
            new(7f, 0f, 11.5f), new(16f, 0f, 11f), new(-3f, 0f, 12f),
            new(-9f, 0f, -9f), new(6f, 0f, -9.5f), new(15f, 0f, -8.5f),
            new(4f, 0f, -1f), new(22f, 0f, 2f),
        };
        foreach (Vector3 pos in spots)
        {
            var tree = new Node3D { Position = pos };
            tree.AddChild(MeshI(new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.35f, Height = 3f },
                new Vector3(0f, 1.5f, 0f), Trunk));
            tree.AddChild(MeshI(new SphereMesh { Radius = 1.8f, Height = 3.2f },
                new Vector3(0f, 3.9f, 0f), Leaf));
            tree.AddChild(MeshI(new SphereMesh { Radius = 1.2f, Height = 2.2f },
                new Vector3(0.9f, 3.1f, 0.4f), LeafDark));
            trees.AddChild(tree);
        }
        root.AddChild(trees);
    }

    private static void BuildSunflowers(Node3D root)
    {
        var flowers = new Node3D { Name = "Sunflowers" };
        for (int i = 0; i < 7; i++)
        {
            float x = -15f + i * 5f;
            var f = new Node3D { Position = new Vector3(x, 0f, 9.9f) };
            f.AddChild(MeshI(new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 1.3f },
                new Vector3(0f, 0.65f, 0f), new Color(0.3f, 0.5f, 0.2f)));
            f.AddChild(MeshI(new SphereMesh { Radius = 0.28f, Height = 0.4f },
                new Vector3(0f, 1.4f, 0f), new Color(0.95f, 0.8f, 0.15f)));
            f.AddChild(MeshI(new SphereMesh { Radius = 0.12f, Height = 0.2f },
                new Vector3(0f, 1.4f, 0.14f), new Color(0.4f, 0.28f, 0.12f)));
            flowers.AddChild(f);
        }
        root.AddChild(flowers);
    }

    private static void BuildBackdrop(Node3D root)
    {
        var backdrop = new Node3D { Name = "Backdrop" };
        // 遠景の山（カメラの抜け対策）
        (Vector3 pos, float r, float h, Color c)[] hills =
        {
            (new Vector3(-18f, 0f, -44f), 20f, 13f, new Color(0.35f, 0.5f, 0.42f)),
            (new Vector3(12f, 0f, -47f), 24f, 16f, new Color(0.3f, 0.45f, 0.4f)),
            (new Vector3(38f, 0f, -40f), 16f, 10f, new Color(0.38f, 0.52f, 0.42f)),
            (new Vector3(-45f, 0f, 12f), 22f, 12f, new Color(0.36f, 0.5f, 0.4f)),
        };
        foreach (var (pos, r, h, c) in hills)
        {
            backdrop.AddChild(MeshI(new CylinderMesh { TopRadius = 0f, BottomRadius = r, Height = h },
                pos + new Vector3(0f, h / 2f, 0f), c));
        }
        // 入道雲（ライティングに影響されないよう unshaded）
        Vector3[] clouds = { new(-20f, 20f, -30f), new(8f, 23f, -34f), new(28f, 19f, -25f), new(-2f, 24f, 18f) };
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

    private static void BuildPlayer(Node3D root)
    {
        var player = new CharacterBody3D { Name = "Player", Position = new Vector3(-6f, 0.1f, 4f) };
        player.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.2f },
            Position = new Vector3(0f, 0.6f, 0f),
        });
        player.AddChild(MeshI(new CapsuleMesh { Radius = 0.3f, Height = 0.9f },
            new Vector3(0f, 0.55f, 0f), Colors.White));                       // 白シャツ
        player.AddChild(Box(new Vector3(0.5f, 0.35f, 0.32f), new Vector3(0f, 0.2f, 0f), new Color(0.25f, 0.35f, 0.6f))); // 半ズボン
        player.AddChild(MeshI(new SphereMesh { Radius = 0.28f, Height = 0.56f },
            new Vector3(0f, 1.22f, 0f), Skin));                               // 顔
        player.AddChild(MeshI(new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.32f, Height = 0.14f },
            new Vector3(0f, 1.5f, 0f), Straw));                               // 帽子の山
        player.AddChild(MeshI(new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.55f, Height = 0.04f },
            new Vector3(0f, 1.44f, 0f), Straw));                              // 帽子のつば
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
        cams.AddChild(Cam("CamYard", new Vector3(-17f, 8f, 11f), new Vector3(-9f, 0.5f, 1f), 40f, current: true));
        cams.AddChild(Cam("CamRoad", new Vector3(24f, 1.4f, 9.5f), new Vector3(-2f, 1f, 8f), 22f));
        cams.AddChild(Cam("CamRiver", new Vector3(3f, 2.6f, -22f), new Vector3(0f, 0.6f, -10f), 55f));
        cams.AddChild(Cam("CamField", new Vector3(13f, 11f, 15f), new Vector3(0f, 0f, -1f), 50f));
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

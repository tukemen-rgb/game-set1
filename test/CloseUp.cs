using Godot;

/// <summary>
/// 細部を確かめるための寄りのキャプチャ。小さい物（セミ・店先の小物）は
/// 固定カメラからでは点にしか映らず、目視検査ができないため。
///   xvfb-run -a godot --path . --write-movie screenshots/closeup/frame.png \
///     --fixed-fps 30 --quit-after 40 --script res://test/CloseUp.cs
/// 対象の座標は CLOSEUP_AT 環境変数（"x,y,z"）で差し替えられる。既定は公園の木。
/// </summary>
public partial class CloseUp : SceneTree
{
    private Camera3D _cam;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 100000.0);  // 時刻を止める
        main.Set("RngSeed", 2);
        Root.AddChild(main);
        ((CanvasLayer)main.GetNode("UI")).Visible = false;

        var target = new Vector3(-4f, 1.7f, -9f);
        string env = OS.GetEnvironment("CLOSEUP_AT");
        if (env != "")
        {
            string[] parts = env.Split(',');
            if (parts.Length == 3)
                target = new Vector3(parts[0].ToFloat(), parts[1].ToFloat(), parts[2].ToFloat());
        }

        _cam = new Camera3D { Fov = 30f };
        Root.AddChild(_cam);
        // 木の葉（樹冠は y=2.1 付近から始まる）に潜り込まないよう、
        // 低く・少し離れた位置から見る
        _cam.LookAtFromPosition(target + new Vector3(2.5f, 0.18f, 1.9f), target, Vector3.Up);
    }

    public override bool _Process(double delta)
    {
        _cam.MakeCurrent();   // 固定カメラに毎フレーム奪い返される
        return false;
    }
}

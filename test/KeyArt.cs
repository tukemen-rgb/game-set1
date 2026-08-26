using Godot;

/// <summary>
/// 参考写真（団地の航空写真・曇天）と比較するためのキーアート撮影。
///   xvfb-run -a godot --path . --write-movie screenshots/keyart/frame.png \
///     --fixed-fps 30 --quit-after 12 --script res://test/KeyArt.cs
/// 曇天モード・UI 非表示・俯瞰カメラで数フレームだけ撮る。
/// </summary>
public partial class KeyArt : SceneTree
{
    private Camera3D _cam;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 100000.0); // 時刻を止める
        main.Set("Overcast", true);
        main.Set("RngSeed", 2);
        Root.AddChild(main);
        ((CanvasLayer)main.GetNode("UI")).Visible = false;

        _cam = new Camera3D { Fov = 48f };
        Root.AddChild(_cam);
        _cam.LookAtFromPosition(new Vector3(32f, 23f, -26f), new Vector3(-22f, 1f, 10f), Vector3.Up);
    }

    public override bool _Process(double delta)
    {
        // SummerMain._Ready の固定カメラ切替に負けないよう毎フレーム奪い返す
        _cam.MakeCurrent();
        return false;
    }
}

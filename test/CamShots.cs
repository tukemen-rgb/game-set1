using Godot;

/// <summary>
/// 4つの固定カメラの構図を1枚ずつ撮る。画像生成AIへ渡す img2img 参照用。
///   xvfb-run -a godot --path . --write-movie screenshots/camshots/frame.png \
///     --fixed-fps 30 --quit-after 12 --script res://test/CamShots.cs
/// UI と主人公を消し、時刻を固定して「背景プレートの下絵」として撮る。
/// カメラは3フレームずつ切り替わるので frame 2/5/8/11 を採用する。
/// </summary>
public partial class CamShots : SceneTree
{
    private static readonly string[] Cams = { "CamDanchi", "CamStreet", "CamPark", "CamPlaza" };
    private Node _main;
    private int _frame;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        _main = packed.Instantiate();
        _main.Set("SecondsPerHour", 100000.0); // 時刻を止める
        _main.Set("RngSeed", 2);
        _main.Set("SkipIntro", true);   // 検査では導入を飛ばす
        Root.AddChild(_main);
        ((CanvasLayer)_main.GetNode("UI")).Visible = false;
        ((Node3D)_main.GetNode("Player")).Visible = false;
    }

    public override bool _Process(double delta)
    {
        int idx = Mathf.Min(_frame / 3, Cams.Length - 1);
        _main.GetNode<Camera3D>($"Cameras/{Cams[idx]}").MakeCurrent();
        _frame++;
        return false;
    }
}

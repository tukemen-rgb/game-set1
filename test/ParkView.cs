using Godot;

/// <summary>
/// 公園の再現（docs/reference/park_pond_gpt.png）を動画で確かめる用。
/// 池の東から北へ歩き、CamPark の画の中を主人公が横切る。
///   xvfb-run -a godot --path . --write-movie screenshots/park/frame.png \
///     --fixed-fps 30 --quit-after 480 --script res://test/ParkView.cs
/// </summary>
public partial class ParkView : SceneTree
{
    private double _t;
    private Node _main;
    private bool _placed;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        _main = packed.Instantiate();
        _main.Set("SecondsPerHour", 30.0);
        _main.Set("RngSeed", 2);
        _main.Set("SkipIntro", true);
        _main.Set("StartDay", 4);
        _main.Set("StartHour", 15.5);
        Root.AddChild(_main);
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        if (!_placed && _t > 0.2)
        {
            _placed = true;
            _main.GetNode<Node3D>("Player").Position = new Vector3(13f, 0.1f, -22f);
        }
        Set("ui_left", _t > 1.0 && _t < 7.0);    // 池の南を西へ
        Set("ui_up", _t > 7.2 && _t < 11.5);     // 池の西を北へ（すべり台のほうへ）
        Set("ui_right", _t > 11.7 && _t < 14.5);
        return false;
    }

    private static void Set(string action, bool on)
    {
        if (on)
            Input.ActionPress(action);
        else
            Input.ActionRelease(action);
    }
}

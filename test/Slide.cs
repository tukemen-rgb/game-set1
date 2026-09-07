using Godot;

/// <summary>
/// すべり台の検査。はしごの下に立って決定 → 登る → 滑る。
///   xvfb-run -a godot --path . --write-movie screenshots/slide/frame.png \
///     --fixed-fps 15 --quit-after 110 --script res://test/Swing.cs
/// </summary>
public partial class Slide : SceneTree
{
    private double _t;
    private Node _main;
    private bool _placed;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        _main = packed.Instantiate();
        _main.Set("SecondsPerHour", 60.0);
        _main.Set("RngSeed", 2);
        _main.Set("SkipIntro", true);
        _main.Set("StartDay", 4);
        _main.Set("StartHour", 15.0);
        Root.AddChild(_main);
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        if (!_placed && _t > 0.2)
        {
            _placed = true;
            _main.GetNode<Node3D>("Player").Position = new Vector3(-6.4f, 0.1f, -14.2f);
        }
        if (_t > 1.0 && _t < 1.12)
            Input.ActionPress("ui_accept");
        else
            Input.ActionRelease("ui_accept");
        return false;
    }
}

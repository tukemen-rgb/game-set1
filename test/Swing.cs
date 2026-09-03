using Godot;

/// <summary>
/// ブランコに乗る検査。座席の前に立って決定 → 6.5 秒こぐ → 降りる。
///   xvfb-run -a godot --path . --write-movie screenshots/swing/frame.png \
///     --fixed-fps 15 --quit-after 150 --script res://test/Swing.cs
/// </summary>
public partial class Swing : SceneTree
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
            _main.GetNode<Node3D>("Player").Position = new Vector3(-1.2f, 0.1f, -19.0f);
        }
        if (_t > 1.0 && _t < 1.12)
            Input.ActionPress("ui_accept");
        else
            Input.ActionRelease("ui_accept");
        return false;
    }
}

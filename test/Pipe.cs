using Godot;

/// <summary>
/// 土管の検査。土管の前に立って決定 → 入る（枠と音のこもり）→ Z で出る。
///   xvfb-run -a godot --path . --write-movie screenshots/pipe/frame.png \
///     --fixed-fps 15 --quit-after 90 --script res://test/Swing.cs
/// </summary>
public partial class Pipe : SceneTree
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
            _main.GetNode<Node3D>("Player").Position = new Vector3(19.6f, 0.1f, 0.3f);
        }
        if (_t > 1.0 && _t < 1.12)
            Input.ActionPress("ui_accept");
        else
            Input.ActionRelease("ui_accept");
        if (_t > 4.5 && _t < 4.62)
            Input.ActionPress("dex");
        else
            Input.ActionRelease("dex");
        return false;
    }
}

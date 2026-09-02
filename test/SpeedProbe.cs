using Godot;

/// <summary>
/// 歩き／走りの実測。2秒歩いて、2秒走って、それぞれの移動距離を出す。
///   godot --headless --path . --script res://test/SpeedProbe.cs --quit-after 240
/// </summary>
public partial class SpeedProbe : SceneTree
{
    private double _t;
    private Node3D _p;
    private Vector3 _a, _b;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 100000.0);
        main.Set("SkipIntro", true);
        Root.AddChild(main);
        _p = main.GetNode<Node3D>("Player");
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        if (_t < 0.5) return false;
        if (_t < 2.5) { if (_a == Vector3.Zero) _a = _p.GlobalPosition; Input.ActionPress("ui_right"); }
        else if (_t < 3.0) { Input.ActionRelease("ui_right"); GD.Print($"[walk] {(_p.GlobalPosition - _a).Length():F2} m / 2.0 s"); _b = _p.GlobalPosition; }
        else if (_t < 5.0) { Input.ActionPress("ui_right"); Input.ActionPress("run"); }
        else { Input.ActionRelease("ui_right"); Input.ActionRelease("run"); GD.Print($"[run] {(_p.GlobalPosition - _b).Length():F2} m / 2.0 s"); Quit(); }
        return false;
    }
}

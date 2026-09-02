using Godot;

/// <summary>
/// 走って近づくと虫が逃げるかの検査。公園の木 (-4,-9) へ走って向かう。
///   xvfb-run -a godot --path . --write-movie screenshots/startle/frame.png \
///     --fixed-fps 30 --quit-after 260 --script res://test/Startle.cs
/// 歩きで同じ動線を通す比較は STARTLE_WALK=1。
/// </summary>
public partial class Startle : SceneTree
{
    private double _t;
    private bool _walk;
    private Label _msg;
    private string _last = "";

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 300.0);
        main.Set("RngSeed", 2);
        main.Set("SkipIntro", true);
        Root.AddChild(main);
        _msg = main.GetNode<Label>("UI/MessageLabel");
        _walk = OS.GetEnvironment("STARTLE_WALK") == "1";
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        // 走りは 4.5m/s、歩きは 2.6m/s。同じ地点に着くよう時間を変える
        double scale = _walk ? 1.73 : 1.0;
        Set("run", !_walk);
        Set("ui_right", _t < 2.4 * scale);
        Set("ui_up", _t > 2.5 * scale && _t < 4.5 * scale);

        if (_msg.Text != _last)
        {
            _last = _msg.Text;
            GD.Print($"[{_t:F1}s] {_last.Replace("\n", " / ")}");
        }
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

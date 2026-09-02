using Godot;

/// <summary>
/// 町の人との立ち話の検査。出発 (-14,0) から団地の広場の二人に話しかける。
///   xvfb-run -a godot --path . --write-movie screenshots/talk/frame.png \
///     --fixed-fps 30 --quit-after 260 --script res://test/Talk.cs
/// </summary>
public partial class Talk : SceneTree
{
    private double _t;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 300.0);
        main.Set("RngSeed", 2);
        main.Set("SkipIntro", true);
        Root.AddChild(main);
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        // 西へ 4.6m、北へ 1.1m 進むと (-18.6, -1.1) の人の前
        Set("ui_left", _t < 1.8);
        Set("ui_up", _t > 1.9 && _t < 2.3);
        Set("ui_accept", _t > 2.8 && _t < 2.9);
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

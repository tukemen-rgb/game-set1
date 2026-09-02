using Godot;

/// <summary>
/// ずかんの検査。何種か捕った状態を作ってから Z で開く。
///   xvfb-run -a godot --path . --write-movie screenshots/dex/frame.png \
///     --fixed-fps 30 --quit-after 200 --script res://test/Dex.cs
/// セーブは触らない（SkipIntro）。捕獲は木のそばで虫あみを振って作る。
/// </summary>
public partial class Dex : SceneTree
{
    private double _t;
    private Node _main;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        _main = packed.Instantiate();
        _main.Set("SecondsPerHour", 400.0);   // 時刻はほぼ止める
        _main.Set("RngSeed", 2);
        _main.Set("SkipIntro", true);
        Root.AddChild(_main);
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        // 公園の木 (-4,-9) まで歩いて数回振り、そのあと Z で開く
        // 公園の木 (-4,-9) → (-8,-18) と回って振り、そのあと Z で開く
        Set("ui_right", _t < 3.9);
        Set("ui_up", (_t > 4.0 && _t < 7.5) || (_t > 8.2 && _t < 11.7));
        Set("ui_left", _t > 8.2 && _t < 9.8);
        Set("ui_accept", (_t > 7.7 && _t < 7.8) || (_t > 11.9 && _t < 12.0));
        Set("dex", _t > 12.6 && _t < 12.7);
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

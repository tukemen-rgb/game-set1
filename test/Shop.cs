using Godot;

/// <summary>
/// 駄菓子屋の検査用。店先まで歩いて話しかける。
///   xvfb-run -a godot --path . --write-movie screenshots/shop/frame.png \
///     --fixed-fps 30 --quit-after 520 --script res://test/Shop.cs
/// 店は商店街の南列 x=4, z=12.7。プレイヤーは (-14,0) から出発する。
/// </summary>
public partial class Shop : SceneTree
{
    private double _t;

    /// <summary>歩き速度にした分だけ動線の時間を伸ばす（Presentation.cs と同じ）。</summary>
    private const double Slow = 1.731;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 40.0 * Slow);
        main.Set("RngSeed", 2);
        main.Set("SkipIntro", true);
        Root.AddChild(main);
    }

    public override bool _Process(double delta)
    {
        _t += delta / Slow;
        // 東へ出て、商店街へ北上し、店先 (4, 14.6) に立って話しかける
        // 商店街は店の列で塞がれていて、入口は x=-4 のギャップだけ。
        // 東 → 入口から北 → 通路を東へ、の順でないと店先に着けない。
        Set("ui_right", _t < 2.3 || (_t > 5.9 && _t < 7.9));
        Set("ui_down", _t > 2.4 && _t < 5.8);
        Set("ui_accept", _t > 8.2 && _t < 8.3);
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

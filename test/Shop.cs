using Godot;

/// <summary>
/// 駄菓子屋の検査用。店先まで歩いて話しかける。
///   xvfb-run -a godot --path . --write-movie screenshots/shop/frame.png \
///     --fixed-fps 30 --quit-after 820 --script res://test/Shop.cs
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
        string day = OS.GetEnvironment("SHOP_DAY");
        if (day != "")
            main.Set("StartDay", day.ToInt());   // 8/24 は屋台の品書きになる
        Root.AddChild(main);
    }

    public override bool _Process(double delta)
    {
        _t += delta / Slow;
        // 東へ出て、商店街へ北上し、店先 (4, 14.6) に立って話しかける
        // 商店街は店の列で塞がれていて、入口は x=-4 のギャップだけ。
        // 東 → 入口から北 → 通路を東へ、の順でないと店先に着けない。
        // 8.2 一言 → 14.4 品書き（二押し方式）→ 右2回で「アイス」→ 15.6 で買う。
        // 買うのは品書きの描き直しから 0.3 秒後。ここが 1.2 秒未満だと
        // 結果の文が品書きの 60 秒の後ろに回って出なかった（監査 B4）。
        // わざと短くして、再発したら画面に品書きが残ることで分かるようにする
        Set("ui_right", (_t < 2.3) || (_t > 5.9 && _t < 7.9) || Tap(14.9) || Tap(15.3));
        Set("ui_down", _t > 2.4 && _t < 5.8);
        Set("ui_accept", Tap(8.2) || Tap(14.4) || Tap(15.6));
        return false;
    }

    /// <summary>その時刻から 0.12 秒だけ押す（1回の入力にする）。</summary>
    private bool Tap(double at) => _t > at && _t < at + 0.12;

    private static void Set(string action, bool on)
    {
        if (on)
            Input.ActionPress(action);
        else
            Input.ActionRelease(action);
    }
}

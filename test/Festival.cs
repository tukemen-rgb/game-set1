using Godot;

/// <summary>
/// 夏まつり（8月24日の夕方）の花火を検査する。
///   xvfb-run -a godot --path . --write-movie screenshots/festival/frame.png \
///     --fixed-fps 30 --quit-after 700 --script res://test/Festival.cs
/// 花火は17:30から上がるので、時間を早回しして夕方まで飛ばす。
/// </summary>
public partial class Festival : SceneTree
{
    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        // 8時から早回しすると、花火の時間帯（17:30〜19:00）が2秒しか無く
        // 数発しか撮れなかった。StartHour で夕方から始めて、時計はゆっくり進める
        main.Set("StartHour", 17.4);
        main.Set("SecondsPerHour", 40.0);
        main.Set("RngSeed", 2);
        main.Set("SkipIntro", true);
        main.Set("StartDay", 24);
        Root.AddChild(main);
    }

    private double _t;

    /// <summary>歩き速度にした分だけ動線の時間を伸ばす（Presentation.cs と同じ）。</summary>
    private const double Slow = 1.731;

    /// <summary>
    /// 団地の棟間は建物で空がほとんど見えないので、花火を見るには
    /// 開けた場所へ出る必要がある。検査でも公園まで歩かせる。
    /// （これは欠陥ではなく「花火は空が見える所へ行って見るもの」という設計）
    /// </summary>
    public override bool _Process(double delta)
    {
        _t += delta / Slow;
        if (_t < 6.0)
        {
            Input.ActionPress("ui_right");   // 棟間から東へ出る
            Input.ActionPress("ui_up");      // そのまま南の公園へ
        }
        else
        {
            Input.ActionRelease("ui_right");
            Input.ActionRelease("ui_up");
        }
        return false;
    }
}

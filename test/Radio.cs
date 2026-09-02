using Godot;

/// <summary>
/// ラジオ体操の検査。8時台に台の前まで歩いて、はんこを押す。
///   xvfb-run -a godot --path . --write-movie screenshots/radio/frame.png \
///     --fixed-fps 30 --quit-after 260 --script res://test/Radio.cs
/// 出発 (-14,0) から台 (-16,1.5) までは目と鼻の先なので、時刻は止めておく。
/// </summary>
public partial class Radio : SceneTree
{
    private double _t;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 300.0);   // 8時台のまま
        main.Set("RngSeed", 2);
        main.Set("SkipIntro", true);
        Root.AddChild(main);
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        Set("ui_left", _t < 0.8);        // 西へ 2m
        Set("ui_down", _t > 0.9 && _t < 1.5);  // 南へ 1.5m
        Set("ui_accept", _t > 2.0 && _t < 2.1);
        Set("dex", _t > 5.0 && _t < 5.1);      // ずかんでカードの枚数を見る
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

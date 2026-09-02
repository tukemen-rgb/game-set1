using Godot;

/// <summary>
/// タイトルと導入（オープニング）の検査用。SkipIntro を立てずにそのまま流す。
///   xvfb-run -a godot --path . --write-movie screenshots/intro/frame.png \
///     --fixed-fps 30 --quit-after 540 --script res://test/Intro.cs
/// </summary>
public partial class Intro : SceneTree
{
    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 20.0);
        main.Set("RngSeed", 2);
        Root.AddChild(main);
    }

    private double _t;

    /// <summary>タイトルで入力待ちになるので、1秒見せてから決定を押す。</summary>
    public override bool _Process(double delta)
    {
        _t += delta;
        if (_t > 1.0 && _t < 1.1)
            Input.ActionPress("ui_accept");
        else
            Input.ActionRelease("ui_accept");
        return false;
    }
}

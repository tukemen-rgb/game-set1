using Godot;

/// <summary>
/// 導入（オープニング）の検査用。SkipIntro を立てずにそのまま流す。
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
}

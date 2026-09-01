using Godot;

/// <summary>
/// 天気の検査用。CLOSEUP_DAY で日付を指定して、その日の空模様を撮る。
///   CLOSEUP_DAY=8 xvfb-run -a godot --path . --write-movie screenshots/weather/frame.png \
///     --fixed-fps 30 --quit-after 60 --script res://test/Weather.cs
/// 天気は日付から決定的に決まるので、同じ日は必ず同じ空になる。
/// </summary>
public partial class Weather : SceneTree
{
    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 100000.0);
        main.Set("RngSeed", 2);
        main.Set("SkipIntro", true);
        string d = OS.GetEnvironment("CLOSEUP_DAY");
        main.Set("StartDay", d == "" ? 8 : d.ToInt());
        Root.AddChild(main);
    }
}

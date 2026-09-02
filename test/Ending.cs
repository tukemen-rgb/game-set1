using Godot;

/// <summary>
/// 終わり（8月31日）の検査用。31日プレイせずに結末だけ確かめる。
///   xvfb-run -a godot --path . --write-movie screenshots/ending/frame.png \
///     --fixed-fps 30 --quit-after 1300 --script res://test/Ending.cs
/// </summary>
public partial class Ending : SceneTree
{
    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 0.35);   // 1日を数秒で終わらせて日没まで飛ばす
        main.Set("RngSeed", 2);
        main.Set("SkipIntro", true);
        // END_DAY=30 なら 8/30 から始めて、日をまたぐ朝の一言まで見られる
        string d = OS.GetEnvironment("END_DAY");
        main.Set("StartDay", d != "" ? d.ToInt() : 31);   // 既定は最終日から
        Root.AddChild(main);
    }
}

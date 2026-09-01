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
        main.Set("StartDay", 31);           // 最終日から始める
        Root.AddChild(main);
    }
}

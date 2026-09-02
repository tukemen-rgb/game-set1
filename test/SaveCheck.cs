using Godot;

/// <summary>
/// 中断と再開の検査。SkipIntro を立てないので、実際に保存・読込が走る。
///   CHECK_PHASE=save xvfb-run -a godot --path . --quit-after 900 --script res://test/SaveCheck.cs
///   CHECK_PHASE=load ...（2回目。前回の続きから始まるかを見る）
/// 導入（約16秒）→ 1日目の日没 → 2日目の朝に保存、という流れを通す。
/// </summary>
public partial class SaveCheck : SceneTree
{
    private Node _main;
    private double _t;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        _main = packed.Instantiate();
        _main.Set("SecondsPerHour", 0.5);   // 1日を約6秒に圧縮
        _main.Set("RngSeed", 2);
        Root.AddChild(_main);
        GD.Print($"[check] 開始 phase={OS.GetEnvironment("CHECK_PHASE")}");
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        if (_t > 1.0)
        {
            _t = -1e9;
            // 画面に出ている日付ラベルを読む。これが「続きから」の証拠になる
            var label = _main.GetNode<Label>("UI/DateLabel");
            GD.Print($"[check] 画面の日付 = 「{label.Text}」 / " +
                     $"セーブの有無 = {FileAccess.FileExists("user://summer_save.json")}");
        }
        return false;
    }
}

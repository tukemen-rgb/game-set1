using Godot;

/// <summary>
/// 検証・記録用のキャプチャスクリプト。実行方法（godot.md 参照）:
///   xvfb-run -a godot --path . --write-movie screenshots/run/frame.png \
///     --fixed-fps 30 --quit-after 560 --script res://test/Presentation.cs
/// 時間を加速した1日（朝→夕→日記）を、スクリプト入力で4つの固定カメラ
/// ゾーンを巡りながら撮る。ライブ入力は使わない。
/// </summary>
public partial class Presentation : SceneTree
{
    private double _t;

    // (開始秒, 終了秒, アクション) のタイムライン。
    // 川辺の木2本 (6,-9.5) と (15,-8.5) の前を通ってセミとりを試みる
    private static readonly (double Start, double End, string Action)[] Plan =
    {
        (0.6, 2.6, "ui_right"),     // 庭 → 東へ（原っぱカメラへ）
        (2.6, 4.4, "ui_down"),      // 北の田舎道へ（望遠カメラ）
        (4.4, 5.2, "ui_right"),     // 道を少し歩く
        (5.2, 10.0, "ui_up"),       // 南下して川辺の木 (6,-9.5) の前へ
        (10.2, 10.3, "ui_accept"),  // 虫あみを振る
        (10.7, 10.8, "ui_accept"),
        (10.9, 12.9, "ui_right"),   // 川沿いを東へ、木 (15,-8.5) の前へ
        (13.1, 13.2, "ui_accept"),
        (13.6, 13.7, "ui_accept"),
        (14.0, 14.6, "ui_left"),    // 夕焼けの川辺で立ち止まる
    };

    private static readonly string[] Actions = { "ui_left", "ui_right", "ui_up", "ui_down", "ui_accept" };

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 1.4); // 1.4秒 = ゲーム内1時間 → 1日 ≒ 15.4秒
        Root.AddChild(main);
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        foreach (string action in Actions)
        {
            bool active = false;
            foreach (var seg in Plan)
            {
                if (seg.Action == action && _t >= seg.Start && _t < seg.End)
                {
                    active = true;
                    break;
                }
            }
            if (active)
                Input.ActionPress(action);
            else
                Input.ActionRelease(action);
        }
        return false;
    }
}

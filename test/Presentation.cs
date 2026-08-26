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
    // 団地の広場 → 大通りを渡って商店街 → 公園の木 (-4,-9) へ
    private static readonly (double Start, double End, string Action)[] Plan =
    {
        (0.6, 3.0, "ui_right"),     // 団地の広場 → 東へ（大通りカメラへ）
        (3.0, 6.6, "ui_down"),      // 横断歩道を渡り、入口から商店街へ（望遠カメラ）
        (6.8, 8.6, "ui_left"),      // アーケードの下を西へ歩く
        (8.8, 10.4, "ui_right"),    // 入口（x=-4 のギャップ）へ戻る
        (10.4, 16.0, "ui_up"),      // 入口を抜けて南下、公園の木 (-4,-9) へ
        (16.1, 16.2, "ui_accept"),  // 虫あみを振る
        (16.4, 17.4, "ui_left"),    // 隣の木 (-8,-18) へ移動
        (16.4, 18.3, "ui_up"),
        (18.4, 18.5, "ui_accept"),
    };

    private static readonly string[] Actions = { "ui_left", "ui_right", "ui_up", "ui_down", "ui_accept" };

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 1.7); // 1.7秒 = ゲーム内1時間 → 1日 ≒ 18.7秒
        main.Set("RngSeed", 2); // 動線上の木にセミが湧き捕獲成功する seed（test/SeedProbe.cs で探索）
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

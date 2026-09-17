using Godot;

/// <summary>
/// 便利屋（案B）の試作を通しで走らせる検査。瞬間移動で依頼板→依頼主→こなす場所を回す。
///   DEBUG_MSG=1 godot --headless --path . --fixed-fps 30 --quit-after 1500 --script res://test/Benriya.cs
///   xvfb-run -a godot --path . --write-movie screenshots/benriya/frame.png --fixed-fps 15 --quit-after 750 --script res://test/Benriya.cs
/// BENRIYA_FAST=1 で 1 手 1.2 秒に詰める（ヘッドレスの論理確認用）。
/// </summary>
public partial class Benriya : SceneTree
{
    private double _t;
    private int _step;
    private Node _main;
    private (double At, char Kind, Vector3 Pos)[] _plan;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/benriya_main.tscn");
        _main = packed.Instantiate();
        _main.Set("SecondsPerHour", 60.0);
        Root.AddChild(_main);
        double g = OS.GetEnvironment("BENRIYA_FAST") == "1" ? 1.2 : 3.2;   // 1 手の間隔
        // P=瞬間移動 A=決定 Z=閉じる D=下（依頼板で次を選ぶ）
        var list = new System.Collections.Generic.List<(double, char, Vector3)>();
        double t = 1.0;
        void Go(Vector3 p) { list.Add((t, 'P', p)); t += 0.3; }
        void Press(char k) { list.Add((t, k, Vector3.Zero)); t += g; }
        Vector3 board = new(5.2f, 0.1f, 5.4f), kanrinin = new(-6f, 0.1f, -2.2f), sasaki = new(-18.6f, 0.1f, 0.6f);
        Vector3 monohoshi = new(-25f, 0.1f, -1.8f), komeya = new(-8f, 0.1f, 15.2f), kaidan = new(-16f, 0.1f, -8f);
        Vector3 ojiisan = new(-1.2f, 0.1f, 16.9f), pond = new(14.6f, 0.1f, -15f), bus = new(36f, 0.1f, 10f);
        // 依頼1: 回覧板
        Go(board); Press('A'); Press('A'); Go(kanrinin); Press('A'); Go(sasaki); Press('A');
        // 依頼2: 物干し（工具箱）
        Go(board); Press('A'); Press('A'); Go(sasaki); Press('A'); Go(monohoshi); Press('A');
        // 依頼3: 米（台車）→ 原付
        Go(board); Press('A'); Press('A'); Go(komeya); Press('A'); Go(kaidan); Press('A');
        // 依頼4: 写真
        Go(board); Press('A'); Press('A'); Go(ojiisan); Press('A'); Go(pond); Press('A');
        // 依頼5: 隣の団地（原付）
        Go(board); Press('A'); Press('A'); Go(kanrinin); Press('A'); Go(bus); Press('A');
        // 写真を見る → 回想
        Go(pond); Press('A');
        _plan = list.ToArray();
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        Input.ActionRelease("ui_accept"); Input.ActionRelease("dex"); Input.ActionRelease("ui_down");
        if (_step >= _plan.Length || !IsInstanceValid(_main) || _main.GetNodeOrNull("Player") == null)
            return false;
        (double at, char kind, Vector3 pos) = _plan[_step];
        if (_t < at)
            return false;
        _step++;
        switch (kind)
        {
            case 'P': _main.GetNode<Node3D>("Player").Position = pos; break;
            case 'A': Input.ActionPress("ui_accept"); break;
            case 'Z': Input.ActionPress("dex"); break;
            case 'D': Input.ActionPress("ui_down"); break;
        }
        return false;
    }
}

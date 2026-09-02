using Godot;

/// <summary>
/// 固定カメラごとに、主人公が画面で何ピクセルになるかを実測する。
///   godot --headless --path . --script res://test/CamMetric.cs
/// 「小さすぎる気がする」を数字にしないと、カメラを動かす判断ができない。
/// 足元と頭を UnprojectPosition して、その差を高さとして出す。
/// </summary>
public partial class CamMetric : SceneTree
{
    private Node3D _player;
    private int _step;

    // ゾーンごとに「端」と「中央」を見る。端で小さくなるのが本当の問題
    private static readonly (string Label, Vector3 At)[] Probes =
    {
        ("団地 広場",   new Vector3(-14f, 0.1f, 0f)),
        ("団地 西端",   new Vector3(-26f, 0.1f, 6f)),
        ("大通り",      new Vector3(2f, 0.1f, 2f)),
        ("大通り 西",   new Vector3(-8f, 0.1f, 8f)),
        ("大通り 北",   new Vector3(0f, 0.1f, 10f)),
        ("空き地 東端", new Vector3(25f, 0.1f, 6f)),
        ("商店街 店先", new Vector3(4f, 0.1f, 14.6f)),
        ("商店街 西端", new Vector3(-16f, 0.1f, 15.5f)),
        ("公園 池ぎわ", new Vector3(6f, 0.1f, -7.6f)),
        ("公園 西端",   new Vector3(-20f, 0.1f, -16f)),
    };

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 100000.0);
        main.Set("RngSeed", 2);
        main.Set("SkipIntro", true);
        Root.AddChild(main);
        _player = main.GetNode<Node3D>("Player");
    }

    public override bool _Process(double delta)
    {
        // 1点につき2フレーム使う（移動 → カメラ切替が効いたあとに測る）
        int idx = _step / 2;
        if (idx >= Probes.Length)
        {
            Quit();
            return false;
        }
        if (_step % 2 == 0)
        {
            _player.GlobalPosition = Probes[idx].At;
        }
        else
        {
            Camera3D cam = Root.GetViewport().GetCamera3D();
            Vector2 foot = cam.UnprojectPosition(_player.GlobalPosition);
            Vector2 head = cam.UnprojectPosition(_player.GlobalPosition + new Vector3(0f, 1.5f, 0f));
            float px = Mathf.Abs(foot.Y - head.Y);
            float pct = px / Root.GetViewport().GetVisibleRect().Size.Y * 100f;
            GD.Print($"[metric] {Probes[idx].Label,-12} {cam.Name,-10} " +
                     $"{px,6:F1} px  ({pct:F1}% / 画面 {(int)Root.GetViewport().GetVisibleRect().Size.Y}px)");
        }
        _step++;
        return false;
    }
}

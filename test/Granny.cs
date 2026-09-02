using Godot;

/// <summary>
/// 駄菓子屋のおばあさんの一言の検査。店先へ運んで話しかける。
///   GRANNY_DAY=7 xvfb-run -a godot --path . --fixed-fps 30 --quit-after 120 \
///     --script res://test/Granny.cs
/// 8/7 は「あしたは雨」、8/8 は雨の日、それ以外はふだんの一言。
/// </summary>
public partial class Granny : SceneTree
{
    private double _t;
    private Node3D _player;
    private Label _msg;
    private string _last = "";

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 300.0);
        main.Set("RngSeed", 2);
        main.Set("SkipIntro", true);
        string d = OS.GetEnvironment("GRANNY_DAY");
        main.Set("StartDay", d != "" ? d.ToInt() : 7);
        Root.AddChild(main);
        _player = main.GetNode<Node3D>("Player");
        _msg = main.GetNode<Label>("UI/MessageLabel");
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        if (_t < 0.1)
            _player.GlobalPosition = new Vector3(4f, 0.1f, 14.6f);   // 店先
        // 1回目で一言、2回目で品書き
        if ((_t > 2.0 && _t < 2.1) || (_t > 3.5 && _t < 3.6))
            Input.ActionPress("ui_accept");
        else
            Input.ActionRelease("ui_accept");
        if (_msg.Text != _last)
        {
            _last = _msg.Text;
            GD.Print($"[{_t:F1}s] {_last.Replace("\n", " / ")}");
        }
        return false;
    }
}

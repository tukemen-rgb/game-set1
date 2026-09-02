using Godot;

/// <summary>
/// 自販機の下の十円玉の検査。落ちている日（8/9）と落ちていない日（8/10）を
/// 同じ動線で比べる。
///   COIN_DAY=9 xvfb-run -a godot --path . --fixed-fps 30 --quit-after 300 \
///     --script res://test/Coin.cs
/// </summary>
public partial class Coin : SceneTree
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
        string d = OS.GetEnvironment("COIN_DAY");
        main.Set("StartDay", d != "" ? d.ToInt() : 9);
        Root.AddChild(main);
        _player = main.GetNode<Node3D>("Player");
        _msg = main.GetNode<Label>("UI/MessageLabel");
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        if (_t < 0.1)
            _player.GlobalPosition = new Vector3(14.5f, 0.1f, 9.2f);   // 自販機の前
        if (_t > 2.0 && _t < 2.1)
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

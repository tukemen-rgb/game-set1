using Godot;

/// <summary>
/// 発見した場所へ夏の後半にもう一度来ると「その後」が出るかの検査。
/// 8/20 から始めて、公園の池ぎわ (6,-8) へ2回入る。
///   xvfb-run -a godot --path . --write-movie screenshots/revisit/frame.png \
///     --fixed-fps 30 --quit-after 300 --script res://test/Revisit.cs
/// </summary>
public partial class Revisit : SceneTree
{
    private double _t;
    private Node3D _player;
    private Label _msg;
    private string _last = "";

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 1.2);   // 1日を約13秒で回して、翌日に戻る
        main.Set("RngSeed", 2);
        main.Set("SkipIntro", true);
        main.Set("StartDay", 20);       // LaterDay(16) を過ぎた日
        Root.AddChild(main);
        _player = main.GetNode<Node3D>("Player");
        _msg = main.GetNode<Label>("UI/MessageLabel");
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        // 池ぎわへ入る → いったん離れる → もう一度入る
        if (_t < 0.1)
            _player.GlobalPosition = new Vector3(6f, 0.1f, -7.6f);
        else if (_t > 3.0 && _t < 3.1)
            _player.GlobalPosition = new Vector3(-20f, 0.1f, 0f);   // いったん離れる
        else if (_t > 22.0 && _t < 22.1)
            _player.GlobalPosition = new Vector3(6f, 0.1f, -7.6f);  // 翌日に戻る

        if (_msg.Text != _last)
        {
            _last = _msg.Text;
            GD.Print($"[{_t:F1}s] {_last.Replace("\n", " / ")}");
        }
        return false;
    }
}

using Godot;

/// <summary>
/// 池のザリガニつりの検査。ふちまで歩いて糸を垂らし、
/// 当たりの合図（メッセージに「ツン」が出る）を見てから引く。
///   xvfb-run -a godot --path . --write-movie screenshots/fishing/frame.png \
///     --fixed-fps 30 --quit-after 900 --script res://test/Fishing.cs
/// 当たりの時刻は乱数なので、秒数直書きでは検査できない。画面の文言で待つ。
/// </summary>
public partial class Fishing : SceneTree
{
    private double _t;
    private Label _msg;
    private Node3D _player;
    private bool _dropped;
    private double _hold = -1.0;   // ボタンを離すまでの残り
    private string _last = "";
    private Camera3D _cam;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node main = packed.Instantiate();
        main.Set("SecondsPerHour", 200.0);   // 時刻はほぼ止める
        main.Set("RngSeed", 2);
        main.Set("SkipIntro", true);
        Root.AddChild(main);
        _msg = main.GetNode<Label>("UI/MessageLabel");
        _player = main.GetNode<Node3D>("Player");

        // FISH_CLOSEUP=1 で寄りのカメラ。固定カメラだと主人公が点にしか映らず、
        // 竿と糸とウキが出ているかを目視できない
        if (OS.GetEnvironment("FISH_CLOSEUP") == "1")
        {
            _cam = new Camera3D { Fov = 34f };
            Root.AddChild(_cam);
        }
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        if (_cam != null)
        {
            _cam.MakeCurrent();
            Vector3 at = _player.GlobalPosition + new Vector3(0f, 0.8f, -0.5f);
            _cam.LookAtFromPosition(at + new Vector3(2.1f, 0.5f, 1.6f), at, Vector3.Up);
        }
        // 出発 (-14,0) から池のふち (6,-7.6) へ歩く
        Vector3 p = _player.GlobalPosition;
        bool arrived = p.X > 5.6f && p.Z < -7.2f;
        Input.ActionRelease("ui_right");
        Input.ActionRelease("ui_up");
        if (!arrived)
        {
            if (p.X < 6.0f) Input.ActionPress("ui_right");
            if (p.Z > -7.6f) Input.ActionPress("ui_up");
            return false;
        }

        if (_hold > 0.0)
        {
            _hold -= delta;
            if (_hold <= 0.0)
                Input.ActionRelease("ui_accept");
            return false;
        }

        string text = _msg.Text;
        if (text != _last)
        {
            _last = text;
            GD.Print($"[{_t:F1}s / frame {(int)(_t * 30)}] {text.Replace("\n", " / ")}");
        }
        if (!_dropped)
        {
            _dropped = true;
            Press();                       // 糸を垂らす
        }
        else if (text.Contains("ツン"))
        {
            Press();                       // 当たりに応える
            _dropped = false;
        }
        else if (text.Contains("つりあげた") || text.Contains("はなされた") || text.Contains("いかれた"))
        {
            _dropped = false;              // 次の1回へ
        }
        return false;
    }

    private void Press()
    {
        Input.ActionPress("ui_accept");
        _hold = 0.08;
    }
}

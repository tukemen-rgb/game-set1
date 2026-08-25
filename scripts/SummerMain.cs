using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 「昭和の夏休み」風ゲームの進行管理。
/// 8月1日〜31日の日数システム、時刻による空の変化、固定カメラの切り替え、
/// セミとり、1日の終わりの日記を担当する。世界の見た目は
/// scenes/BuildSummer.cs（ビルド時シーン生成）が作る。
/// </summary>
public partial class SummerMain : Node3D
{
    /// <summary>実時間◯秒 = ゲーム内1時間。キャプチャ時は外から小さくする。</summary>
    [Export]
    public double SecondsPerHour { get; set; } = 20.0;

    private const double DayStartHour = 8.0;
    private const double DayEndHour = 19.0;
    private const int LastDay = 31;
    private const int CicadasPerDay = 4;
    private const float CatchRange = 2.2f;

    private int _day = 1;
    private double _hour = DayStartHour;
    private int _totalCaught;
    private int _todayCaught;
    private bool _transitioning;
    private bool _vacationOver;
    private double _messageTimer;

    private PlayerController _player;
    private DirectionalLight3D _sun;
    private Godot.Environment _env;
    private Label _dateLabel;
    private Label _bugLabel;
    private Label _messageLabel;
    private ColorRect _fade;
    private Node3D _cameras;
    private string _zone = "";
    private readonly List<Node3D> _cicadas = new();
    private readonly List<Vector3> _treeSpots = new();
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _player = GetNode<PlayerController>("Player");
        _sun = GetNode<DirectionalLight3D>("Sun");
        _env = GetNode<WorldEnvironment>("Env").Environment;
        _dateLabel = GetNode<Label>("UI/DateLabel");
        _bugLabel = GetNode<Label>("UI/BugLabel");
        _messageLabel = GetNode<Label>("UI/MessageLabel");
        _fade = GetNode<ColorRect>("UI/Fade");
        _cameras = GetNode<Node3D>("Cameras");
        foreach (Node child in GetNode("Trees").GetChildren())
        {
            if (child is Node3D tree)
                _treeSpots.Add(tree.Position);
        }
        _rng.Randomize();
        RespawnCicadas();
        UpdateCamera(force: true);
        ShowMessage("8月1日。いなかの なつやすみが はじまった！", 4.0);
    }

    public override void _Process(double delta)
    {
        if (_vacationOver || _transitioning)
            return;
        _hour += delta / SecondsPerHour;
        UpdateSky();
        UpdateLabels();
        UpdateCamera();
        UpdateMessages(delta);
        CheckCatch();
        if (_hour >= DayEndHour)
            _ = EndDay();
    }

    // --- 固定カメラ（場面ごとに1アングル、位置で自動切替） ---

    private string ZoneFor(Vector3 p)
    {
        if (p.X < -5f && p.Z < 6f)
            return "CamYard";   // 家の庭：斜め見下ろし
        if (p.Z > 4.5f)
            return "CamRoad";   // 田舎道：ローアングル望遠
        if (p.Z < -6f)
            return "CamRiver";  // 川辺：対岸からの引き
        return "CamField";      // 原っぱ：高め俯瞰
    }

    private void UpdateCamera(bool force = false)
    {
        string zone = ZoneFor(_player.Position);
        if (zone == _zone && !force)
            return;
        _zone = zone;
        _cameras.GetNode<Camera3D>(zone).MakeCurrent();
    }

    // --- 時刻と空 ---

    private void UpdateSky()
    {
        float t = Mathf.Clamp((float)((_hour - 6.0) / 13.0), 0f, 1f);
        var morning = new Color(0.72f, 0.82f, 0.95f);
        var noon = new Color(0.45f, 0.72f, 0.95f);
        var sunset = new Color(0.97f, 0.55f, 0.35f);
        var gold = new Color(0.93f, 0.75f, 0.48f);
        Color sky;
        if (_hour < 9.0)
        {
            sky = morning.Lerp(noon, Mathf.Clamp((float)((_hour - 6.0) / 3.0), 0f, 1f));
        }
        else if (_hour < 16.0)
        {
            sky = noon;
        }
        else
        {
            // 青→橙を直接補間すると灰色に濁るので、金色を経由して夕焼けにする
            float u = Mathf.Clamp((float)((_hour - 16.0) / 3.0), 0f, 1f);
            sky = u < 0.5f ? noon.Lerp(gold, u * 2f) : gold.Lerp(sunset, (u - 0.5f) * 2f);
        }
        _env.BackgroundColor = sky;
        _env.FogLightColor = sky;
        _env.AmbientLightColor = sky.Lerp(Colors.White, 0.45f) * 0.8f;
        float arc = Mathf.Sin(t * Mathf.Pi);
        _sun.RotationDegrees = new Vector3(-10f - 60f * arc, -150f + 120f * t, 0f);
        _sun.LightEnergy = 0.55f + 0.65f * arc;
        _sun.LightColor = new Color(1f, 0.72f + 0.26f * arc, 0.55f + 0.4f * arc);
    }

    private string Weekday()
    {
        // 1975年8月1日は金曜日
        string[] youbi = { "金", "土", "日", "月", "火", "水", "木" };
        return youbi[(_day - 1) % 7];
    }

    private void UpdateLabels()
    {
        int h = (int)_hour;
        int m = (int)((_hour - h) * 60.0);
        _dateLabel.Text = $"8月{_day}日({Weekday()})  {h:D2}:{m:D2}";
        _bugLabel.Text = $"セミ ×{_totalCaught}";
    }

    // --- メッセージ ---

    private void ShowMessage(string text, double seconds = 2.5)
    {
        _messageLabel.Text = text;
        _messageTimer = seconds;
    }

    private void UpdateMessages(double delta)
    {
        if (_messageTimer > 0.0)
        {
            _messageTimer -= delta;
            return;
        }
        _messageLabel.Text = NearestCicada() != null ? "スペースで 虫あみを ふる！" : "";
    }

    // --- セミとり ---

    private void RespawnCicadas()
    {
        foreach (Node3D c in _cicadas)
            c.QueueFree();
        _cicadas.Clear();
        var indices = new List<int>();
        for (int i = 0; i < _treeSpots.Count; i++)
            indices.Add(i);
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = (int)(_rng.Randi() % (uint)(i + 1));
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        int count = Mathf.Min(CicadasPerDay, indices.Count);
        for (int k = 0; k < count; k++)
        {
            var spot = new Node3D { Position = _treeSpots[indices[k]] };
            var body = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.26f, 0.12f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.28f, 0.2f, 0.1f), Roughness = 1f },
                Position = new Vector3(0.34f, 1.7f, 0f),
            };
            spot.AddChild(body);
            AddChild(spot);
            _cicadas.Add(spot);
        }
    }

    private Node3D NearestCicada()
    {
        Node3D best = null;
        float bestDist = CatchRange;
        foreach (Node3D c in _cicadas)
        {
            float d = _player.Position.DistanceTo(c.Position);
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }
        return best;
    }

    private void CheckCatch()
    {
        Node3D spot = NearestCicada();
        if (spot == null || !Input.IsActionJustPressed("ui_accept"))
            return;
        _cicadas.Remove(spot);
        spot.QueueFree();
        if (_rng.Randf() < 0.65f)
        {
            _totalCaught++;
            _todayCaught++;
            ShowMessage("ミンミンゼミを つかまえた！");
        }
        else
        {
            ShowMessage("あっ、にげられた……");
        }
    }

    // --- 1日の終わり（日記→翌朝） ---

    private string DiaryText()
    {
        string line = _todayCaught switch
        {
            0 => "きょうは セミが とれなかった。",
            1 => "セミを 1ぴき つかまえた。",
            _ => $"セミを {_todayCaught}ひきも つかまえた！",
        };
        return $"【日記】8月{_day}日({Weekday()})\n{line}\nあしたは なにを しようかな。";
    }

    private async Task EndDay()
    {
        _transitioning = true;
        _player.Frozen = true;
        Tween fadeOut = CreateTween();
        fadeOut.TweenProperty(_fade, "color:a", 1.0f, 1.0);
        await ToSignal(fadeOut, Tween.SignalName.Finished);

        if (_day >= LastDay)
        {
            _vacationOver = true;
            _messageLabel.Text =
                $"8月31日。なつやすみが おわった。\nつかまえたセミ、ぜんぶで {_totalCaught}ひき。\nまた らいねん！";
            return;
        }

        _messageLabel.Text = DiaryText();
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

        _day++;
        _hour = DayStartHour;
        _todayCaught = 0;
        _player.Position = new Vector3(-6f, 0.1f, 4f);
        RespawnCicadas();
        _messageLabel.Text = "";
        Tween fadeIn = CreateTween();
        fadeIn.TweenProperty(_fade, "color:a", 0.0f, 1.0);
        await ToSignal(fadeIn, Tween.SignalName.Finished);
        _player.Frozen = false;
        _transitioning = false;
        ShowMessage($"8月{_day}日の あさ。ラジオたいそう おわり！", 3.0);
    }
}

using Godot;
using System.Collections.Generic;

/// <summary>
/// 案B: 2026 年、老いたニュータウンに戻った 30 代の便利屋。
/// 依頼板で依頼を受け、こなし、報酬は金ではなく物（工具箱・台車・原付・古い写真）。
/// 物が増えるほど、受けられる依頼と行ける場所が広がる。締切も失敗も暴力も無い。
/// 古い写真を池で見ると、同じ場所の 2000 年の夏（summer_main）へ回想で切り替わる。
/// 試作の範囲: 依頼 5 件・道具 2 つ・原付・住人 3 人の連なり 1 本。
/// </summary>
public partial class BenriyaMain : Node3D
{
    [Export] public double SecondsPerHour { get; set; } = 20.0;
    [Export] public int StartDay { get; set; } = 4;
    [Export] public double StartHour { get; set; } = 9.0;

    private enum Kind { Deliver, Fix, Find, Listen }

    /// <summary>依頼。Target は「こなす場所」。NeedTool が無ければ受けた時点で行ける。</summary>
    private readonly record struct Request(int Id, string Client, Vector2 ClientPos, string Title, string Ask,
        Kind Kind, Vector2 Target, string TargetLabel, string NeedTool, string Reward, string Done, int Unlocks);

    private static readonly Request[] Requests =
    {
        new(1, "管理人", new Vector2(-6f, -4f), "回覧板を 3号棟へ",
            "「回覧板、3号棟の 佐々木さんに 届けてくれんか。\nわしは 膝が もう だめでな」",
            Kind.Deliver, new Vector2(-18.6f, -1.1f), "3号棟の 佐々木さん", "", "工具箱",
            "「助かった。これ、亡くなった 前の 管理人の 工具箱だ。\n使って やってくれ」", 2),
        new(2, "佐々木さん", new Vector2(-18.6f, -1.1f), "物干しの 修理",
            "「ベランダの 物干しが 傾いてね。\n工具が あれば 直せると 思うんだけど」",
            Kind.Fix, new Vector2(-25f, -3.4f), "3号棟の 物干し場", "工具箱", "台車",
            "「まあ、まっすぐに なった。\n台車、うちの 人のだけど もう 使わないから」", 3),
        new(3, "米屋の 田中さん", new Vector2(-8f, 13.5f), "米 20kg を 3号棟へ",
            "「米を 20 キロ、3号棟の 階段の 下まで。\n昔は わしが 担いだが、もう 無理だ」",
            Kind.Deliver, new Vector2(-16f, -9.5f), "3号棟の 階段の 下", "台車", "原付",
            "「おう、ご苦労。裏の カブ、もう 乗らんから 持ってけ。\n隣の 団地まで 行ける」", 5),
        new(4, "ベンチの おじいさん", new Vector2(-1.2f, 15.3f), "落とした 写真",
            "「公園の 池の ところで、古い 写真を 落としてなあ。\n2000年の 夏の。孫が 写っとる」",
            Kind.Find, new Vector2(13.5f, -15f), "公園の 池の ふち", "", "古い写真",
            "「これだ、これだ。……あんた、もしかして この 子か？」", 0),
        new(5, "管理人", new Vector2(-6f, -4f), "隣の 団地へ 荷物",
            "「隣の 団地の バス停まで、この 段ボールを。\n歩きじゃ 遠い。乗り物が あるなら 頼む」",
            Kind.Deliver, new Vector2(36f, 8.5f), "隣の 団地の バス停", "原付", "鍵束",
            "「ありがとう。……これ、空き部屋の 鍵束だ。\n次の 依頼は、また 明日」", 0),
    };

    private static readonly Vector2 BoardPos = new(5.2f, 3.4f);
    private static readonly Vector2 PondCenter = new(6f, -15f);

    private PlayerController _player;
    private Node3D _cameras;
    private string _zone = "";
    private Label _dateLabel, _bugLabel, _messageLabel;
    private ColorRect _fade;
    private WorldEnvironment _envNode;
    private DirectionalLight3D _sun;
    private ProceduralSkyMaterial _sky;

    private int _day;
    private double _hour;
    private readonly HashSet<int> _available = new() { 1, 4 };
    private readonly HashSet<int> _accepted = new();
    private readonly HashSet<int> _done = new();
    private readonly HashSet<string> _tools = new();
    private int _carrying = -1;          // 荷物を持っている依頼の番号
    private int _listenStep;             // 「話を聞く」の進み
    private bool _boardOpen;
    private int _pick;
    private double _messageTimer;
    private bool _pressUsed;
    private bool _transitioning;

    public override void _Ready()
    {
        _player = GetNode<PlayerController>("Player");
        _cameras = GetNode<Node3D>("Cameras");
        _dateLabel = GetNode<Label>("UI/DateLabel");
        _bugLabel = GetNode<Label>("UI/BugLabel");
        _messageLabel = GetNode<Label>("UI/MessageLabel");
        _fade = GetNode<ColorRect>("UI/Fade");
        _envNode = GetNodeOrNull<WorldEnvironment>("Env");
        _sun = GetNodeOrNull<DirectionalLight3D>("Sun");
        _sky = _envNode?.Environment?.Sky?.SkyMaterial as ProceduralSkyMaterial;
        if (GetNodeOrNull<CanvasLayer>("Title") is CanvasLayer title)
            title.Visible = false;

        _day = StartDay;
        _hour = StartHour;
        if (BenriyaState.Restore(this))
        {
            ShowMessage("……目を あけると、2026年の 池の ふちだった。\n写真は、ポケットに 入って いた。", 5.0);
        }
        else
        {
            ShowMessage("2026年8月4日。\n会社を やめて、団地に もどって 3日目。\nなんでも屋を はじめた。", 5.0);
        }
        _fade.Color = new Color(0f, 0f, 0f, 0f);
        UpdateCamera(force: true);
        UpdateHud();
    }

    public override void _Process(double delta)
    {
        if (_transitioning)
            return;
        if (!_boardOpen)
            _hour += delta / SecondsPerHour;
        if (_hour >= 19.0)
            _hour = 19.0;   // 試作では日を回さない
        UpdateSky();
        UpdateCamera();
        UpdateHud();
        _pressUsed = false;
        if (_boardOpen)
        {
            UpdateBoardInput();
            return;
        }
        UpdateMessages(delta);
        CheckBoard();
        CheckClients();
        CheckTargets();
        CheckPhoto();
    }

    // --- 入力 ---

    private bool TakePress()
    {
        if (_pressUsed || !Input.IsActionJustPressed("ui_accept"))
            return false;
        _pressUsed = true;
        return true;
    }

    private Vector2 PlayerXZ() => new(_player.Position.X, _player.Position.Z);
    private bool Near(Vector2 at, float range) => PlayerXZ().DistanceTo(at) < range;

    // --- 依頼板 ---

    private void CheckBoard()
    {
        if (!Near(BoardPos, 2.4f) || !TakePress())
            return;
        _boardOpen = true;
        _pick = 0;
        _player.Frozen = true;
        ShowBoard();
    }

    private List<int> BoardList()
    {
        var list = new List<int>();
        foreach (Request r in Requests)
        {
            if (_available.Contains(r.Id) && !_done.Contains(r.Id))
                list.Add(r.Id);
        }
        return list;
    }

    private void ShowBoard()
    {
        List<int> list = BoardList();
        var sb = new System.Text.StringBuilder("【依頼板】\n");
        if (list.Count == 0)
            sb.Append("いまは 張り紙が ない。\n");
        for (int i = 0; i < list.Count; i++)
        {
            Request r = Requests[list[i] - 1];
            string state = _accepted.Contains(r.Id) ? "（うけた）" : "";
            string need = r.NeedTool != "" && !_tools.Contains(r.NeedTool) ? $"　※{r.NeedTool}が いる" : "";
            sb.Append(i == _pick ? "▶ " : "　 ").Append(r.Title).Append("　— ").Append(r.Client).Append(state).Append(need).Append('\n');
        }
        sb.Append("やじるしで えらぶ／スペースで うける／Ｚ で とじる");
        _messageLabel.Text = sb.ToString();
        _messageTimer = 600.0;
    }

    private void UpdateBoardInput()
    {
        List<int> list = BoardList();
        if (Input.IsActionJustPressed("dex"))
        {
            CloseBoard();
            return;
        }
        if (list.Count > 0 && (Input.IsActionJustPressed("ui_down") || Input.IsActionJustPressed("ui_right")))
        {
            _pick = (_pick + 1) % list.Count;
            ShowBoard();
            return;
        }
        if (list.Count > 0 && (Input.IsActionJustPressed("ui_up") || Input.IsActionJustPressed("ui_left")))
        {
            _pick = (_pick + list.Count - 1) % list.Count;
            ShowBoard();
            return;
        }
        if (!TakePress() || list.Count == 0)
            return;
        Request r = Requests[list[Mathf.Clamp(_pick, 0, list.Count - 1)] - 1];
        CloseBoard();
        if (_accepted.Contains(r.Id))
        {
            ShowMessage($"「{r.Title}」は もう うけて いる。\n{r.Client}の ところへ。", 3.0);
            return;
        }
        _accepted.Add(r.Id);
        ShowMessage($"「{r.Title}」を うけた。\nまず {r.Client}に 話を 聞こう。", 3.5);
    }

    private void CloseBoard()
    {
        _boardOpen = false;
        _player.Frozen = false;
        _messageTimer = 0.0;
        _messageLabel.Text = "";
    }

    // --- 依頼主・こなす場所 ---

    private Request? AcceptedAt(Vector2 pos, float range)
    {
        foreach (Request r in Requests)
        {
            if (_accepted.Contains(r.Id) && !_done.Contains(r.Id) && pos.DistanceTo(r.ClientPos) < range)
                return r;
        }
        return null;
    }

    private void CheckClients()
    {
        foreach (Request r in Requests)
        {
            if (!_accepted.Contains(r.Id) || _done.Contains(r.Id) || !Near(r.ClientPos, 2.4f))
                continue;
            if (!TakePress())
                return;
            if (r.NeedTool != "" && !_tools.Contains(r.NeedTool))
            {
                ShowMessage($"{r.Ask}\n（{r.NeedTool}が ないと できない）", 5.0);
                return;
            }
            switch (r.Kind)
            {
                case Kind.Deliver:
                    if (_carrying == r.Id)
                    {
                        ShowMessage($"荷物は もう 持っている。\n{r.TargetLabel}へ。", 3.0);
                        return;
                    }
                    _carrying = r.Id;
                    ShowMessage($"{r.Ask}\n荷物を うけとった。→ {r.TargetLabel}", 5.0);
                    return;
                case Kind.Fix:
                case Kind.Find:
                    ShowMessage($"{r.Ask}\n→ {r.TargetLabel}", 5.0);
                    return;
                case Kind.Listen:
                    _listenStep++;
                    ShowMessage(r.Ask, 4.0);
                    return;
            }
            return;
        }
    }

    private void CheckTargets()
    {
        foreach (Request r in Requests)
        {
            if (!_accepted.Contains(r.Id) || _done.Contains(r.Id) || !Near(r.Target, 2.4f))
                continue;
            if (r.Kind == Kind.Deliver && _carrying != r.Id)
                continue;   // 荷物を持っていない
            if (r.NeedTool != "" && !_tools.Contains(r.NeedTool))
                continue;
            if (!TakePress())
                return;
            Complete(r);
            return;
        }
    }

    private void Complete(Request r)
    {
        _done.Add(r.Id);
        if (_carrying == r.Id)
            _carrying = -1;
        if (r.Unlocks > 0)
            _available.Add(r.Unlocks);
        string got;
        switch (r.Reward)
        {
            case "工具箱":
            case "台車":
            case "鍵束":
            case "古い写真":
                _tools.Add(r.Reward);
                got = $"{r.Reward}を 手に 入れた。";
                break;
            case "原付":
                _tools.Add(r.Reward);
                _player.HasMoped = true;
                got = "原付（カブ）を 手に 入れた。\nShift で 乗る。隣の 団地まで 行ける。";
                break;
            default:
                got = "";
                break;
        }
        string what = r.Kind switch
        {
            Kind.Deliver => "届けた。",
            Kind.Fix => "直した。",
            Kind.Find => "見つけた。",
            _ => "聞いた。",
        };
        ShowMessage($"{what}\n{r.Done}\n{got}", 6.0);
        if (r.Reward == "古い写真")
            _messageTimer = 7.0;
    }

    // --- 回想（古い写真を池で見る） ---

    private void CheckPhoto()
    {
        if (!_tools.Contains("古い写真") || PlayerXZ().DistanceTo(PondCenter) > 9.5f || !TakePress())
            return;
        _ = StartFlashback();
    }

    private async System.Threading.Tasks.Task StartFlashback()
    {
        _transitioning = true;
        _player.Frozen = true;
        ShowMessage("写真の 池と、目の前の 池が かさなった。\n——2000年の 8月。", 4.0);
        Tween tw = CreateTween();
        tw.TweenProperty(_fade, "color:a", 1.0f, 2.2);
        await ToSignal(tw, Tween.SignalName.Finished);
        BenriyaState.Save(this);
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        Node summer = packed.Instantiate();
        summer.Set("SkipIntro", true);
        summer.Set("StartDay", 24);
        summer.Set("StartHour", 17.5);
        summer.Set("SecondsPerHour", 30.0);
        summer.Set("FlashbackMode", true);
        GetTree().Root.AddChild(summer);
        // 同じ場所で目が覚める。2000 年の「ぼく」も池のふちに立っている
        if (summer.GetNodeOrNull<Node3D>("Player") is Node3D kid)
            kid.Position = _player.Position;
        GetTree().CurrentScene = summer;
        QueueFree();
    }

    // --- 画面 ---

    private void ShowMessage(string text, double seconds)
    {
        _messageLabel.Text = text;
        _messageTimer = seconds;
        if (OS.GetEnvironment("DEBUG_MSG") == "1")
            GD.Print($"[msg] 2026/8/{_day} {(int)_hour:D2}時 | {text.Replace("\n", " / ")}");
    }

    private void UpdateMessages(double delta)
    {
        if (_messageTimer > 0.0)
        {
            _messageTimer -= delta;
            return;
        }
        // 近くの案内
        if (Near(BoardPos, 2.4f)) { _messageLabel.Text = "依頼板　スペースで 見る"; return; }
        foreach (Request r in Requests)
        {
            if (_accepted.Contains(r.Id) && !_done.Contains(r.Id))
            {
                if (Near(r.ClientPos, 2.4f)) { _messageLabel.Text = $"{r.Client}　スペースで 話す"; return; }
                bool can = (r.Kind != Kind.Deliver || _carrying == r.Id) && (r.NeedTool == "" || _tools.Contains(r.NeedTool));
                if (Near(r.Target, 2.4f) && can)
                {
                    _messageLabel.Text = r.Kind switch
                    {
                        Kind.Deliver => $"{r.TargetLabel}　スペースで 届ける",
                        Kind.Fix => $"{r.TargetLabel}　スペースで 直す",
                        Kind.Find => $"{r.TargetLabel}　スペースで さがす",
                        _ => "",
                    };
                    return;
                }
            }
        }
        if (_tools.Contains("古い写真") && PlayerXZ().DistanceTo(PondCenter) <= 9.5f)
        {
            _messageLabel.Text = "池　スペースで 写真を 見る";
            return;
        }
        _messageLabel.Text = "";
    }

    private void UpdateHud()
    {
        int h = (int)_hour;
        int m = (int)((_hour - h) * 60.0);
        _dateLabel.Text = $"2026年8月{_day}日  {h:D2}:{m:D2}  便利屋";
        var sb = new System.Text.StringBuilder();
        sb.Append($"依頼 {_done.Count}/{Requests.Length}");
        if (_tools.Count > 0)
            sb.Append("   持ち物: ").Append(string.Join(" ", _tools));
        if (_carrying > 0)
            sb.Append("   荷物: ").Append(Requests[_carrying - 1].Title);
        _bugLabel.Text = sb.ToString();
    }

    private string ZoneFor(Vector3 p)
    {
        if (p.Z > 11.5f) return "CamStreet";
        if (p.Z < -6f) return p.X < -2f ? "CamParkWest" : "CamPark";
        if (p.X < -10f) return "CamDanchi";
        return p.X > 12f ? "CamLot" : "CamPlaza";
    }

    private void UpdateCamera(bool force = false)
    {
        string zone = ZoneFor(_player.Position);
        if (zone == _zone && !force)
            return;
        _zone = zone;
        _cameras.GetNode<Camera3D>(zone).MakeCurrent();
    }

    /// <summary>時刻の光。夏の 2000 年版より少し白く、夕方は同じく金色へ。</summary>
    private void UpdateSky()
    {
        if (_sun == null || _envNode?.Environment == null)
            return;
        float t = Mathf.Clamp((float)((_hour - 6.0) / 13.0), 0f, 1f);
        float arc = Mathf.Sin(t * Mathf.Pi);
        float warm = Mathf.Pow(Mathf.Clamp((float)((_hour - 14.0) / 5.0), 0f, 1f), 1.3f);
        _sun.RotationDegrees = new Vector3(-10f - 50f * arc, -150f + 120f * t, 0f);
        _sun.LightColor = new Color(1f, 0.98f, 0.94f).Lerp(new Color(1f, 0.6f, 0.3f), warm);
        _sun.LightEnergy = (0.55f + 0.65f * arc) * (1f - 0.38f * warm);
        var hor = new Color(0.72f, 0.82f, 0.92f).Lerp(new Color(0.98f, 0.55f, 0.3f), warm);
        _envNode.Environment.AmbientLightColor = hor.Lerp(Colors.White, 0.45f) * 0.66f * (1f - 0.16f * warm);
        if (_sky != null)
        {
            _sky.SkyTopColor = new Color(0.16f, 0.38f, 0.8f).Lerp(new Color(0.3f, 0.26f, 0.5f), warm);
            _sky.SkyHorizonColor = hor;
            _sky.GroundHorizonColor = hor;
        }
    }
}

/// <summary>回想へ行って戻るあいだ、便利屋の状態を持っておく入れ物。</summary>
public static class BenriyaState
{
    private static bool _has;
    private static readonly HashSet<string> Tools = new();
    private static readonly HashSet<int> Done = new(), Accepted = new(), Available = new();
    private static Vector3 _pos;
    private static double _hour;
    private static int _day;

    public static void Save(BenriyaMain m)
    {
        _has = true;
        Tools.Clear(); Done.Clear(); Accepted.Clear(); Available.Clear();
        foreach (string t in m.ToolsView) Tools.Add(t);
        foreach (int i in m.DoneView) Done.Add(i);
        foreach (int i in m.AcceptedView) Accepted.Add(i);
        foreach (int i in m.AvailableView) Available.Add(i);
        _pos = m.PlayerPos; _hour = m.HourView; _day = m.DayView;
    }

    public static bool Restore(BenriyaMain m)
    {
        if (!_has)
            return false;
        _has = false;
        m.RestoreFrom(Tools, Done, Accepted, Available, _pos, _hour, _day);
        return true;
    }
}

public partial class BenriyaMain
{
    public IEnumerable<string> ToolsView => _tools;
    public IEnumerable<int> DoneView => _done;
    public IEnumerable<int> AcceptedView => _accepted;
    public IEnumerable<int> AvailableView => _available;
    public Vector3 PlayerPos => _player.Position;
    public double HourView => _hour;
    public int DayView => _day;

    public void RestoreFrom(HashSet<string> tools, HashSet<int> done, HashSet<int> accepted, HashSet<int> available,
        Vector3 pos, double hour, int day)
    {
        _tools.Clear(); _done.Clear(); _accepted.Clear(); _available.Clear();
        foreach (string t in tools) _tools.Add(t);
        foreach (int i in done) _done.Add(i);
        foreach (int i in accepted) _accepted.Add(i);
        foreach (int i in available) _available.Add(i);
        _player.Position = pos;
        _hour = hour + 0.4;
        _day = day;
        _player.HasMoped = _tools.Contains("原付");
    }
}

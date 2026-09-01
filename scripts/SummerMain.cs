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

    /// <summary>0 以外ならセミ配置などの乱数を固定する（決定的キャプチャ用）。</summary>
    [Export]
    public int RngSeed { get; set; }

    /// <summary>true なら曇天（柔らかい光・影なし・灰色の空）。</summary>
    [Export]
    public bool Overcast { get; set; }

    private const double DayStartHour = 8.0;
    private const double DayEndHour = 19.0;
    private const int LastDay = 31;
    private const int CicadasPerDay = 4;

    /// <summary>セミの種類。出る時間帯が違うので「夕方にしか採れない」動機が生まれる。</summary>
    private readonly record struct Species(string Name, Color Color, int FromHour, int ToHour, float CatchRate);

    // 時間帯で顔ぶれが変わる。難しい種ほど捕獲率が低い
    private static readonly Species[] AllSpecies =
    {
        new("ニイニイゼミ", new Color(0.35f, 0.32f, 0.26f), 8, 11, 0.75f),
        new("クマゼミ", new Color(0.16f, 0.17f, 0.19f), 8, 13, 0.5f),
        new("アブラゼミ", new Color(0.36f, 0.24f, 0.13f), 10, 17, 0.7f),
        new("ミンミンゼミ", new Color(0.2f, 0.42f, 0.3f), 10, 17, 0.6f),
        new("ツクツクボウシ", new Color(0.28f, 0.3f, 0.22f), 15, 19, 0.55f),
        new("ヒグラシ", new Color(0.45f, 0.28f, 0.22f), 17, 19, 0.4f),
    };
    private const float CatchRange = 2.2f;
    private const float DiscoverRange = 3.2f;

    /// <summary>
    /// 立ち止まると何か思い出す場所。締切も失敗も無く、歩けば増える。
    /// このゲームは「30代が2000年の子供に戻る」話なので、
    /// 子供の目線に、かすかな既視感（大人の記憶）を一枚重ねる。
    /// </summary>
    private readonly record struct Spot(Vector2 Pos, string Text);

    private static readonly Spot[] Spots =
    {
        new(new Vector2(-26f, 14f), "きゅうすいとうは いつも おなじ かたち。\nどうして だろう、と 思った ことも なかった。"),
        new(new Vector2(22f, 0f), "どかんの なかは ひんやり している。\nここに かくれると、だれも 見つけられない。"),
        new(new Vector2(6f, -8f), "いけの みずは にごって いて そこが 見えない。\nなにか いる きが する。ずっと そう 思って いた。"),
        new(new Vector2(14f, 10.5f), "じはんきの したを のぞくと、ときどき 十円が おちて いる。\nきょうは なかった。"),
        new(new Vector2(2f, 8f), "しんごうが ないから、みぎ ひだり みぎ。\nおかあさんに 百回 いわれた。"),
        new(new Vector2(-6f, -11f), "すべりだいは まなつに さわると あつい。\nしっている のに まいかい さわって しまう。"),
        new(new Vector2(-2f, -17f), "ブランコを こぎながら 空を 見ると、\nくもが すごい はやさで うごいて 見える。"),
        new(new Vector2(-4f, 13f), "しょうてんがいの したは いつも すずしい。\nそとの あつさが、きゅうに とおくなる。"),
    };

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
    private ProceduralSkyMaterial _sky;
    private Label _dateLabel;
    private Label _bugLabel;
    private Label _messageLabel;
    private ColorRect _fade;
    private Node3D _cameras;
    private string _zone = "";
    private readonly List<Node3D> _cicadas = new();
    private readonly List<int> _cicadaSpecies = new();   // _cicadas と同じ並び
    private readonly HashSet<int> _collected = new();    // 図鑑（日をまたいで残る）
    private readonly HashSet<int> _found = new();        // 発見（日をまたいで残る）
    private string _todayFound = "";                     // その日 最初に見つけたもの
    private int _spawnedPhase = -1;                      // 何時台の顔ぶれで湧かせたか
    private readonly List<Vector3> _treeSpots = new();
    private readonly AudioStreamPlayer[] _cicadaVoices = new AudioStreamPlayer[3];
    // 遠景（実写パノラマ・入道雲）は Unshaded なのでライトが当たらない。
    // 時刻に合わせて手で色を掛けないと、夕焼けの世界に真昼の空が残ってしまう。
    private readonly List<(StandardMaterial3D Mat, Color Base)> _skyTinted = new();
    private StandardMaterial3D _nightPano;   // 夕方から重なってくる夜の遠景
    private AudioStreamPlayer _sfxSwing, _sfxCatch, _sfxEscape;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _player = GetNode<PlayerController>("Player");
        _sun = GetNode<DirectionalLight3D>("Sun");
        _env = GetNode<WorldEnvironment>("Env").Environment;
        _sky = (ProceduralSkyMaterial)_env.Sky.SkyMaterial;
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
        if (RngSeed != 0)
            _rng.Seed = (ulong)RngSeed;
        else
            _rng.Randomize();
        SetupAudio();
        CollectSkyTinted(GetNodeOrNull("Backdrop"));
        if (GetNodeOrNull("Backdrop/PanoramaNight") is MeshInstance3D np)
            _nightPano = np.MaterialOverride as StandardMaterial3D;
        RespawnCicadas();
        UpdateCamera(force: true);
        ShowMessage("2000年8月1日。ニュータウンの なつやすみが はじまった！", 4.0);
    }

    public override void _Process(double delta)
    {
        if (_vacationOver || _transitioning)
            return;
        _hour += delta / SecondsPerHour;
        UpdateSky();
        UpdateAudio(delta);
        UpdateLabels();
        UpdateCamera();
        UpdateMessages(delta);
        if (PhaseOfHour() != _spawnedPhase)
        {
            RespawnCicadas();
            ShowMessage("セミの こえが かわった……", 2.5);
        }
        CheckCatch();
        CheckDiscovery();
        if (_hour >= DayEndHour)
            _ = EndDay();
    }

    // --- 固定カメラ（場面ごとに1アングル、位置で自動切替） ---

    private string ZoneFor(Vector3 p)
    {
        if (p.Z > 11.5f)
            return "CamStreet"; // 商店街：通りの軸で望遠
        if (p.Z < -6f)
            return "CamPark";   // 公園：池の対岸から広角
        if (p.X < -10f)
            return "CamDanchi"; // 団地の広場：斜め見下ろし
        return "CamPlaza";      // 大通りと空き地：高め俯瞰
    }

    private void UpdateCamera(bool force = false)
    {
        string zone = ZoneFor(_player.Position);
        if (zone == _zone && !force)
            return;
        _zone = zone;
        _cameras.GetNode<Camera3D>(zone).MakeCurrent();
    }

    // --- 遠景の色を時刻に合わせる ---

    /// <summary>Backdrop 配下の Unshaded なマテリアルを集める（実写パノラマと雲）。</summary>
    private void CollectSkyTinted(Node node)
    {
        if (node == null)
            return;
        if (node.Name != "PanoramaNight"
            && node is MeshInstance3D mi && mi.MaterialOverride is StandardMaterial3D m
            && m.ShadingMode == BaseMaterial3D.ShadingModeEnum.Unshaded
            && !_skyTinted.Exists(e => e.Mat == m))
        {
            _skyTinted.Add((m, m.AlbedoColor));
        }
        foreach (Node child in node.GetChildren())
            CollectSkyTinted(child);
    }

    private void ApplySkyTint(Color tint)
    {
        foreach ((StandardMaterial3D mat, Color base_) in _skyTinted)
        {
            mat.AlbedoColor = new Color(base_.R * tint.R, base_.G * tint.G,
                                        base_.B * tint.B, base_.A);
        }
    }

    // --- 音（時間帯でセミの顔ぶれが変わるのを耳でも分かるようにする） ---

    private void SetupAudio()
    {
        string[] files = { "cicada_morning", "cicada_day", "cicada_evening" };
        for (int i = 0; i < files.Length; i++)
        {
            var stream = GD.Load<AudioStreamWav>($"res://assets/audio/{files[i]}.wav");
            if (stream == null)
                continue;
            // 4秒の素材を途切れず回す。インポート設定に頼らずコード側で閉じる
            stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            stream.LoopBegin = 0;
            stream.LoopEnd = stream.Data.Length / 2; // 16bit モノラル
            var player = new AudioStreamPlayer
            {
                Name = $"Cicada{i}",
                Stream = stream,
                Autoplay = false,
                // 開始時点の時間帯だけ最初から鳴らす。全部を無音から上げると
                // ゲーム開始の数秒が無音になってしまう
                VolumeDb = i == PhaseOfHour() ? -8f : -60f,
            };
            AddChild(player);
            player.Play();
            _cicadaVoices[i] = player;
        }

        // 効果音。虫あみを振った瞬間・捕れた瞬間・逃げられた瞬間に手応えを返す
        _sfxSwing = MakeSfx("sfx_swing", -10f);
        _sfxCatch = MakeSfx("sfx_catch", -6f);
        _sfxEscape = MakeSfx("sfx_escape", -8f);
    }

    private static void PlaySfx(AudioStreamPlayer p)
    {
        if (p == null)
            return;
        p.Stop();   // 連打しても頭から鳴り直す
        p.Play();
    }

    private AudioStreamPlayer MakeSfx(string file, float db)
    {
        var stream = GD.Load<AudioStreamWav>($"res://assets/audio/{file}.wav");
        if (stream == null)
            return null;
        var player = new AudioStreamPlayer { Name = file, Stream = stream, VolumeDb = db };
        AddChild(player);
        return player;
    }

    /// <summary>いまの時間帯の声だけを鳴らし、他は絞る（ぶつ切りにせず混ぜる）。</summary>
    private void UpdateAudio(double delta)
    {
        int phase = PhaseOfHour();
        for (int i = 0; i < _cicadaVoices.Length; i++)
        {
            AudioStreamPlayer v = _cicadaVoices[i];
            if (v == null)
                continue;
            float target = i == phase ? -8f : -60f;
            // 秒あたり約12dB で寄せる。時間帯の変わり目がゆっくり入れ替わる
            v.VolumeDb = Mathf.MoveToward(v.VolumeDb, target, (float)delta * 12f);
        }
    }

    // --- 時刻と空 ---

    private void UpdateSky()
    {
        float t = Mathf.Clamp((float)((_hour - 6.0) / 13.0), 0f, 1f);
        if (Overcast)
        {
            var overTop = new Color(0.6f, 0.66f, 0.71f);
            var overHor = new Color(0.85f, 0.87f, 0.89f);
            _sky.SkyTopColor = overTop;
            _sky.SkyHorizonColor = overHor;
            _sky.GroundHorizonColor = overHor;
            _env.FogLightColor = overHor;
            _env.FogDensity = 0.0035f;
            _env.AmbientLightColor = new Color(0.74f, 0.76f, 0.78f);
            _sun.ShadowEnabled = false;
            _sun.LightEnergy = 0.55f;
            _sun.LightColor = new Color(0.95f, 0.96f, 0.98f);
            _sun.RotationDegrees = new Vector3(-60f, -40f, 0f);
            ApplySkyTint(new Color(0.84f, 0.86f, 0.88f)); // 曇りは彩度を落として少し暗く
            return;
        }
        _sun.ShadowEnabled = true;
        _env.FogDensity = 0.006f;
        // 天頂と地平線の2色をそれぞれ時刻で補間してグラデーション空を作る
        var topMorning = new Color(0.4f, 0.55f, 0.85f);
        var topNoon = new Color(0.2f, 0.45f, 0.85f);
        var topSunset = new Color(0.3f, 0.26f, 0.5f);
        var horMorning = new Color(0.85f, 0.87f, 0.9f);
        var horNoon = new Color(0.65f, 0.82f, 0.95f);
        var horGold = new Color(0.95f, 0.78f, 0.5f);
        var horSunset = new Color(0.98f, 0.5f, 0.28f);
        Color top;
        Color hor;
        if (_hour < 9.0)
        {
            float u = Mathf.Clamp((float)((_hour - 6.0) / 3.0), 0f, 1f);
            top = topMorning.Lerp(topNoon, u);
            hor = horMorning.Lerp(horNoon, u);
        }
        else if (_hour < 16.0)
        {
            top = topNoon;
            hor = horNoon;
        }
        else
        {
            // 青→橙を直接補間すると灰色に濁るので、地平線は金色を経由して夕焼けにする
            float u = Mathf.Clamp((float)((_hour - 16.0) / 3.0), 0f, 1f);
            top = topNoon.Lerp(topSunset, u);
            hor = u < 0.5f ? horNoon.Lerp(horGold, u * 2f) : horGold.Lerp(horSunset, (u - 0.5f) * 2f);
        }
        _sky.SkyTopColor = top;
        _sky.SkyHorizonColor = hor;
        _sky.GroundHorizonColor = hor;
        _env.FogLightColor = hor;
        _env.AmbientLightColor = hor.Lerp(Colors.White, 0.45f) * 0.8f;
        float arc = Mathf.Sin(t * Mathf.Pi);
        _sun.RotationDegrees = new Vector3(-10f - 60f * arc, -150f + 120f * t, 0f);
        _sun.LightEnergy = 0.55f + 0.65f * arc;
        _sun.LightColor = new Color(1f, 0.72f + 0.26f * arc, 0.55f + 0.4f * arc);

        // 遠景も同じ光の下に置く。真昼は素の色、朝夕は太陽の色に寄せて暗くする
        var sunTint = new Color(1f, 0.72f + 0.26f * arc, 0.55f + 0.4f * arc);
        float bright = 0.45f + 0.55f * arc;
        ApplySkyTint(Colors.White.Lerp(sunTint, 0.65f) * bright);

        // 夜の遠景（窓に灯りが点いた同じ町）を17時から重ねていく。
        // 昼版を暗くするだけでは窓の灯りは作れないので、別撮りを混ぜる
        if (_nightPano != null)
        {
            float nightAlpha = Mathf.Clamp((float)((_hour - 17.0) / 2.0), 0f, 1f);
            _nightPano.AlbedoColor = new Color(1f, 1f, 1f, nightAlpha);
        }
    }

    private string Weekday()
    {
        // 2000年8月1日は火曜日
        string[] youbi = { "火", "水", "木", "金", "土", "日", "月" };
        return youbi[(_day - 1) % 7];
    }

    private void UpdateLabels()
    {
        int h = (int)_hour;
        int m = (int)((_hour - h) * 60.0);
        _dateLabel.Text = $"8月{_day}日({Weekday()})  {h:D2}:{m:D2}";
        _bugLabel.Text = $"セミ ×{_totalCaught}   ずかん {_collected.Count}/{AllSpecies.Length}   " +
                         $"はっけん {_found.Count}/{Spots.Length}";
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
        int near = NearestCicada();
        if (near < 0)
        {
            _messageLabel.Text = "";
            return;
        }
        Species sp = AllSpecies[_cicadaSpecies[near]];
        string mark = _collected.Contains(_cicadaSpecies[near]) ? "" : "  ★みつけていない！";
        _messageLabel.Text = $"{sp.Name}だ！ スペースで 虫あみを ふる{mark}";
    }

    // --- セミとり ---

    private void RespawnCicadas()
    {
        foreach (Node3D c in _cicadas)
            c.QueueFree();
        _cicadas.Clear();
        _cicadaSpecies.Clear();
        var indices = new List<int>();
        for (int i = 0; i < _treeSpots.Count; i++)
            indices.Add(i);
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = (int)(_rng.Randi() % (uint)(i + 1));
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        // いまの時刻に出ている種類だけを候補にする
        var pool = new List<int>();
        for (int i = 0; i < AllSpecies.Length; i++)
        {
            if (_hour >= AllSpecies[i].FromHour && _hour < AllSpecies[i].ToHour)
                pool.Add(i);
        }
        if (pool.Count == 0)
            pool.Add(2); // 保険（アブラゼミ）

        int count = Mathf.Min(CicadasPerDay, indices.Count);
        for (int k = 0; k < count; k++)
        {
            int sp = pool[(int)(_rng.Randi() % (uint)pool.Count)];
            var spot = new Node3D { Position = _treeSpots[indices[k]] };
            var body = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.26f, 0.12f) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = AllSpecies[sp].Color, Roughness = 1f },
                Position = new Vector3(0.34f, 1.7f, 0f),
            };
            spot.AddChild(body);
            AddChild(spot);
            _cicadas.Add(spot);
            _cicadaSpecies.Add(sp);
        }
        _spawnedPhase = PhaseOfHour();
    }

    /// <summary>朝=0 / 昼=1 / 夕=2。変わったら顔ぶれを入れ替える。</summary>
    private int PhaseOfHour() => _hour < 11.0 ? 0 : _hour < 16.0 ? 1 : 2;

    /// <summary>捕獲圏内で一番近いセミの添字。無ければ -1。</summary>
    private int NearestCicada()
    {
        int best = -1;
        float bestDist = CatchRange;
        for (int i = 0; i < _cicadas.Count; i++)
        {
            float d = _player.Position.DistanceTo(_cicadas[i].Position);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    private void CheckCatch()
    {
        if (!Input.IsActionJustPressed("ui_accept"))
            return;
        // セミが居なくても振った音は返す。無反応が一番よくない
        PlaySfx(_sfxSwing);
        int idx = NearestCicada();
        if (idx < 0)
            return;
        int sp = _cicadaSpecies[idx];
        Species species = AllSpecies[sp];
        _cicadas[idx].QueueFree();
        _cicadas.RemoveAt(idx);
        _cicadaSpecies.RemoveAt(idx);

        if (_rng.Randf() < species.CatchRate)
        {
            _totalCaught++;
            _todayCaught++;
            PlaySfx(_sfxCatch);
            if (_collected.Add(sp))
                ShowMessage($"{species.Name}を つかまえた！\nずかんに はじめて のった！", 3.5);
            else
                ShowMessage($"{species.Name}を つかまえた！");
        }
        else
        {
            PlaySfx(_sfxEscape);
            ShowMessage($"あっ、{species.Name}に にげられた……");
        }
    }

    // --- 発見（締切も失敗も無い。歩けば増える） ---

    private void CheckDiscovery()
    {
        var here = new Vector2(_player.Position.X, _player.Position.Z);
        for (int i = 0; i < Spots.Length; i++)
        {
            if (_found.Contains(i) || here.DistanceTo(Spots[i].Pos) > DiscoverRange)
                continue;
            _found.Add(i);
            if (_todayFound == "")
                _todayFound = Spots[i].Text.Replace("\n", "");
            ShowMessage(Spots[i].Text, 5.0);
            return;
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
        // 「あと◯しゅるい」のような残数は書かない。
        // 締切に見えると、この手のゲームでは焦りになって台無しになる。
        // 日記は達成表ではなく、その日の思い出として書く。
        string extra = _todayFound != ""
            ? _todayFound
            : "とくべつな ことは なかった。それも わるくない。";
        return $"【日記】8月{_day}日({Weekday()})\n{line}\n{extra}\nあしたは なにを しようかな。";
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
                $"8月31日。なつやすみが おわった。\nつかまえたセミ、ぜんぶで {_totalCaught}ひき。\n" +
                $"ずかんは {_collected.Count}/{AllSpecies.Length}しゅるい。\n" +
                $"おぼえて いる ばしょは {_found.Count}こ。\nまた らいねん！";
            return;
        }

        _messageLabel.Text = DiaryText();
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

        _day++;
        _hour = DayStartHour;
        _todayCaught = 0;
        _todayFound = "";
        _player.Position = new Vector3(-14f, 0.1f, 0f); // 団地の広場から一日開始
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

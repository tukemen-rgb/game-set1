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

    /// <summary>true なら導入を飛ばす（キャプチャ用。人が遊ぶときは常に false）。</summary>
    [Export]
    public bool SkipIntro { get; set; }

    /// <summary>開始日。終わりの検査で31日目から始めるために使う。既定は1。</summary>
    [Export]
    public int StartDay { get; set; } = 1;

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

    /// <summary>
    /// その日の天気。31日が全部同じだと3日で飽きるので、日ごとに変える。
    /// 癒し系の長編で反復を防ぐ定石は季節・天候・行事だが、8月の1か月では
    /// 季節が動かないため天候で差を作る。
    /// </summary>
    private enum Weather { Sunny, Cloudy, Rainy }

    /// <summary>
    /// 日付が決まっている行事。天候だけでは「今日と明日の違い」しか作れず、
    /// 「あと3日で祭り」という**待ち遠しさ**が生まれない。
    /// 8月という月に山をつけるための仕掛け。
    /// </summary>
    private readonly record struct Event(string Morning, string Diary);

    private static readonly System.Collections.Generic.Dictionary<int, Event> Events = new()
    {
        [7] = new("きょうで ラジオたいそうは おしまい。\nはんこが ぜんぶ そろった。",
                  "ラジオたいそうの さいごの日。はんこが そろった。"),
        [13] = new("おぼんが はじまった。\nどこの まども、いつもより あかるい。",
                   "おぼん。だんちじゅうの まどが あかるかった。"),
        [16] = new("おくりびの ひ。\nかえって いく ひとたちが いるらしい。",
                   "おくりび。なつが すこし とおのいた 気がした。"),
        [24] = new("きょうは なつまつり。\n夕方に はなびが 上がるらしい。",
                   "なつまつりの はなび。ずっと 見あげて いた。"),
    };

    private const int FestivalDay = 24;
    private const double FireworkFromHour = 17.5;
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
    private Weather _weather = Weather.Sunny;
    private AudioStreamPlayer _rainVoice;
    private CpuParticles3D _rainFx;
    private CpuParticles3D _fireworkFx;
    private AudioStreamPlayer _sfxFirework;
    private double _nextFirework;
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
        _day = Mathf.Max(1, StartDay);
        _weather = WeatherOfDay(_day);
        Overcast = _weather != Weather.Sunny;
        if (RngSeed != 0)
            _rng.Seed = (ulong)RngSeed;
        else
            _rng.Randomize();
        SetupAudio();
        SetupRainFx();
        SetupFireworkFx();
        if (_rainFx != null)
            _rainFx.Emitting = _weather == Weather.Rainy;
        CollectSkyTinted(GetNodeOrNull("Backdrop"));
        if (GetNodeOrNull("Backdrop/PanoramaNight") is MeshInstance3D np)
            _nightPano = np.MaterialOverride as StandardMaterial3D;
        RespawnCicadas();
        UpdateCamera(force: true);
        if (SkipIntro)
            ShowMessage($"2000年8月{_day}日。ニュータウンの なつやすみ。", 4.0);
        else
            _ = PlayIntro();
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
        AnimateCicadas();
        UpdateFireworks(delta);
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

    // --- 導入（このゲームが何の話なのかを最初に渡す） ---

    /// <summary>
    /// 「30代のサラリーマンが2000年の夏に戻る」という企画の核は、
    /// 遊びの中だけでは絶対に伝わらない。最初に言葉で渡しておく。
    /// 現代の3行を淡々と、そこから音だけが先に夏になる構成にした。
    /// </summary>
    private async Task PlayIntro()
    {
        _transitioning = true;
        _player.Frozen = true;
        _fade.Color = new Color(0f, 0f, 0f, 1f);
        var ui = GetNode<CanvasLayer>("UI");
        _dateLabel.Visible = false;
        _bugLabel.Visible = false;

        (string Text, double Hold)[] lines =
        {
            ("二〇二六年。会議、見積、終電。", 3.2),
            ("気づけば、夏を まるごと 忘れていた。", 3.2),
            ("その夜、ひどく つかれて 眠った。", 3.0),
            ("——目が さめると、", 2.4),
            ("セミの こえが していた。", 3.0),
        };
        foreach ((string text, double hold) in lines)
        {
            _messageLabel.Text = text;
            await ToSignal(GetTree().CreateTimer(hold), SceneTreeTimer.SignalName.Timeout);
        }

        _messageLabel.Text = "";
        Tween fadeIn = CreateTween();
        fadeIn.TweenProperty(_fade, "color:a", 0.0f, 2.4);
        await ToSignal(fadeIn, Tween.SignalName.Finished);

        _dateLabel.Visible = true;
        _bugLabel.Visible = true;
        _player.Frozen = false;
        _transitioning = false;
        ShowMessage("2000年8月1日（火）。\n10さいの ぼくの、なつやすみが はじまった。", 5.0);
    }

    /// <summary>
    /// 終わり。導入で張った「2026年の疲れた大人」という糸を回収する。
    /// 張った糸を回収しない物語は、張らなかったより悪い。
    /// 夏の総括 → 目が覚める → 現代 → それでも何かが残っている、の順。
    /// </summary>
    private async Task PlayEnding()
    {
        _messageLabel.Text =
            $"8月31日。なつやすみが おわった。\n" +
            $"つかまえたセミ、ぜんぶで {_totalCaught}ひき。\n" +
            $"ずかんは {_collected.Count}/{AllSpecies.Length}しゅるい。\n" +
            $"おぼえて いる ばしょは {_found.Count}こ。";
        await ToSignal(GetTree().CreateTimer(5.0), SceneTreeTimer.SignalName.Timeout);

        _messageLabel.Text = "";
        _dateLabel.Visible = false;
        _bugLabel.Visible = false;

        // セミの声を静かに引いていく。夏が遠ざかる音として使う
        foreach (AudioStreamPlayer v in _cicadaVoices)
        {
            if (v == null)
                continue;
            Tween t = CreateTween();
            t.TweenProperty(v, "volume_db", -60f, 6.0);
        }

        string[] lines =
        {
            "——目が さめる。",
            "二〇二六年。いつもの 朝。",
            "会議も 見積も、なにも 変わっていない。",
            _found.Count > 0
                ? "ただ、あの がっこうの 帰り道の においを、\nまだ おぼえて いる 気がした。"
                : "ただ、どこかで セミが 鳴いている 気がした。",
            "",
            "（おわり）",
        };
        foreach (string line in lines)
        {
            _messageLabel.Text = line;
            await ToSignal(GetTree().CreateTimer(line == "" ? 1.2 : 4.0),
                           SceneTreeTimer.SignalName.Timeout);
        }
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
        var rainStream = GD.Load<AudioStreamWav>("res://assets/audio/rain.wav");
        if (rainStream != null)
        {
            rainStream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            rainStream.LoopBegin = 0;
            rainStream.LoopEnd = rainStream.Data.Length / 2;
            _rainVoice = new AudioStreamPlayer { Name = "Rain", Stream = rainStream, VolumeDb = -60f };
            AddChild(_rainVoice);
            _rainVoice.Play();
        }

        _sfxFirework = MakeSfx("sfx_firework", -4f);
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

    /// <summary>
    /// 雨粒。プレイ範囲の上空から落とす。ソフトウェア描画でも重くならないよう
    /// 粒は少なめにして、代わりに縦に伸ばして線に見せる。
    /// </summary>
    private void SetupRainFx()
    {
        _rainFx = new CpuParticles3D
        {
            Name = "RainFx",
            Amount = 900,
            Lifetime = 1.4f,
            Emitting = false,
            Position = new Vector3(0f, 14f, 0f),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(34f, 1f, 34f),
            Direction = Vector3.Down,
            Spread = 2f,
            InitialVelocityMin = 16f,
            InitialVelocityMax = 20f,
            ScaleAmountMin = 1f,
            ScaleAmountMax = 1f,
            Mesh = new BoxMesh { Size = new Vector3(0.035f, 0.9f, 0.035f) },
            Gravity = new Vector3(0f, -3f, 0f),
        };
        _rainFx.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.82f, 0.88f, 0.95f, 0.75f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        AddChild(_rainFx);
    }

    /// <summary>花火。空の高いところで一発ずつ開かせる。</summary>
    private void SetupFireworkFx()
    {
        _fireworkFx = new CpuParticles3D
        {
            Name = "FireworkFx",
            Amount = 260,
            Lifetime = 2.2f,
            OneShot = true,
            Explosiveness = 1f,
            Emitting = false,
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = 6f,
            InitialVelocityMax = 11f,
            Gravity = new Vector3(0f, -4.5f, 0f),
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.1f,
            Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 5, Rings = 3 },
        };
        _fireworkFx.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.85f, 0.5f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        AddChild(_fireworkFx);
    }

    /// <summary>
    /// 祭りの日の夕方だけ、数秒おきに一発ずつ打ち上げる。
    /// 色と位置を毎回変えて、同じ絵が続かないようにする。
    /// </summary>
    private void UpdateFireworks(double delta)
    {
        if (_fireworkFx == null || _day != FestivalDay || _hour < FireworkFromHour)
            return;
        _nextFirework -= delta;
        if (_nextFirework > 0.0)
            return;
        _nextFirework = _rng.RandfRange(2.2f, 4.0f);
        // ワールド座標で決め打ちにすると、見下ろしの固定カメラでは空が映らず
        // 一発も見えない。いま使っているカメラの前方・上方に置いて必ず見せる。
        // 花火は遠くの空の出来事なので、この置き方で不自然にはならない。
        Camera3D cam = _cameras.GetNode<Camera3D>(_zone);
        var local = new Vector3(_rng.RandfRange(-14f, 14f), _rng.RandfRange(7f, 15f),
                                -_rng.RandfRange(28f, 44f));
        _fireworkFx.Position = cam.GlobalTransform * local;
        Color[] hues =
        {
            new(1f, 0.85f, 0.5f), new(1f, 0.5f, 0.55f), new(0.6f, 0.85f, 1f),
            new(0.7f, 1f, 0.7f), new(1f, 0.7f, 0.95f),
        };
        ((StandardMaterial3D)_fireworkFx.MaterialOverride).AlbedoColor =
            hues[(int)(_rng.Randi() % (uint)hues.Length)];
        _fireworkFx.Restart();
        PlaySfx(_sfxFirework);
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
            // 雨の日はセミが鳴かない。ここを変えるだけで一日の印象が別物になる
            float target = (i == phase && _weather != Weather.Rainy) ? -8f : -60f;
            // 秒あたり約12dB で寄せる。時間帯の変わり目がゆっくり入れ替わる
            v.VolumeDb = Mathf.MoveToward(v.VolumeDb, target, (float)delta * 12f);
        }
        if (_rainVoice != null)
        {
            float rainTarget = _weather == Weather.Rainy ? -12f : -60f;
            _rainVoice.VolumeDb = Mathf.MoveToward(_rainVoice.VolumeDb, rainTarget, (float)delta * 12f);
        }
    }

    // --- 時刻と空 ---

    private void UpdateSky()
    {
        float t = Mathf.Clamp((float)((_hour - 6.0) / 13.0), 0f, 1f);
        if (Overcast)
        {
            bool rainy = _weather == Weather.Rainy;
            var overTop = rainy ? new Color(0.34f, 0.37f, 0.41f) : new Color(0.6f, 0.66f, 0.71f);
            var overHor = rainy ? new Color(0.55f, 0.58f, 0.61f) : new Color(0.85f, 0.87f, 0.89f);
            _sky.SkyTopColor = overTop;
            _sky.SkyHorizonColor = overHor;
            _sky.GroundHorizonColor = overHor;
            _env.FogLightColor = overHor;
            _env.FogDensity = 0.0035f;
            _env.AmbientLightColor = rainy
                ? new Color(0.46f, 0.49f, 0.53f)
                : new Color(0.74f, 0.76f, 0.78f);
            _sun.ShadowEnabled = false;
            _sun.LightEnergy = rainy ? 0.3f : 0.55f;
            _sun.LightColor = new Color(0.95f, 0.96f, 0.98f);
            _sun.RotationDegrees = new Vector3(-60f, -40f, 0f);
            // 遠景は晴れの日に撮った実写なので、曇り・雨の日は強めに沈めないと
            // 「空だけ晴れている」ちぐはぐな絵になる
            ApplySkyTint(_weather == Weather.Rainy
                ? new Color(0.42f, 0.46f, 0.52f)
                : new Color(0.68f, 0.71f, 0.75f));
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
        string wx = _weather switch
        {
            Weather.Rainy => "雨",
            Weather.Cloudy => "くもり",
            _ => "はれ",
        };
        _dateLabel.Text = $"8月{_day}日({Weekday()})  {h:D2}:{m:D2}  {wx}";
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

        // 雨の日はセミが鳴かない＝とれない。「今日は無理だ」も一日の表情
        int count = _weather == Weather.Rainy ? 0 : Mathf.Min(CicadasPerDay, indices.Count);
        count = Mathf.Min(count, indices.Count);
        for (int k = 0; k < count; k++)
        {
            int sp = pool[(int)(_rng.Randi() % (uint)pool.Count)];
            var spot = new Node3D { Position = _treeSpots[indices[k]] };
            spot.AddChild(MakeCicadaBody(AllSpecies[sp], indices[k] + k));
            AddChild(spot);
            _cicadas.Add(spot);
            _cicadaSpecies.Add(sp);
        }
        _spawnedPhase = PhaseOfHour();
    }

    /// <summary>
    /// その日の天気を日付から決める。乱数を回さないので、
    /// 同じ日は何度始めても同じ天気になる（日記の記述とも矛盾しない）。
    /// 8月なので晴れが主、たまに曇り、時々 雨。
    /// </summary>
    private static Weather WeatherOfDay(int day)
    {
        int h = (day * 37 + 11) % 100;
        if (h < 18)
            return Weather.Rainy;
        if (h < 40)
            return Weather.Cloudy;
        return Weather.Sunny;
    }

    private void ApplyWeather()
    {
        _weather = WeatherOfDay(_day);
        Overcast = _weather != Weather.Sunny;
        if (_rainFx != null)
            _rainFx.Emitting = _weather == Weather.Rainy;
    }

    /// <summary>朝=0 / 昼=1 / 夕=2。変わったら顔ぶれを入れ替える。</summary>
    private int PhaseOfHour() => _hour < 11.0 ? 0 : _hour < 16.0 ? 1 : 2;

    /// <summary>木にとまったセミ1匹。頭を上にして幹に貼りつく本物の姿勢で作る。</summary>
    private static Node3D MakeCicadaBody(Species sp, int variant)
    {
        var root = new Node3D
        {
            // 高さ 1.25m。広葉樹の樹冠は 2.1m から、メタセコイアの円錐は
            // 1.7m から始まるので、それより下でないと葉に埋もれて見つけられない
            Position = new Vector3(0.32f, 1.25f, 0f),
            // 幹に沿って少し傾ける。個体ごとに向きを変えて並びの単調さを消す
            RotationDegrees = new Vector3(-14f, (variant * 53) % 360, 0f),
        };
        var shell = new StandardMaterial3D { AlbedoColor = sp.Color, Roughness = 0.65f };
        var dark = new StandardMaterial3D { AlbedoColor = sp.Color * 0.55f, Roughness = 0.6f };

        root.AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.045f, Height = 0.24f },
            MaterialOverride = shell,
        });
        root.AddChild(new MeshInstance3D
        {
            Name = "Head",
            Mesh = new SphereMesh { Radius = 0.055f, Height = 0.09f },
            MaterialOverride = dark,
            Position = new Vector3(0f, 0.11f, 0f),
        });
        foreach (float ex in new[] { -0.042f, 0.042f })
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.019f, Height = 0.034f },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.06f, 0.05f, 0.05f),
                    Roughness = 0.3f,
                },
                Position = new Vector3(ex, 0.13f, 0.028f),
            });
        }

        // 翅。半透明にして重なりが透けるようにする（セミらしさはここで決まる）
        var wingMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.78f, 0.77f, 0.72f, 0.42f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.35f,
        };
        var wings = new Node3D { Name = "Wings" };
        foreach (float side in new[] { -1f, 1f })
        {
            wings.AddChild(new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(0.1f, 0.3f) },
                MaterialOverride = wingMat,
                Position = new Vector3(side * 0.05f, -0.03f, -0.035f),
                RotationDegrees = new Vector3(0f, side * 18f, side * 6f),
            });
        }
        root.AddChild(wings);
        return root;
    }

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

    /// <summary>
    /// 数秒おきに小刻みに震わせる。木にとまった置物ではなく
    /// 「いま鳴いている生き物」に見せるための最小限の動き。
    /// </summary>
    private void AnimateCicadas()
    {
        double now = Time.GetTicksMsec() / 1000.0;
        for (int i = 0; i < _cicadas.Count; i++)
        {
            if (_cicadas[i].GetChildCount() == 0)
                continue;
            var body = _cicadas[i].GetChild<Node3D>(0);
            // 個体ごとに周期をずらし、5秒に1回ほど1秒だけ鳴く
            double cycle = (now + i * 1.7) % 5.0;
            float call = cycle < 1.0 ? Mathf.Sin((float)cycle * Mathf.Pi) : 0f;
            Vector3 rot = body.RotationDegrees;
            rot.Z = Mathf.Sin((float)now * 26f + i) * 4.5f * call;
            body.RotationDegrees = rot;
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
        if (_weather == Weather.Rainy)
            line = "あめ。セミは 一ぴきも 鳴かなかった。";
        if (Events.TryGetValue(_day, out Event todayEvent))
            return $"【日記】8月{_day}日({Weekday()})\n{line}\n{todayEvent.Diary}\nあしたは なにを しようかな。";

        // 祭りが近づくと日記が数え始める。これが「待ち遠しさ」になる
        int toFestival = FestivalDay - _day;
        if (toFestival is > 0 and <= 5)
            return $"【日記】8月{_day}日({Weekday()})\n{line}\n" +
                   $"あと {toFestival}日で なつまつり。\nあしたは なにを しようかな。";

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
            await PlayEnding();
            return;
        }

        _messageLabel.Text = DiaryText();
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);

        _day++;
        _hour = DayStartHour;
        _todayCaught = 0;
        _todayFound = "";
        ApplyWeather();
        _player.Position = new Vector3(-14f, 0.1f, 0f); // 団地の広場から一日開始
        RespawnCicadas();
        _messageLabel.Text = "";
        Tween fadeIn = CreateTween();
        fadeIn.TweenProperty(_fade, "color:a", 0.0f, 1.0);
        await ToSignal(fadeIn, Tween.SignalName.Finished);
        _player.Frozen = false;
        _transitioning = false;
        ShowMessage(Events.TryGetValue(_day, out Event ev)
            ? ev.Morning
            : $"8月{_day}日の あさ。ラジオたいそう おわり！", ev.Morning != null ? 4.5 : 3.0);
    }
}

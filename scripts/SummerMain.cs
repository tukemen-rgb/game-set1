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

    /// <summary>
    /// true なら導入を飛ばす。キャプチャ・検査の実行を意味するので、
    /// セーブの読み書きも行わない（検査が人のセーブを壊さないため）。
    /// 人が遊ぶときは常に false。
    /// </summary>
    [Export]
    public bool SkipIntro { get; set; }

    /// <summary>開始日。終わりの検査で31日目から始めるために使う。既定は1。</summary>
    [Export]
    public int StartDay { get; set; } = 1;

    /// <summary>検査で「昼の絵」を撮るための開始時刻。0 なら既定（8時）。</summary>
    [Export]
    public double StartHour { get; set; }

    private const double DayStartHour = 8.0;
    private const double DayEndHour = 19.0;
    private const int LastDay = 31;
    private const int CicadasPerDay = 4;

    /// <summary>セミの種類。出る時間帯が違うので「夕方にしか採れない」動機が生まれる。</summary>
    private readonly record struct Species(string Name, Color Color, int FromHour, int ToHour,
                                          float CatchRate, bool RainOnly = false,
                                          bool PondOnly = false,
                                          bool SapOnly = false);

    // 時間帯で顔ぶれが変わる。難しい種ほど捕獲率が低い
    private static readonly Species[] AllSpecies =
    {
        new("ニイニイゼミ", new Color(0.35f, 0.32f, 0.26f), 8, 11, 0.75f),
        new("クマゼミ", new Color(0.16f, 0.17f, 0.19f), 8, 13, 0.5f),
        new("アブラゼミ", new Color(0.36f, 0.24f, 0.13f), 10, 17, 0.7f),
        new("ミンミンゼミ", new Color(0.2f, 0.42f, 0.3f), 10, 17, 0.6f),
        new("ツクツクボウシ", new Color(0.28f, 0.3f, 0.22f), 15, 19, 0.55f),
        new("ヒグラシ", new Color(0.45f, 0.28f, 0.22f), 17, 19, 0.4f),
        // 雨の日だけ出る。雨を「できない日」で終わらせず、
        // 雨の日にしか会えないものを置いて、天気を引き算から足し算に変える
        new("カタツムリ", new Color(0.62f, 0.55f, 0.42f), 8, 19, 0.95f, RainOnly: true),
        new("アマガエル", new Color(0.42f, 0.68f, 0.35f), 8, 19, 0.55f, RainOnly: true),
        // 木ではなく池で釣る。虫あみ（近づいて振る）とは手ざわりの違う遊びにする
        new("ザリガニ", new Color(0.7f, 0.25f, 0.2f), 8, 19, 0.8f, PondOnly: true),
        // 樹液の出るくぬぎに、朝と夕方だけ来る。昼間は来ないので
        // 「いつでも行けば居る」にはならない
        new("カブトムシ", new Color(0.24f, 0.16f, 0.11f), 8, 19, 0.6f, SapOnly: true),
        new("クワガタ", new Color(0.13f, 0.11f, 0.1f), 8, 19, 0.45f, SapOnly: true),
    };
    private const float CatchRange = 2.2f;
    // 走って近づくと虫は逃げる。歩きを既定にした（イテレーション24）意味を
    // 遊びの側にも通す。走るのが速いだけなら、歩く理由が絵にしか無い
    private const float StartleRange = 4.0f;
    private bool _toldStartle;      // 「そっと ちかづく」は一度だけ教える

    // 樹液の出るくぬぎ（空き地のそば）。ここだけ甲虫が来る
    private static readonly Vector3 SapTree = new(19f, 0f, 5f);
    private const int SapKabuto = 9;
    private const int SapKuwagata = 10;

    // --- ザリガニつり（公園の池） ---
    // 池のふちに立って糸を垂らし、当たりが来た合図で引く。
    // セミとりは「近づいて振る」だけなので、待ってから反射で応える遊びを
    // 一つ足して、同じボタンでも手ざわりが変わるようにする。
    private static readonly Vector2 PondCenter = new(6f, -15f);
    private const float PondEdgeIn = 6.85f;    // これより内側には入れない（壁）
    private const float PondEdgeOut = 8.8f;    // ふちに立っていると見なす外周
    private const int CrayfishIndex = 8;
    private const double BiteWindow = 1.1;     // 当たりに応えられる時間
    // 時刻は Time.GetTicksMsec（実時間）ではなく delta の積算で持つ。
    // 実時間で持つと、キャプチャ（--fixed-fps の早回し）で当たりが一瞬で来てしまい、
    // 「待つ」という遊びそのものを検査できなくなる。
    private double _fishClock;
    private double _lineOutAt = -1.0;          // 糸を垂らした時刻（負なら垂らしていない）
    private double _biteAt;                    // 当たりが来る時刻
    private double _biteUntil;                 // 当たりの受付が切れる時刻
    private double _fishingPutAwayAt = -1.0;   // 竿を仕舞う時刻（あおりを見せ切ってから）

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

    /// <summary>駄菓子屋の店先。ここに立つと話しかけられる。</summary>
    private static readonly Vector2 ShopPos = new(4f, 14.6f);
    private const float TalkRange = 2.6f;

    /// <summary>
    /// おばあさんの一言。日替わりで変わるので、通うと違う話が聞ける。
    /// 町に人が一人もいないのが最大の嘘だったので、まずこの一人を置く。
    /// 30代の記憶という本作の枠に合わせ、子供には分からない言い方を混ぜてある。
    /// </summary>
    private static readonly string[] GrannyLines =
    {
        "「あついねえ。\nラムネ、ひやして あるよ」",
        "「その あみ、じょうずに つかえるかい。\nむかしの 子は みんな もってた」",
        "「きょうは 雨が くるよ。\n足が いたむ日は だいたい あたる」",
        "「だんちの 子だろう。\nおかあさんに よろしくね」",
        "「そこの 木は、まいとし よく 鳴くんだよ。\n毎年、おなじ 木でね」",
        "「ゆっくり おいき。\n夏は、いそぐと おわるのが 早い」",
        "「なつまつり、いくのかい。\nうちも 屋台を 出すよ」",
        "「おや、また 来たね。\nおぼえて いてくれるのは うれしいもんだ」",
    };

    // --- 町の人 ---
    // ベンチのおじいさんも立ち話の二人も、置いただけで一言も話さなかった。
    // 人が居るのに黙っているのは、居ないより不自然になる。
    private readonly record struct Townsfolk(Vector2 Pos, string Label, string[] Lines);
    private static readonly Townsfolk[] Folks =
    {
        new(new Vector2(-1.5f, 15.2f), "おじいさん", new[]
        {
            "「この あつさは、むかしの 夏には なかったよ」",
            "「この先の 空き地は、ずっと 田んぼだったんだ」",
            "「新聞は 朝に よむと きまってる。\nここが いちばん すずしい」",
            "「学校は どうだい。\n……ああ、なつやすみか」",
            "「あの 電柱、わしより 年下なんだ」",
        }),
        new(new Vector2(-18.6f, -1.1f), "おばさん", new[]
        {
            "「たまごが 安いのよ、きょう。\nいそがないと なくなる」",
            "「おおきく なったわねえ。\nこの前まで だっこして たのに」",
            "「せんたくもの、はやく 入れなさいって\nおかあさんに 言っといて」",
            "「ぼうや、水分は とってる？」",
            "「なつまつりの 提灯、もう ついてたわ」",
        }),
        new(new Vector2(-19.6f, -0.2f), "となりの おばさん", new[]
        {
            "「うちの 子は もう おきないの。\nえらいわねえ、あなたは」",
            "「あそこの 木、また セミが すごいのよ」",
            "「夕立が くるかも しれないわ。\nかさ、もってる？」",
            "「そうそう、それでね——」",
            "「ラジオたいそう、行った？\nはんこ もらえるうちに 行きなさい」",
        }),
    };
    private const float FolkRange = 2.4f;
    private int _folkTalkedDay = -1;
    private int _folkTalkedIndex = -1;
    private int _folkTalks;              // 夏のあいだに交わした立ち話の数

    private const int FestivalDay = 24;
    private const int LaterDay = 16;      // これ以降、発見した場所に「その後」が出る
    private readonly HashSet<int> _foundLater = new();
    // その日に初めて見つけた場所。同じ日に「その後」まで出ると、
    // 最初の独白を読む前に上書きされる（検査で実際にそうなった）
    private readonly HashSet<int> _foundToday = new();
    private const double FireworkFromHour = 17.5;
    private const float DiscoverRange = 3.2f;

    /// <summary>
    /// 立ち止まると何か思い出す場所。締切も失敗も無く、歩けば増える。
    /// このゲームは「30代が2000年の子供に戻る」話なので、
    /// 子供の目線に、かすかな既視感（大人の記憶）を一枚重ねる。
    /// </summary>
    // Later は夏の後半（8/16 以降）にもう一度そこへ来たときの独白。
    // 発見が「一度きりの読み物」で終わっていたので、同じ場所に
    // 戻る理由を作る。同じ景色に、ひと月ぶんの時間が乗る
    private readonly record struct Spot(Vector2 Pos, string Text, string Later);

    private static readonly Spot[] Spots =
    {
        new(new Vector2(-26f, 14f), "きゅうすいとうは いつも おなじ かたち。\nどうして だろう、と 思った ことも なかった。",
            "きゅうすいとうは きょうも おなじ かたち。\nかわらない ものが ある、と いうことに 気づいた。"),
        new(new Vector2(22f, 0f), "どかんの なかは ひんやり している。\nここに かくれると、だれも 見つけられない。",
            "どかんの なかで、雨やどりを した 日を 思い出す。\nまだ 夏なのに、思い出に なって いる。"),
        new(new Vector2(6f, -8f), "いけの みずは にごって いて そこが 見えない。\nなにか いる きが する。ずっと そう 思って いた。",
            "そこは やっぱり 見えない。\nでも 何が いるかは、もう 知って いる。"),
        new(new Vector2(14f, 10.5f), "じはんきの したを のぞくと、ときどき 十円が おちて いる。\nきょうは なかった。",
            "きょうも なかった。\nあると 思って のぞくのが、たぶん たのしい。"),
        new(new Vector2(2f, 8f), "しんごうが ないから、みぎ ひだり みぎ。\nおかあさんに 百回 いわれた。",
            "みぎ ひだり みぎ。\nもう 言われなくても やって いる。"),
        new(new Vector2(-6f, -11f), "すべりだいは まなつに さわると あつい。\nしっている のに まいかい さわって しまう。",
            "きょうは そんなに あつくない。\nすべりだいの ほうが さきに 秋に なる。"),
        new(new Vector2(-2f, -17f), "ブランコを こぎながら 空を 見ると、\nくもが すごい はやさで うごいて 見える。",
            "くもの かたちが かわった。\n入道雲じゃ ない くもが、たかい ところに ある。"),
        new(new Vector2(-4f, 13f), "しょうてんがいの したは いつも すずしい。\nそとの あつさが、きゅうに とおくなる。",
            "アーケードの 音が、来た ころより すこし ちいさい。\nみんな 夏に なれたのかも しれない。"),
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
    private int _talkedDay = -1;      // 同じ日に何度も同じ話をしないため
    private int _talkCount;           // 通った回数

    // 1日 3.7分 × 31日 ≒ 1時間53分。ぶっ通しでしか遊べないと
    // 結末まで辿り着く人がほとんどいなくなるので、毎朝 自動で保存する。
    private const string SavePath = "user://summer_save.json";
    private readonly List<Vector3> _treeSpots = new();
    private readonly AudioStreamPlayer[] _cicadaVoices = new AudioStreamPlayer[3];
    // 遠景（実写パノラマ・入道雲）は Unshaded なのでライトが当たらない。
    // 時刻に合わせて手で色を掛けないと、夕焼けの世界に真昼の空が残ってしまう。
    private readonly List<(StandardMaterial3D Mat, Color Base)> _skyTinted = new();
    private StandardMaterial3D _nightPano;   // 夕方から重なってくる夜の遠景
    private StandardMaterial3D _dayPano;     // 昼の遠景
    private StandardMaterial3D _duskSky;     // 夕方だけ上空に重なる夕焼け
    private StandardMaterial3D _rainSky;     // 雨・くもりの日に上空を覆う灰色
    // 雨の日に濡らす地面の材質（元の色と粗さを覚えておいて戻す）
    private readonly List<(StandardMaterial3D Mat, Color Albedo, float Rough, float Metal)> _groundMats = new();
    private Node3D _puddles;
    private string _panoZone = "";           // いま貼っている場所

    // 場面ごとに実写が1枚ずつある。遠景もその場所のものに差し替える。
    // 固定カメラはもともと場面が切り替わるので、同時に遠景が変わっても不自然にならない。
    private static readonly System.Collections.Generic.Dictionary<string, string> ZonePlate = new()
    {
        ["CamDanchi"] = "danchi",
        ["CamStreet"] = "street",
        ["CamPark"] = "park",
        ["CamPlaza"] = "plaza",
        // 分割して増えた場面は、隣の場面と同じ遠景を使う（新しい写真は要らない）
        ["CamParkWest"] = "park",
        ["CamLot"] = "plaza",
    };
    // 木漏れ日。曇りと雨では薄れ、朝夕は寝るので、濃さを時刻と天気で動かす
    private readonly List<StandardMaterial3D> _komorebi = new();
    // --- あさがおの観察 ---
    // ラジオ体操が 8/7 で終わると、朝にすることが無くなる。
    // 31日かけて育つものを1つ置いて、「毎朝ちょっと見に行く」を残す。
    private static readonly Vector2 AsagaoPos = new(-13.6f, 1.35f);
    private Node3D _asagaoVine, _asagaoBuds, _asagaoFlowers, _asagaoPoles, _asagaoFutaba;
    private int _watchedDay = -1;      // その日 観察したか
    private int _bloomSeen;            // 花が咲いた日を見た回数

    // --- ラジオ体操 ---
    // 8/7 の朝に「はんこが ぜんぶ そろった」と言っておきながら、
    // 押す場所がどこにも無かった。game が自分で吐いた嘘を本当にする。
    private static readonly Vector2 RadioPos = new(-16f, 1.5f);
    private const int RadioLastDay = 7;      // 8/1〜8/7 の朝だけ
    private const double RadioFromHour = 8.0, RadioToHour = 9.0;
    private int _stamps;                     // 押したはんこの数（夏のあいだ残る）
    private int _stampedDay = -1;

    // --- 駄菓子屋の買い物 ---
    // 「ラムネ、ひやして あるよ」と言われて何も買えないのは、
    // 池と同じ「期待させて返さない」形。おこづかいは毎朝100円入る。
    private readonly record struct Goods(string Name, int Price, string Bought);
    private static readonly Goods[] Shelf =
    {
        new("ラムネ", 100, "せんが ぬけて、たまが おちた。\nつめたいのが のどを とおって いく。"),
        new("あたりくじ", 20, ""),   // 当たり外れは引いてから決める
        new("アイス", 50, "かじると まんなかに あずきが いた。\nあたりだ、と おもった。"),
    };
    // 夏まつりの日だけ品書きが変わる。おこづかいの行き先が3品しか無く、
    // 上限900円まで貯まったあと使い道が消えていた
    private static readonly Goods[] FestivalShelf =
    {
        new("かき氷", 100, "けずった 氷に いちごの シロップ。\nこめかみが きゅっと なった。"),
        new("りんご飴", 150, "外は かたくて、中は すっぱい。\n手が べたべたに なった。"),
        new("金魚すくい", 200, ""),   // すくえた数は やってから決める
    };
    private int _goldfish;               // すくった金魚。夏のあいだ残る
    private Node3D _festivalNode;
    private Node3D _okuribi;          // 8/16 の夕方だけ焚く送り火
    private double _fireFlicker;
    private const int OkuribiDay = 16;

    private const int DailyAllowance = 100;
    private int _money = DailyAllowance;
    private int _marbles;            // ラムネのビー玉。夏のあいだ残る
    private bool _shopOpen;          // 品書きを開いている
    private int _shopPick;
    private string _todayBought = "";

    private CanvasLayer _diary;
    private TextureRect _diaryShot;
    private Label _diaryText;
    private CanvasLayer _dex;
    private Label _dexTitle, _dexList, _dexFoot;
    private bool _dexOpen;
    private CanvasLayer _title;
    private Label _titlePrompt;
    private bool _atTitle;          // タイトルで入力待ち
    private bool _titleContinued;   // セーブがあるか（プロンプトの文言を変える）
    private double _titleBlink;
    // セミの声の混ぜ具合（0〜1）。dB ではなくここを動かして等パワーで混ぜる
    private readonly float[] _voiceMix = new float[3];
    private const float CicadaDb = -8f;
    private const float RainDb = -7f;
    private static readonly float CicadaAmp = (float)Mathf.DbToLinear(CicadaDb);
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
        _title = GetNodeOrNull<CanvasLayer>("Title");
        _dex = GetNodeOrNull<CanvasLayer>("Dex");
        _diary = GetNodeOrNull<CanvasLayer>("Diary");
        _diaryShot = GetNodeOrNull<TextureRect>("Diary/Paper/Frame/Shot");
        _diaryText = GetNodeOrNull<Label>("Diary/Paper/Text");
        _dexTitle = GetNodeOrNull<Label>("Dex/Title");
        _dexList = GetNodeOrNull<Label>("Dex/List");
        _dexFoot = GetNodeOrNull<Label>("Dex/Foot");
        Node komorebi = GetNodeOrNull("Komorebi");
        if (komorebi != null)
        {
            foreach (Node child in komorebi.GetChildren())
            {
                if (child is MeshInstance3D mi && mi.MaterialOverride is StandardMaterial3D km)
                    _komorebi.Add(km);
            }
        }
        _titlePrompt = GetNodeOrNull<Label>("Title/Prompt");
        foreach (Node child in GetNode("Trees").GetChildren())
        {
            if (child is Node3D tree)
                _treeSpots.Add(tree.Position);
        }
        _day = Mathf.Max(1, StartDay);
        if (StartHour > 0.0)
            _hour = StartHour;
        bool continued = LoadGame();
        GD.Print($"[save] つづきから={continued} 日付=8月{_day}日 " +
                 $"セミ={_totalCaught} ずかん={_collected.Count} はっけん={_found.Count}");
        _weather = WeatherOfDay(_day);
        Overcast = _weather != Weather.Sunny;
        if (RngSeed != 0)
            _rng.Seed = (ulong)RngSeed;
        else
            _rng.Randomize();
        SetupAudio();
        SetupRainFx();
        SetupFireworkFx();
        CollectSkyTinted(GetNodeOrNull("Backdrop"));
        CollectGround(this);
        _puddles = GetNodeOrNull<Node3D>("Puddles");
        _asagaoVine = GetNodeOrNull<Node3D>("Asagao/Vine");
        _asagaoBuds = GetNodeOrNull<Node3D>("Asagao/Buds");
        _asagaoFlowers = GetNodeOrNull<Node3D>("Asagao/Flowers");
        _asagaoPoles = GetNodeOrNull<Node3D>("Asagao/Poles");
        _asagaoFutaba = GetNodeOrNull<Node3D>("Asagao/Futaba");
        _festivalNode = GetNodeOrNull<Node3D>("Festival");
        _okuribi = GetNodeOrNull<Node3D>("Okuribi");
        if (GetNodeOrNull("Backdrop/PanoramaNight") is MeshInstance3D np)
            _nightPano = np.MaterialOverride as StandardMaterial3D;
        if (GetNodeOrNull("Backdrop/Panorama") is MeshInstance3D dp)
            _dayPano = dp.MaterialOverride as StandardMaterial3D;
        if (GetNodeOrNull("Backdrop/PanoramaDusk") is MeshInstance3D sk)
            _duskSky = sk.MaterialOverride as StandardMaterial3D;
        if (GetNodeOrNull("Backdrop/PanoramaRain") is MeshInstance3D rk)
            _rainSky = rk.MaterialOverride as StandardMaterial3D;
        // 初日ぶんの天気も反映する（ApplyWeather は日付が変わるときにしか呼ばれない）。
        // 遠景の材質を掴んだあとでないと、雨雲の帯に色が入らない
        ApplyWeather();
        RespawnCicadas();
        UpdateCamera(force: true);
        // 検査（SkipIntro）ではタイトルを出さない。人のセーブも導入も触らない
        _titleContinued = continued;
        if (_title != null)
            _title.Visible = !SkipIntro;
        _atTitle = !SkipIntro;
        if (_atTitle)
        {
            _player.Frozen = true;
            GetNode<CanvasLayer>("UI").Visible = false;
            if (_titlePrompt != null)
                _titlePrompt.Text = continued ? "スペースで つづきから" : "スペースで はじめる";
            return;
        }

        if (SkipIntro)
            ShowMessage($"2000年8月{_day}日。ニュータウンの なつやすみ。", 4.0);
        else if (continued)
            // 続きから。導入はもう見ているので流さない
            ShowMessage($"8月{_day}日。\nつづきから はじめる。", 4.0);
        else
            _ = PlayIntro();
    }

    public override void _Process(double delta)
    {
        if (_atTitle)
        {
            UpdateTitle(delta);
            return;
        }
        if (_vacationOver || _transitioning)
            return;
        if (CheckDex())
            return;   // ずかんを開いている間は時間も止める
        _hour += delta / SecondsPerHour;
        UpdateSky();
        UpdateKomorebi();
        UpdateOkuribi(delta);
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
        CheckRadio();
        CheckFolk();
        CheckAsagao();
        CheckShop();
        CheckStartle();
        _fishClock += delta;
        if (_fishingPutAwayAt > 0.0 && _fishClock >= _fishingPutAwayAt)
        {
            _fishingPutAwayAt = -1.0;
            _player.SetFishing(false);
        }
        if (!CheckFishing())
            CheckCatch();
        CheckDiscovery();
        if (_hour >= DayEndHour)
            _ = EndDay();
    }

    /// <summary>
    /// 木漏れ日の濃さ。晴れの正午が一番濃く、朝夕は寝て消える。
    /// 曇りは薄く、雨は出さない。影が天気と無関係に出ていると嘘になる。
    /// </summary>
    private void UpdateKomorebi()
    {
        if (_komorebi.Count == 0)
            return;
        float peak = _weather switch
        {
            Weather.Rainy => 0f,
            Weather.Cloudy => 0.3f,
            _ => 1f,
        };
        // 8:30〜17:30 の外は0。正午へ向かって上がる山にする
        float t = Mathf.Clamp((float)((_hour - 8.5) / 3.5), 0f, 1f)
                * Mathf.Clamp((float)((17.5 - _hour) / 3.5), 0f, 1f);
        float a = peak * t;
        foreach (StandardMaterial3D m in _komorebi)
        {
            m.AlbedoColor = new Color(1f, 1f, 1f, a);
            // 完全に透明な板を描き続けても意味が無い
            m.NoDepthTest = false;
        }
    }

    /// <summary>
    /// 送り火。8/16 の 17時から日暮れまで焚く。炎は同じ形のまま置くと
    /// 「オレンジの三角」に見えるので、大きさと灯りを不規則に揺らす。
    /// </summary>
    private void UpdateOkuribi(double delta)
    {
        if (_okuribi == null)
            return;
        bool on = _day == OkuribiDay && _hour >= 17.0;
        if (_okuribi.Visible != on)
            _okuribi.Visible = on;
        if (!on)
            return;

        _fireFlicker += delta;
        for (int i = 0; i < _okuribi.GetChildCount(); i++)
        {
            var fire = (Node3D)_okuribi.GetChild(i);
            // 炎ごとに位相をずらす。そろって揺れると機械に見える
            float w = Mathf.Sin((float)_fireFlicker * (5.3f + i * 1.7f)) * 0.5f
                    + Mathf.Sin((float)_fireFlicker * (11.1f + i * 2.3f)) * 0.25f;
            if (fire.GetNodeOrNull<Node3D>("Flame") is Node3D flame)
                flame.Scale = new Vector3(1f + w * 0.14f, 1f + w * 0.26f, 1f + w * 0.14f);
            if (fire.GetNodeOrNull<OmniLight3D>("Light") is OmniLight3D lamp)
                lamp.LightEnergy = 2.2f + w * 0.7f;
        }
    }

    // --- ずかん ---

    /// <summary>その種をどこで見つけるかの手がかり。未捕獲でもここは見せる。</summary>
    private static string SpeciesHint(Species sp)
    {
        if (sp.PondOnly)
            return "こうえんの いけ";
        if (sp.SapOnly)
            return "じゅえきの くぬぎ　あさと ゆうがた";
        if (sp.RainOnly)
            return "あめの ひ";
        string band = sp.FromHour < 10 ? "あさ" : sp.FromHour < 15 ? "ひる" : "ゆうがた";
        return $"きの みき　{band}（{sp.FromHour}〜{sp.ToHour}じ）";
    }

    /// <summary>開いている間は true。時間も操作も止める。</summary>
    private bool CheckDex()
    {
        if (Input.IsActionJustPressed("dex"))
        {
            _dexOpen = !_dexOpen;
            if (_dex != null)
                _dex.Visible = _dexOpen;
            _player.Frozen = _dexOpen;
            // 日付・匹数のラベルが透けて重なるので、開いている間は隠す
            GetNode<CanvasLayer>("UI").Visible = !_dexOpen;
            if (_dexOpen)
                FillDex();
        }
        return _dexOpen;
    }

    /// <summary>
    /// 捕った種は名前と手がかり、まだの種は「？？？」と手がかりだけ。
    /// 名前を伏せても手がかりを見せるのは、次にどこへ行けばいいかを渡すため。
    /// </summary>
    private void FillDex()
    {
        if (_dexTitle == null || _dexList == null)
            return;
        _dexTitle.Text = $"ずかん　{_collected.Count} / {AllSpecies.Length}";
        // 種類が増えるたびに一覧が枠からはみ出して footer と重なっていた
        // （9種で一度、11種でまた）。数から文字の大きさを決めて、
        // 何種になっても収まるようにする。行の高さは文字の約1.62倍
        const float ListHeight = 440f;
        int size = Mathf.Clamp(Mathf.FloorToInt(ListHeight / (AllSpecies.Length * 1.62f)), 15, 28);
        _dexList.AddThemeFontSizeOverride("font_size", size);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < AllSpecies.Length; i++)
        {
            bool got = _collected.Contains(i);
            string name = got ? AllSpecies[i].Name : "？？？";
            // 1種1行。2行使うと9種が画面に入らない
            sb.Append(got ? "●　" : "○　").Append(name.PadRight(8, '　'))
              .Append('　').Append(SpeciesHint(AllSpecies[i])).Append('\n');
        }

        _dexList.Text = sb.ToString();
        // 一覧の下に足すと 9種で埋まった枠からはみ出すので、footer に持たせる
        if (_dexFoot != null)
        {
            string card = $"ラジオたいそうの カード　{_stamps} / {RadioLastDay}";
            if (_marbles > 0)
                card += $"　　　ビー玉　{_marbles}こ";
            string tail = _collected.Count == AllSpecies.Length ? "ぜんぶ そろった。" : "";
            _dexFoot.Text = $"{card}\n{tail}Ｚ で とじる";
        }
    }

    // --- タイトル ---

    /// <summary>
    /// 押すまで待つ。押されたら UI を戻して、導入（初回）か本編（つづき）へ。
    /// </summary>
    private void UpdateTitle(double delta)
    {
        _titleBlink += delta;
        if (_titlePrompt != null)
            _titlePrompt.Modulate = new Color(1f, 1f, 1f, 0.45f + 0.55f * (float)((Mathf.Sin(_titleBlink * 2.4) + 1.0) * 0.5));
        if (!Input.IsActionJustPressed("ui_accept"))
            return;

        _atTitle = false;
        if (_title != null)
            _title.Visible = false;
        GetNode<CanvasLayer>("UI").Visible = true;
        _player.Frozen = false;
        if (_titleContinued)
            ShowMessage($"8月{_day}日。\nつづきから はじめる。", 4.0);
        else
            _ = PlayIntro();
    }

    // --- 固定カメラ（場面ごとに1アングル、位置で自動切替） ---

    private string ZoneFor(Vector3 p)
    {
        if (p.Z > 11.5f)
            return "CamStreet"; // 商店街：通りの軸で望遠
        if (p.Z < -6f)
            // 公園は東西で分ける。1台で 34m を見ると西端で主人公が 4% まで縮む
            return p.X < -2f ? "CamParkWest" : "CamPark";
        if (p.X < -10f)
            return "CamDanchi"; // 団地の広場：斜め見下ろし
        // 空き地はカメラの手前に来てしまうので、専用の画に切り替える
        return p.X > 12f ? "CamLot" : "CamPlaza";
    }

    private void UpdateCamera(bool force = false)
    {
        string zone = ZoneFor(_player.Position);
        if (zone == _zone && !force)
            return;
        _zone = zone;
        _cameras.GetNode<Camera3D>(zone).MakeCurrent();
        UpdatePanoramaPlate();
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
        _dateLabel.Visible = true;
        _bugLabel.Visible = true;
        _messageLabel.Visible = true;
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
        // EndDay が絵日記のために隠したラベルを戻す。ここを忘れると
        // 結末の文章が一行も出ないまま黒画面のまま終わる（実際に一度そうなった）
        _messageLabel.Visible = true;
        _dateLabel.Visible = true;
        _bugLabel.Visible = true;

        // 夏が終わったら記録を消す。次に起動したら、また8月1日から
        if (!SkipIntro && FileAccess.FileExists(SavePath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath));

        // この夏に増えたものを全部ここで返す。数えたものが最後に出てこないと、
        // 集めた意味がプレイヤーに戻らない
        string extra = "";
        if (_stamps > 0)
            extra += $"\nラジオたいそうの はんこは {_stamps}こ。";
        if (_marbles > 0)
            extra += $"\nビー玉は {_marbles}こ たまった。";
        if (_bloomSeen > 0)
            extra += $"\nあさがおが 咲いた朝を {_bloomSeen}回 見た。";
        if (_folkTalks > 0)
            extra += $"\n町の人と {_folkTalks}回 立ち話を した。";
        if (_goldfish > 0)
            extra += $"\n金魚は {_goldfish}ひき つれて かえった。";
        if (_foundLater.Count > 0)
            extra += $"\nおなじ ばしょへ {_foundLater.Count}回 もどって みた。";
        _messageLabel.Text =
            $"8月31日。なつやすみが おわった。\n" +
            $"つかまえた むし、ぜんぶで {_totalCaught}ひき。\n" +
            $"ずかんは {_collected.Count}/{AllSpecies.Length}しゅるい。\n" +
            $"おぼえて いる ばしょは {_found.Count}こ。{extra}";
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
        if (node.Name != "PanoramaNight" && node.Name != "PanoramaDusk" && node.Name != "PanoramaRain"
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
                // 雨の日はセミが鳴かない。ここで満音から始めると、
                // 開始直後にセミ→雨の入れ替わりが起きて音に穴が開く（実測 -42.7dB）
                VolumeDb = i == PhaseOfHour() && _weather != Weather.Rainy ? CicadaDb : -60f,
            };
            AddChild(player);
            player.Play();
            _cicadaVoices[i] = player;
            _voiceMix[i] = i == PhaseOfHour() && _weather != Weather.Rainy ? 1f : 0f;
        }

        // 効果音。虫あみを振った瞬間・捕れた瞬間・逃げられた瞬間に手応えを返す
        var rainStream = GD.Load<AudioStreamWav>("res://assets/audio/rain.wav");
        if (rainStream != null)
        {
            rainStream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            rainStream.LoopBegin = 0;
            rainStream.LoopEnd = rainStream.Data.Length / 2;
            // 雨の日に始めるときは最初から鳴らす（セミと同じ扱い）。
            // 音量はセミの床（-8dB）とそろえる。雨の日だけ 8dB 静かだと、
            // 「雨で世界が静まる」ではなく「音が抜けている」に聞こえる
            _rainVoice = new AudioStreamPlayer
            {
                Name = "Rain",
                Stream = rainStream,
                VolumeDb = _weather == Weather.Rainy ? RainDb : -60f,
            };
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
        // 花火は 70〜95m 先の空に上がる。玉が小さいと点にしか映らないので、
        // 距離に合わせて玉も広がりも大きく取る（半径0.16では点だった）
        var fade = new Gradient();
        fade.SetColor(0, new Color(1f, 1f, 1f, 1f));      // 開いた瞬間は白く光る
        fade.SetColor(1, new Color(1f, 1f, 1f, 0f));      // 尾を引いて消える
        fade.AddPoint(0.25f, new Color(1f, 1f, 1f, 0.95f));
        _fireworkFx = new CpuParticles3D
        {
            Name = "FireworkFx",
            Amount = 420,
            Lifetime = 2.6f,
            OneShot = true,
            Explosiveness = 1f,
            Emitting = false,
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = 9f,
            InitialVelocityMax = 16f,
            Gravity = new Vector3(0f, -4.5f, 0f),
            ScaleAmountMin = 1.1f,
            ScaleAmountMax = 2.4f,
            ColorRamp = fade,
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 5, Rings = 3 },
        };
        _fireworkFx.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 1f, 1f),
            VertexColorUseAsAlbedo = true,   // 粒ごとの色（Color と ColorRamp）を使う
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,   // 光なので加算
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
        // 置き方は画角から決める。28〜44m では団地（カメラから20m前後）の壁に
        // 重なって点にしか見えず、逆に高く上げすぎると画面の上に外れる。
        // カメラ前方 z に対して高さ y を比で取り、仰角を 12〜19° に収める。
        // （固定カメラの縦画角は 40〜55° なので、この比なら必ず画面に入る）
        float dist = _rng.RandfRange(58f, 78f);
        float rise = dist * _rng.RandfRange(0.21f, 0.34f);
        var local = new Vector3(_rng.RandfRange(-22f, 22f), rise, -dist);
        _fireworkFx.Position = cam.GlobalTransform * local;
        Color[] hues =
        {
            new(1f, 0.85f, 0.5f), new(1f, 0.5f, 0.55f), new(0.6f, 0.85f, 1f),
            new(0.7f, 1f, 0.7f), new(1f, 0.7f, 0.95f),
        };
        _fireworkFx.Color = hues[(int)(_rng.Randi() % (uint)hues.Length)];
        if (OS.GetEnvironment("DEBUG_FIREWORK") == "1")
            GD.Print($"[hanabi] {_zone} local={local} world={_fireworkFx.Position}");
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
            float target = (i == phase && _weather != Weather.Rainy) ? 1f : 0f;
            // 混ぜ具合を 0〜1 で動かす（約2.3秒で入れ替わる）。
            // dB を直線で動かすと、入れ替わりの真ん中で両方 -34dB まで下がり、
            // 合計で 8〜10dB の穴が開く（実測で確認）。
            // 振幅を sqrt(混ぜ具合) にすると出力の power が足して1に保たれる
            _voiceMix[i] = Mathf.MoveToward(_voiceMix[i], target, (float)delta * 0.44f);
            float amp = Mathf.Sqrt(_voiceMix[i]) * CicadaAmp;
            v.VolumeDb = amp > 0.001f ? Mathf.LinearToDb(amp) : -60f;
        }
        if (_rainVoice != null)
        {
            float rainTarget = _weather == Weather.Rainy ? RainDb : -60f;
            _rainVoice.VolumeDb = Mathf.MoveToward(_rainVoice.VolumeDb, rainTarget, (float)delta * 12f);
        }
    }

    /// <summary>
    /// いまの場面の実写を遠景に貼る。昼と夜の2枚を同時に差し替える。
    /// 無い場合は前の絵のままにする（届いていない場所があっても壊れない）。
    /// </summary>
    private void UpdatePanoramaPlate()
    {
        if (_zone == _panoZone || !ZonePlate.TryGetValue(_zone, out string place))
            return;
        var day = GD.Load<Texture2D>($"res://assets/plates/BG-{place}-noon.jpg");
        var night = GD.Load<Texture2D>($"res://assets/plates/BG-{place}-night.jpg");
        if (day == null && night == null)
            return;
        if (_dayPano != null && day != null)
            _dayPano.AlbedoTexture = day;
        if (_nightPano != null && night != null)
            _nightPano.AlbedoTexture = night;
        _panoZone = _zone;
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
        Color ambient = hor.Lerp(Colors.White, 0.45f) * 0.8f;
        float arc = Mathf.Sin(t * Mathf.Pi);
        _sun.RotationDegrees = new Vector3(-10f - 60f * arc, -150f + 120f * t, 0f);

        // 光の色は「太陽の高さ」ではなく「時刻」で決める。
        // 高さ（arc）に比例させていたときは、17時でもまだ (1, 0.83, 0.72) の
        // ほぼ白い光で、空だけ夕焼けなのに地面は真昼のままだった。
        // 夕方は 14時から効き始めて 19時で最も濃い金色になる。
        float evening = Mathf.Pow(Mathf.Clamp((float)((_hour - 14.0) / 5.0), 0f, 1f), 1.3f);
        float morning = Mathf.Clamp((float)((9.5 - _hour) / 3.5), 0f, 1f) * 0.55f;
        float warm = Mathf.Max(evening, morning);
        var noonLight = new Color(1f, 0.98f, 0.94f);
        var goldLight = new Color(1f, 0.6f, 0.3f);
        Color sunColor = noonLight.Lerp(goldLight, warm);
        _sun.LightColor = sunColor;
        // 金色になるほど弱くする。明るいまま色だけ変えると絵の具に見える
        _sun.LightEnergy = (0.55f + 0.65f * arc) * (1f - 0.38f * warm);

        // gl_compatibility では環境光が絵の大半を作る。太陽の色だけ金にしても
        // 芝が真昼のまま緑に光っていた（撮って確かめた）。環境光も一緒に
        // 暖色へ寄せて落とさないと、夕方は夕方に見えない
        // 17時だけ見て決めたら、18時過ぎが**セピア写真**のように茶色く潰れた。
        // 寄せ先を明るくし、寄せ具合に上限（0.7）を、暗くする量にも上限を置く。
        // 「夕方らしさ」は色の傾きで作るもので、暗くして作るものではない
        _env.AmbientLightColor = ambient.Lerp(new Color(0.62f, 0.5f, 0.44f), Mathf.Min(warm, 0.7f) * 0.75f)
                                 * (1f - 0.16f * warm);

        // 遠景も同じ光の下に置く。真昼は素の色、朝夕は太陽の色に寄せて暗くする
        Color sunTint = sunColor;
        float bright = 0.45f + 0.55f * arc;
        ApplySkyTint(Colors.White.Lerp(sunTint, 0.65f) * bright);

        // 夜の遠景（窓に灯りが点いた同じ町）を17時から重ねていく。
        // 昼版を暗くするだけでは窓の灯りは作れないので、別撮りを混ぜる
        if (_nightPano != null)
        {
            float nightAlpha = Mathf.Clamp((float)((_hour - 17.0) / 2.0), 0f, 1f);
            _nightPano.AlbedoColor = new Color(1f, 1f, 1f, nightAlpha);
        }

        // 夕焼け空は 16時から立ち上がり 17:40 で最大、そこから夜に譲って引く。
        // 山形にすることで「夕焼けの時間」がはっきり存在するようになる
        if (_duskSky != null)
        {
            float up = Mathf.Clamp((float)((_hour - 16.0) / 1.7), 0f, 1f);
            float down = 1f - Mathf.Clamp((float)((_hour - 17.7) / 1.3), 0f, 1f);
            float a = Mathf.Min(up, down) * (_weather == Weather.Rainy ? 0.25f : 1f);
            _duskSky.AlbedoColor = new Color(1f, 1f, 1f, a);
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
        _bugLabel.Text = $"むし ×{_totalCaught}   ずかん {_collected.Count}/{AllSpecies.Length}   " +
                         $"はっけん {_found.Count}/{Spots.Length}   {_money}円";
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
        if (NearShop())
        {
            _messageLabel.Text = "だがしや　スペースで はなす・かう";
            return;
        }
        int folk = NearestFolk();
        if (folk >= 0)
        {
            _messageLabel.Text = _folkTalkedDay == _day && _folkTalkedIndex == folk
                ? Folks[folk].Label
                : $"{Folks[folk].Label}　スペースで はなす";
            return;
        }
        if (AtAsagao())
        {
            _messageLabel.Text = _watchedDay == _day
                ? "あさがお"
                : "あさがお　スペースで みる";
            return;
        }
        if (AtRadio())
        {
            _messageLabel.Text = _stampedDay == _day
                ? "ラジオたいそう　きょうの はんこは おした"
                : "ラジオたいそう　スペースで はんこ";
            return;
        }
        if (AtPond())
        {
            _messageLabel.Text = _lineOutAt < 0.0
                ? "いけ　スペースで いとを たらす"
                : "…………";
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
        // 晴れ・くもりは時刻に合うセミ、雨の日は雨の生き物だけを候補にする
        bool rainy = _weather == Weather.Rainy;
        var pool = new List<int>();
        for (int i = 0; i < AllSpecies.Length; i++)
        {
            if (AllSpecies[i].PondOnly || AllSpecies[i].SapOnly)
                continue;   // 池で釣るもの・樹液に来るものは別枠で湧かせる
            if (AllSpecies[i].RainOnly != rainy)
                continue;
            if (rainy || (_hour >= AllSpecies[i].FromHour && _hour < AllSpecies[i].ToHour))
                pool.Add(i);
        }
        if (pool.Count == 0)
            pool.Add(2); // 保険（アブラゼミ）

        int count = Mathf.Min(CicadasPerDay, indices.Count);
        for (int k = 0; k < count; k++)
        {
            int sp = pool[(int)(_rng.Randi() % (uint)pool.Count)];
            var spot = new Node3D { Position = _treeSpots[indices[k]] };
            spot.AddChild(MakeCicadaBody(AllSpecies[sp], indices[k] + k));
            AddChild(spot);
            _cicadas.Add(spot);
            _cicadaSpecies.Add(sp);
        }
        SpawnSapBeetles();
        // どこに何が湧いたかは画面から探すしかなく、雨の生き物のように
        // 小さくて低い位置に出るものは見落とす。検査用に位置を出せるようにする
        if (OS.GetEnvironment("DEBUG_SPAWN") == "1")
        {
            for (int i = 0; i < _cicadas.Count; i++)
                GD.Print($"[spawn] {AllSpecies[_cicadaSpecies[i]].Name} @ {_cicadas[i].Position}");
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

    /// <summary>
    /// 地面に使っている材質を集める。雨の日に濡らすため。
    /// 壁まで濡らすと team が意図していない見た目になるので、
    /// 地面に使っているテクスチャ名だけを拾う。
    /// </summary>
    private void CollectGround(Node node)
    {
        if (node is MeshInstance3D mi && mi.MaterialOverride is StandardMaterial3D m &&
            m.AlbedoTexture != null)
        {
            string path = m.AlbedoTexture.ResourcePath;
            if (path.Contains("asphalt") || path.Contains("concrete_floor") ||
                path.Contains("dirt") || path.Contains("grass"))
            {
                _groundMats.Add((m, m.AlbedoColor, m.Roughness, m.Metallic));
            }
        }
        foreach (Node child in node.GetChildren())
            CollectGround(child);
    }

    /// <summary>雨の日は地面を暗く・つるりとさせる。乾いたままだと嘘になる。</summary>
    private void ApplyWetGround(bool wet)
    {
        foreach ((StandardMaterial3D m, Color albedo, float rough, float metal) in _groundMats)
        {
            m.AlbedoColor = wet ? albedo * new Color(0.72f, 0.75f, 0.8f, 1f) : albedo;
            m.Roughness = wet ? 0.3f : rough;
            m.Metallic = wet ? 0.25f : metal;
        }
        if (_puddles != null)
            _puddles.Visible = wet;
    }

    private void ApplyWeather()
    {
        _weather = WeatherOfDay(_day);
        Overcast = _weather != Weather.Sunny;
        if (_rainFx != null)
            _rainFx.Emitting = _weather == Weather.Rainy;
        // 遠景の実写は晴れの1枚だけなので、空は灰色の帯で覆って差し替える
        if (_rainSky != null)
        {
            float a = _weather switch
            {
                Weather.Rainy => 0.97f,
                Weather.Cloudy => 0.72f,
                _ => 0f,
            };
            _rainSky.AlbedoColor = new Color(1f, 1f, 1f, a);
        }
        ApplyWetGround(_weather == Weather.Rainy);
        UpdateAsagao();
        if (_festivalNode != null)
            _festivalNode.Visible = _day == FestivalDay;   // 屋台と提灯は当日だけ
    }

    /// <summary>朝=0 / 昼=1 / 夕=2。変わったら顔ぶれを入れ替える。</summary>
    private int PhaseOfHour() => _hour < 11.0 ? 0 : _hour < 16.0 ? 1 : 2;

    /// <summary>木にとまったセミ1匹。頭を上にして幹に貼りつく本物の姿勢で作る。</summary>
    /// <summary>
    /// 樹液の木のカブトムシ・クワガタ。朝（8〜9時半）と夕方（17時以降）だけ来る。
    /// 昼に来ないのは本当のことで、遊びとしても「行く時間を選ぶ」理由になる。
    /// 雨の日は来ない（雨の日は雨の生き物の日にする）。
    /// </summary>
    private void SpawnSapBeetles()
    {
        if (_weather == Weather.Rainy)
            return;
        bool morning = _hour >= DayStartHour && _hour < 9.5;
        bool evening = _hour >= 17.0;
        if (!morning && !evening)
            return;

        // 幹の同じ高さに2匹まで。カブトのほうが出やすい
        int count = 1 + (int)(_rng.Randi() % 2);
        for (int k = 0; k < count; k++)
        {
            int sp = _rng.Randf() < 0.62f ? SapKabuto : SapKuwagata;
            var spot = new Node3D
            {
                Position = SapTree + new Vector3(0f, 0f, 0f),
            };
            Node3D body = MakeBeetle(AllSpecies[sp], k);
            body.Position = new Vector3(0.3f - k * 0.62f, 0.95f + k * 0.22f, k == 0 ? 0.08f : -0.1f);
            spot.AddChild(body);
            AddChild(spot);
            _cicadas.Add(spot);
            _cicadaSpecies.Add(sp);
        }
    }

    /// <summary>甲虫。丸い背・角（カブト）またはあご（クワガタ）・6本の脚。</summary>
    private static Node3D MakeBeetle(Species sp, int variant)
    {
        var root = new Node3D { RotationDegrees = new Vector3(-8f, 90f + variant * 40f, 0f) };
        var shell = new StandardMaterial3D { AlbedoColor = sp.Color, Roughness = 0.35f };
        var dark = new StandardMaterial3D { AlbedoColor = sp.Color * 0.6f, Roughness = 0.4f };

        var body = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.07f, Height = 0.12f },
            MaterialOverride = shell,
            Scale = new Vector3(1f, 1f, 1.55f),
        };
        root.AddChild(body);
        root.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.045f, Height = 0.07f },
            MaterialOverride = dark,
            Position = new Vector3(0f, 0.01f, -0.1f),
        });

        if (sp.Name == "カブトムシ")
        {
            // 角。前へ突き出して先で上を向く
            var horn = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.016f, Height = 0.14f },
                MaterialOverride = dark,
                Position = new Vector3(0f, 0.05f, -0.16f),
                RotationDegrees = new Vector3(-62f, 0f, 0f),
            };
            root.AddChild(horn);
        }
        else
        {
            // あご。左右に開いた2本
            foreach (float mx in new[] { -0.03f, 0.03f })
            {
                root.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.011f, Height = 0.11f },
                    MaterialOverride = dark,
                    Position = new Vector3(mx, 0.015f, -0.17f),
                    RotationDegrees = new Vector3(-74f, 0f, mx > 0f ? -16f : 16f),
                });
            }
        }

        for (int i = 0; i < 3; i++)
        {
            foreach (float lx in new[] { -0.06f, 0.06f })
            {
                root.AddChild(new MeshInstance3D
                {
                    Mesh = new CapsuleMesh { Radius = 0.008f, Height = 0.07f },
                    MaterialOverride = dark,
                    Position = new Vector3(lx, -0.03f, -0.06f + i * 0.07f),
                    RotationDegrees = new Vector3(0f, 0f, lx > 0f ? -58f : 58f),
                });
            }
        }
        return root;
    }

    private static Node3D MakeCicadaBody(Species sp, int variant)
    {
        if (sp.RainOnly)
            return MakeRainCreature(sp, variant);

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

    /// <summary>雨の日の生き物。木の根元近くに居るので、セミより低い位置に置く。</summary>
    private static Node3D MakeRainCreature(Species sp, int variant)
    {
        var root = new Node3D
        {
            Position = new Vector3(0.34f, 0.22f, 0.16f),
            RotationDegrees = new Vector3(0f, (variant * 61) % 360, 0f),
        };
        var body = new StandardMaterial3D { AlbedoColor = sp.Color, Roughness = 0.5f };

        if (sp.Name == "カタツムリ")
        {
            // 殻（渦は作れないので円環で代用）と、伸びた体と触角
            root.AddChild(new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.03f, OuterRadius = 0.11f },
                MaterialOverride = body,
                Position = new Vector3(0f, 0.11f, -0.02f),
                RotationDegrees = new Vector3(90f, 0f, 0f),
            });
            var soft = new StandardMaterial3D { AlbedoColor = new Color(0.78f, 0.72f, 0.62f), Roughness = 0.4f };
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = 0.035f, Height = 0.2f },
                MaterialOverride = soft,
                Position = new Vector3(0f, 0.035f, 0.06f),
                RotationDegrees = new Vector3(90f, 0f, 0f),
            });
            foreach (float ex in new[] { -0.022f, 0.022f })
            {
                root.AddChild(new MeshInstance3D
                {
                    Mesh = new CapsuleMesh { Radius = 0.006f, Height = 0.07f },
                    MaterialOverride = soft,
                    Position = new Vector3(ex, 0.075f, 0.14f),
                    RotationDegrees = new Vector3(24f, 0f, 0f),
                });
            }
            return root;
        }

        // アマガエル
        root.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.075f, Height = 0.12f },
            MaterialOverride = body,
            Position = new Vector3(0f, 0.07f, 0f),
        });
        foreach (float ex in new[] { -0.035f, 0.035f })
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.024f, Height = 0.042f },
                MaterialOverride = body,
                Position = new Vector3(ex, 0.115f, 0.02f),
            });
            root.AddChild(new MeshInstance3D    // 後ろ足
            {
                Mesh = new CapsuleMesh { Radius = 0.016f, Height = 0.09f },
                MaterialOverride = body,
                Position = new Vector3(ex * 1.9f, 0.03f, -0.03f),
                RotationDegrees = new Vector3(72f, 0f, 0f),
            });
        }
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

    /// <summary>池のふちに立っているか。ふちから離れると糸は自動で切れる。</summary>
    private bool AtPond()
    {
        float d = new Vector2(_player.Position.X, _player.Position.Z).DistanceTo(PondCenter);
        return d >= PondEdgeIn && d <= PondEdgeOut;
    }

    /// <summary>
    /// ザリガニつり。戻り値 true のときは虫あみを振らせない（入力を食う）。
    /// 待つ → 当たる → 引く の3拍。早く引くと逃げるので、待つことに意味が出る。
    /// </summary>
    private bool CheckFishing()
    {
        if (!AtPond())
        {
            _lineOutAt = -1.0;   // 池から離れたら糸は仕舞う
            _player.SetFishing(false);
            return false;
        }

        double now = _fishClock;

        // 当たりの合図。合図から BiteWindow 秒だけ引ける
        if (_lineOutAt >= 0.0 && _biteUntil <= 0.0 && now >= _biteAt)
        {
            _biteUntil = now + BiteWindow;
            PlaySfx(_sfxSwing);
            ShowMessage("ツン、と ひいた！　いま スペース！", BiteWindow);
        }
        // 合図を見逃した
        if (_biteUntil > 0.0 && now > _biteUntil)
        {
            _lineOutAt = -1.0;
            _biteUntil = 0.0;
            _player.SetFishing(false);
            PlaySfx(_sfxEscape);
            ShowMessage("……いかれた。");
            return true;
        }

        if (!Input.IsActionJustPressed("ui_accept"))
            return true;

        if (_lineOutAt < 0.0)
        {
            _lineOutAt = now;
            _biteAt = now + _rng.RandfRange(1.8f, 4.6f);
            _biteUntil = 0.0;
            // ウキは水面（半径6.4・y=0.03）の内側に落とす。
            // プレイヤーから池の中心へ向かって取ると、どこに立っても水の上になる
            var toCenter = (PondCenter - new Vector2(_player.Position.X, _player.Position.Z)).Normalized();
            Vector2 bob = PondCenter - toCenter * 5.5f;
            _player.SetFishing(true, new Vector3(bob.X, 0.05f, bob.Y));   // 虫あみ → 竿
            _player.SwingNet();
            ShowMessage("いとを たらして まった。", 5.0);
            return true;
        }

        if (_biteUntil <= 0.0)
        {
            // 当たる前に引いた
            _lineOutAt = -1.0;
            _player.SwingNet();
            _player.SetFishing(false);
            PlaySfx(_sfxEscape);
            ShowMessage("まだ はやい。にげられた。");
            return true;
        }

        // 当たりに応えた。あおりの絵を見せてから竿を仕舞う
        _lineOutAt = -1.0;
        _biteUntil = 0.0;
        _player.SwingNet();
        _fishingPutAwayAt = _fishClock + 0.6;
        Species cray = AllSpecies[CrayfishIndex];
        if (_rng.Randf() < cray.CatchRate)
        {
            _totalCaught++;
            _todayCaught++;
            PlaySfx(_sfxCatch);
            if (_collected.Add(CrayfishIndex))
                ShowMessage($"{cray.Name}を つりあげた！\nずかんに はじめて のった！", 3.5);
            else
                ShowMessage($"{cray.Name}を つりあげた！");
        }
        else
        {
            PlaySfx(_sfxEscape);
            ShowMessage("あっ、はさみを はなされた……");
        }
        return true;
    }

    /// <summary>
    /// 走っている間、近くの虫は逃げる。止まれば逃げないので、
    /// 「見つけたら歩きに切り替える」が正しい遊び方になる。
    /// 池のザリガニは水の中なので関係ない（対象から外れている）。
    /// </summary>
    private void CheckStartle()
    {
        if (_player == null || !_player.Running)
            return;
        for (int i = _cicadas.Count - 1; i >= 0; i--)
        {
            if (_player.Position.DistanceTo(_cicadas[i].Position) >= StartleRange)
                continue;
            string name = AllSpecies[_cicadaSpecies[i]].Name;
            _cicadas[i].QueueFree();
            _cicadas.RemoveAt(i);
            _cicadaSpecies.RemoveAt(i);
            PlaySfx(_sfxEscape);
            ShowMessage(_toldStartle
                ? $"{name}に にげられた。"
                : $"{name}に にげられた……。\nそっと ちかづかないと だめだ。", 3.0);
            _toldStartle = true;
            return;   // 1フレームに1匹まで。まとめて消えると理由が伝わらない
        }
    }

    private void CheckCatch()
    {
        if (!Input.IsActionJustPressed("ui_accept"))
            return;
        // セミが居なくても振る。音だけでなく見た目でも操作に答える
        _player.SwingNet();
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

    // --- 中断と再開（毎朝 自動保存。メニューは持たない） ---

    private static Godot.Collections.Array FromSet(HashSet<int> set)
    {
        var a = new Godot.Collections.Array();
        foreach (int i in set)
            a.Add(i);
        return a;
    }

    private void SaveGame()
    {
        if (SkipIntro)
            return;   // 検査実行では書かない
        var collected = new Godot.Collections.Array();
        foreach (int i in _collected)
            collected.Add(i);
        var found = new Godot.Collections.Array();
        foreach (int i in _found)
            found.Add(i);
        var data = new Godot.Collections.Dictionary
        {
            ["day"] = _day,
            ["totalCaught"] = _totalCaught,
            ["talkCount"] = _talkCount,
            ["money"] = _money,
            ["marbles"] = _marbles,
            ["stamps"] = _stamps,
            ["bloomSeen"] = _bloomSeen,
            ["folkTalks"] = _folkTalks,
            ["goldfish"] = _goldfish,
            ["foundLater"] = FromSet(_foundLater),
            ["collected"] = collected,
            ["found"] = found,
        };
        using FileAccess f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (f == null)
        {
            GD.PushWarning($"セーブできない: {FileAccess.GetOpenError()}");
            return;
        }
        f.StoreString(Json.Stringify(data));
    }

    /// <summary>保存があれば読み込む。読めれば true（導入を飛ばして続きから）。</summary>
    private bool LoadGame()
    {
        if (SkipIntro || !FileAccess.FileExists(SavePath))
            return false;
        using FileAccess f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (f == null)
            return false;
        Variant parsed = Json.ParseString(f.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
            return false;
        var data = parsed.AsGodotDictionary();

        _day = Mathf.Clamp((int)data["day"], 1, LastDay);
        _totalCaught = (int)data["totalCaught"];
        _talkCount = (int)data["talkCount"];
        // 古いセーブには入っていないので、無ければ既定に戻す
        _money = data.ContainsKey("money") ? (int)data["money"] : DailyAllowance;
        _marbles = data.ContainsKey("marbles") ? (int)data["marbles"] : 0;
        _stamps = data.ContainsKey("stamps") ? (int)data["stamps"] : 0;
        _bloomSeen = data.ContainsKey("bloomSeen") ? (int)data["bloomSeen"] : 0;
        _folkTalks = data.ContainsKey("folkTalks") ? (int)data["folkTalks"] : 0;
        _goldfish = data.ContainsKey("goldfish") ? (int)data["goldfish"] : 0;
        _foundLater.Clear();
        if (data.ContainsKey("foundLater"))
        {
            foreach (Variant v in data["foundLater"].AsGodotArray())
                _foundLater.Add((int)v);
        }
        _collected.Clear();
        foreach (Variant v in data["collected"].AsGodotArray())
            _collected.Add((int)v);
        _found.Clear();
        foreach (Variant v in data["found"].AsGodotArray())
            _found.Add((int)v);
        return true;
    }

    // --- 駄菓子屋（町にいる ただ一人の相手） ---

    /// <summary>ラジオ体操の台の前に立っていて、まだ押せる朝か。</summary>
    private bool AtRadio()
    {
        if (_day > RadioLastDay || _hour < RadioFromHour || _hour >= RadioToHour)
            return false;
        return new Vector2(_player.Position.X, _player.Position.Z).DistanceTo(RadioPos) < 2.6f;
    }

    /// <summary>その日 咲いている花の数。日付から決まるので毎回同じ。</summary>
    private int BloomCount(int day)
    {
        if (day < 16)
            return 0;
        // 咲かない朝もある。毎朝必ず咲くと「見に行く意味」が薄れる
        int h = (day * 53 + 17) % 7;
        if (h == 0)
            return 0;
        return Mathf.Min(1 + (day - 16) / 4, 5);
    }

    /// <summary>
    /// その日の姿にする。つるは節を下から出し、つぼみと花は数で見せる。
    /// 一日ぶんずつしか変わらないので、毎朝見に来ないと変化に気づけない。
    /// </summary>
    private void UpdateAsagao()
    {
        if (_asagaoVine == null)
            return;
        // 8/1 から一節は出しておく（何も無いのに「ふたばが 出て いる」と
        // 言っていた）。8/25 ごろに12節そろう
        int segs = Mathf.Clamp((_day + 1) / 2, 1, 12);
        if (_asagaoFutaba != null)
            _asagaoFutaba.Visible = _day < 9;
        for (int i = 0; i < _asagaoVine.GetChildCount(); i++)
            ((Node3D)_asagaoVine.GetChild(i)).Visible = i < segs;
        if (_asagaoPoles != null)
            _asagaoPoles.Visible = _day >= 6;

        int blooms = BloomCount(_day);
        // つぼみは花になる前の数日だけ。咲いた花のぶんは引く
        int buds = _day >= 13 ? Mathf.Clamp((_day - 12) / 3, 0, 5) : 0;
        for (int i = 0; i < _asagaoBuds.GetChildCount(); i++)
            ((Node3D)_asagaoBuds.GetChild(i)).Visible = i < buds && i >= blooms;
        for (int i = 0; i < _asagaoFlowers.GetChildCount(); i++)
            ((Node3D)_asagaoFlowers.GetChild(i)).Visible = i < blooms;
    }

    /// <summary>いちばん近い町の人。範囲外なら -1。</summary>
    private int NearestFolk()
    {
        var p = new Vector2(_player.Position.X, _player.Position.Z);
        int best = -1;
        float bestDist = FolkRange;
        for (int i = 0; i < Folks.Length; i++)
        {
            float d = p.DistanceTo(Folks[i].Pos);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>
    /// 町の人と立ち話。人ごと・日ごとに台詞が変わる。
    /// 同じ日に同じ人へ何度も話しかけても、話は増やさない。
    /// </summary>
    private void CheckFolk()
    {
        int idx = NearestFolk();
        if (idx < 0 || !Input.IsActionJustPressed("ui_accept"))
            return;
        if (_folkTalkedDay == _day && _folkTalkedIndex == idx)
        {
            ShowMessage("「……」", 1.6);
            return;
        }
        _folkTalkedDay = _day;
        _folkTalkedIndex = idx;
        _folkTalks++;
        string[] lines = Folks[idx].Lines;
        ShowMessage(lines[(_day - 1 + idx * 2) % lines.Length], 4.0);
    }

    private bool AtAsagao()
    {
        return new Vector2(_player.Position.X, _player.Position.Z).DistanceTo(AsagaoPos) < 2.2f;
    }

    /// <summary>あさがおを見る。日に一度だけ、その日の姿を言葉にする。</summary>
    private void CheckAsagao()
    {
        if (!AtAsagao() || !Input.IsActionJustPressed("ui_accept"))
            return;
        if (_watchedDay == _day)
        {
            ShowMessage("あさがお。けさは もう 見た。", 2.0);
            return;
        }
        _watchedDay = _day;
        int blooms = BloomCount(_day);
        string line;
        if (_day <= 3)
            line = "ふたばが 出て いる。\nまだ これだけ。";
        else if (_day < 6)
            line = "ほんばが 一まい ふえた。";
        else if (_day < 13)
            line = "つるが しちゅうに まきついて いる。\nひだり まわりだ。";
        else if (blooms == 0 && _day < 16)
            line = "つぼみが ついた。\nまだ かたい。";
        else if (blooms == 0)
            line = "きょうは ひとつも 咲いて いない。\nそういう 朝も ある。";
        else
        {
            _bloomSeen++;
            line = blooms == 1
                ? "ひとつ 咲いて いた。\nむらさきだった。"
                : $"{blooms}つ 咲いて いた。\nあさの うちだけの 色だ。";
        }
        PlaySfx(_sfxCatch);
        ShowMessage(line, 3.5);
    }

    private void CheckRadio()
    {
        if (!AtRadio() || _stampedDay == _day || !Input.IsActionJustPressed("ui_accept"))
            return;
        _stampedDay = _day;
        _stamps++;
        PlaySfx(_sfxCatch);
        ShowMessage($"ラジオたいそう。\nカードに はんこを おしてもらった。（{_stamps}/{RadioLastDay}）", 3.5);
    }

    private bool NearShop()
    {
        return new Vector2(_player.Position.X, _player.Position.Z).DistanceTo(ShopPos) < TalkRange;
    }

    private void CheckShop()
    {
        if (!NearShop())
        {
            if (_shopOpen)
                _player.Frozen = false;
            _shopOpen = false;   // 店先を離れたら品書きは閉じる
            return;
        }

        if (_shopOpen)
        {
            UpdateShelfInput();
            return;
        }
        if (!Input.IsActionJustPressed("ui_accept"))
            return;

        if (_talkedDay != _day)
        {
            // その日の一言を先に。買い物はそのあと開く
            _talkedDay = _day;
            string line = GrannyLines[(_day - 1) % GrannyLines.Length];
            _talkCount++;
            ShowMessage(line, 5.0);
        }
        _shopOpen = true;
        _shopPick = 0;
        // 品書きは矢印で選ぶので、開いている間は歩かせない
        _player.Frozen = true;
        ShowShelf();
    }

    /// <summary>その日の品書き。夏まつりの日は屋台の品に入れ替わる。</summary>
    private Goods[] TodayShelf() => _day == FestivalDay ? FestivalShelf : Shelf;

    /// <summary>品書き。矢印で選び、スペースで買う。</summary>
    private void ShowShelf()
    {
        Goods[] shelf = TodayShelf();
        var sb = new System.Text.StringBuilder();
        sb.Append(_day == FestivalDay ? $"なつまつり　おこづかい {_money}円\n" : $"おこづかい {_money}円\n");
        // 横に並べると、品名が長い日（金魚すくい 200円）に行が折り返して
        // 「200」と「円」が分かれた。1品1行なら品が増えても崩れない
        for (int i = 0; i < shelf.Length; i++)
        {
            sb.Append(i == _shopPick ? "▶ " : "　 ")
              .Append(shelf[i].Name).Append('　').Append(shelf[i].Price).Append("円\n");
        }
        sb.Append("やじるしで えらぶ／スペースで かう／Ｚ で やめる");
        ShowMessage(sb.ToString(), 60.0);
    }

    private void UpdateShelfInput()
    {
        if (Input.IsActionJustPressed("dex"))
        {
            _shopOpen = false;
            _player.Frozen = false;
            ShowMessage("「また おいで」", 2.0);
            return;
        }
        if (Input.IsActionJustPressed("ui_right") || Input.IsActionJustPressed("ui_down"))
        {
            _shopPick = (_shopPick + 1) % TodayShelf().Length;
            ShowShelf();
            return;
        }
        if (Input.IsActionJustPressed("ui_left") || Input.IsActionJustPressed("ui_up"))
        {
            _shopPick = (_shopPick + TodayShelf().Length - 1) % TodayShelf().Length;
            ShowShelf();
            return;
        }
        if (!Input.IsActionJustPressed("ui_accept"))
            return;

        Goods g = TodayShelf()[_shopPick];
        if (_money < g.Price)
        {
            ShowMessage("「おかねが たりないねえ。\nあしたに しようか」", 3.0);
            _shopOpen = false;
            _player.Frozen = false;
            return;
        }
        _money -= g.Price;
        PlaySfx(_sfxCatch);
        _shopOpen = false;
        _player.Frozen = false;
        _todayBought = g.Name;

        if (g.Name == "ラムネ")
        {
            _marbles++;
            ShowMessage($"{g.Bought}\nビー玉が {_marbles}こに なった。", 4.5);
        }
        else if (g.Name == "金魚すくい")
        {
            // 破れるまでに何匹すくえるか。0匹の年もある
            int got = (int)(_rng.Randi() % 4u);
            _goldfish += got;
            ShowMessage(got == 0
                ? "いっぱつで やぶれた。\nおじさんが 一ぴき くれた。"
                : $"{got}ひき すくえた。\nふくろの 水が ゆれて いる。", 4.0);
            if (got == 0)
                _goldfish++;
        }
        else if (g.Name == "あたりくじ")
        {
            // 4回に1回あたり。小さい賭けだが、当たると声が出る
            bool win = _rng.Randf() < 0.25f;
            if (win)
            {
                _money += g.Price * 2;
                ShowMessage("「あたりだ！」\nおばあさんが 40円 くれた。", 4.0);
            }
            else
            {
                ShowMessage("「はずれ。まあ そんなもんさ」\nかみきれを ポケットに いれた。", 4.0);
            }
        }
        else
        {
            ShowMessage(g.Bought, 4.5);
        }
    }

    // --- 発見（締切も失敗も無い。歩けば増える） ---

    private void CheckDiscovery()
    {
        var here = new Vector2(_player.Position.X, _player.Position.Z);
        for (int i = 0; i < Spots.Length; i++)
        {
            if (here.DistanceTo(Spots[i].Pos) > DiscoverRange)
                continue;

            if (!_found.Contains(i))
            {
                _found.Add(i);
                _foundToday.Add(i);
                if (_todayFound == "")
                    _todayFound = Spots[i].Text.Replace("\n", "");
                ShowMessage(Spots[i].Text, 5.0);
                return;
            }
            // 夏の後半、同じ場所へもう一度来ると、ひと月ぶんの時間が乗った
            // 独白が出る。1か所につき1回だけ
            if (_day >= LaterDay && !_foundLater.Contains(i) && !_foundToday.Contains(i))
            {
                _foundLater.Add(i);
                if (_todayFound == "")
                    _todayFound = Spots[i].Later.Replace("\n", "");
                ShowMessage(Spots[i].Later, 5.0);
                return;
            }
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
        if (_weather == Weather.Rainy && _todayCaught == 0)
            line = "あめ。セミは 一ぴきも 鳴かなかった。";
        else if (_weather == Weather.Rainy)
            line = $"あめの日にしか いない むしを {_todayCaught}ひき つかまえた。";
        if (Events.TryGetValue(_day, out Event todayEvent))
            return $"【日記】8月{_day}日({Weekday()})\n{line}\n{todayEvent.Diary}\nあしたは なにを しようかな。";

        // 祭りが近づくと日記が数え始める。これが「待ち遠しさ」になる
        int toFestival = FestivalDay - _day;
        if (toFestival is > 0 and <= 5)
            return $"【日記】8月{_day}日({Weekday()})\n{line}\n" +
                   $"あと {toFestival}日で なつまつり。\nあしたは なにを しようかな。";

        if (_watchedDay == _day && BloomCount(_day) > 0)
            return $"【日記】8月{_day}日({Weekday()})\n{line}\n" +
                   $"あさがおが {BloomCount(_day)}つ 咲いて いた。\n" +
                   "あしたは なにを しようかな。";

        if (_stampedDay == _day && _day < RadioLastDay)
            return $"【日記】8月{_day}日({Weekday()})\n{line}\n" +
                   $"あさ、はんこを おしてもらった。（{_stamps}/{RadioLastDay}）\n" +
                   "あしたは なにを しようかな。";

        if (_todayBought != "")
            return $"【日記】8月{_day}日({Weekday()})\n{line}\n" +
                   $"だがしやで {_todayBought}を かった。\nあしたは なにを しようかな。";

        if (_talkedDay == _day)
            return $"【日記】8月{_day}日({Weekday()})\n{line}\n" +
                   $"だがしやの おばあさんと はなした。\nあしたは なにを しようかな。";

        string extra = _todayFound != ""
            ? _todayFound
            : "とくべつな ことは なかった。それも わるくない。";
        return $"【日記】8月{_day}日({Weekday()})\n{line}\n{extra}\nあしたは なにを しようかな。";
    }

    /// <summary>
    /// その日の最後の画面を1枚だけ焼き取って、絵日記の「絵」にする。
    /// 絵を毎日別に描く手段が無いので、実際に見ていた景色をそのまま貼る。
    /// 暗転する前に撮らないと、真っ黒な絵になる。
    /// </summary>
    private async Task GrabDiaryShot()
    {
        if (_diaryShot == null)
            return;
        // 日付や匹数のラベルごと焼き付いてしまうので、先に隠して1フレーム待つ。
        // GetImage は「最後に描かれた絵」を返すので、隠した直後では間に合わない。
        _dateLabel.Visible = false;
        _bugLabel.Visible = false;
        _messageLabel.Visible = false;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Image img = GetViewport().GetTexture().GetImage();
        if (img != null)
            _diaryShot.Texture = ImageTexture.CreateFromImage(img);
    }

    private async Task EndDay()
    {
        _transitioning = true;
        _player.Frozen = true;
        await GrabDiaryShot();   // 暗転の前に、ラベルを外して撮る
        Tween fadeOut = CreateTween();
        fadeOut.TweenProperty(_fade, "color:a", 1.0f, 1.0);
        await ToSignal(fadeOut, Tween.SignalName.Finished);

        if (_day >= LastDay)
        {
            _vacationOver = true;
            await PlayEnding();
            return;
        }

        if (_diary != null && _diaryText != null)
        {
            _diaryText.Text = DiaryText();
            _diary.Visible = true;
            await ToSignal(GetTree().CreateTimer(5.0), SceneTreeTimer.SignalName.Timeout);
            _diary.Visible = false;
        }
        else
        {
            _messageLabel.Text = DiaryText();
            await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        }

        _day++;
        _hour = DayStartHour;
        _todayCaught = 0;
        _todayFound = "";
        _todayBought = "";
        _foundToday.Clear();
        // おこづかいは毎朝入る。ためこめるが、貯めることが目的にならない額にする
        _money = Mathf.Min(_money + DailyAllowance, 900);
        ApplyWeather();
        SaveGame();   // 一日が変わるたびに保存。中断してもここから再開できる
        _player.Position = new Vector3(-14f, 0.1f, 0f); // 団地の広場から一日開始
        RespawnCicadas();
        _messageLabel.Text = "";
        Tween fadeIn = CreateTween();
        fadeIn.TweenProperty(_fade, "color:a", 0.0f, 1.0);
        await ToSignal(fadeIn, Tween.SignalName.Finished);
        _player.Frozen = false;
        _transitioning = false;
        string morning = Events.TryGetValue(_day, out Event ev)
            ? ev.Morning
            : $"8月{_day}日の あさ。ラジオたいそう おわり！";
        // 8/7 は「はんこが そろった」と決め打ちしていたが、押していない朝もある。
        // 実際に押した数で言い方を変える
        if (_day == RadioLastDay)
        {
            morning = _stamps >= RadioLastDay - 1
                ? $"きょうで ラジオたいそうは おしまい。\nはんこは {_stamps}こ たまった。"
                : $"きょうで ラジオたいそうは おしまい。\nはんこは {_stamps}こ だけだった。";
        }
        ShowMessage(morning, 4.0);
    }
}

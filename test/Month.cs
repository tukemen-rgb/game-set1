using Godot;

/// <summary>
/// 8月1日〜31日を自動で走破して、出た文・行事・結末を全部ログに流す検査。
///   export DOTNET_ROOT=/opt/dotnet PATH="$PATH:/opt/dotnet"; dotnet build -v q --nologo
///   DEBUG_MSG=1 DEBUG_SPAWN=1 godot --headless --path . --fixed-fps 30 --quit-after 29000 \
///     --script res://test/Month.cs 2>&1 | grep -E "^\[msg|^\[spawn|^\[day|^\[label|^\[diary|^\[dex"
/// 1日は約22秒（SecondsPerHour=2.0）＋日記の暗転 約7秒。--headless でも動く（速い）。
/// 毎日、(a) 団地の広場 → (b) 8時台にラジオ体操の台で決定 → 町の人2人に話す
/// → (c) あさがおで決定 → (d) 自販機の前で決定 → (e) 駄菓子屋で決定2回（一言→品書き）
/// → Z で閉じる → チャイムの後に おじいさんに話す、を瞬間移動で回す。MONTH_START=n で開始日を変えられる（分割して走らせる用）。
/// 行動の間隔は 1.2 秒以上あける。ShowMessage は「前の文を出してから 1.2 秒未満」の
/// 文を待ち行列（2つまで）に回し、あふれた文は即上書きするので、0.4 秒刻みで
/// 押すと検査の押し方そのものが上書き警告を作ってしまう（0.35秒/時で試して
/// 実際にそうなった。待ち行列に残った前日の文が翌日に出るところまで見えた）。
/// 出力:
///   [day]   日付ラベルが変わった（その日の行動開始）
///   [label] 画面の文（MessageLabel）が変わった。ShowMessage を通らない
///           品書き・結末の文もここに出る
///   [diary] 絵日記の本文（DiaryText は ShowMessage を通らないので直接読む）
///   [dex]   品書きを Z で閉じたはずなのに ずかんが開いていた
/// </summary>
public partial class Month : SceneTree
{
    private double _t;          // 起動からの秒
    private double _dt;         // その日の開始からの秒
    private int _day = -1;
    private int _step;          // その日の行動の進み
    private Node _main;
    private Node3D _player;
    private Label _msg, _date, _diaryText;
    private CanvasLayer _dex, _diary;
    private string _lastMsg = "", _lastDiary = "";
    private bool _dexBugLogged;

    // (秒, 行動)。行動: P=瞬間移動, A=決定, Z=ずかんキー, R=→
    // 時刻の目安（2.0秒=1時間）: 8時台=0〜2s, 11:00=6s（合図）, 16:00=16s（合図）,
    // 17:00=18s（チャイム）, 19:00=22s（日の終わり）。
    // 前の文から 1.2 秒以上あけて押す（ShowMessage の待ち行列に入れない）。
    // 品書きは 16:00 の合図の前に閉じる（合図が品書きを上書きするのは別途記す）
    private (double At, char Kind, Vector3 Pos)[] _plan =
    {
        (0.03, 'P', new Vector3(-16f, 0.1f, 1.5f)),    // ラジオ体操の台（8時台）
        (1.30, 'A', Vector3.Zero),                     // はんこ（朝の文を1.3秒読んでから）
        (3.20, 'P', new Vector3(-19.6f, 0.1f, -0.2f)), // となりの おばさん
        (3.25, 'A', Vector3.Zero),
        (4.70, 'P', new Vector3(-18.6f, 0.1f, -1.1f)), // おばさん
        (4.75, 'A', Vector3.Zero),
        (7.45, 'P', new Vector3(-13.6f, 0.1f, 1.35f)), // あさがお（11:00 の合図の後）
        (7.50, 'A', Vector3.Zero),
        (9.45, 'P', new Vector3(14.5f, 0.1f, 9.2f)),   // 自販機の前（発見＋十円）
        (9.50, 'A', Vector3.Zero),
        (11.60, 'P', new Vector3(4f, 0.1f, 14.6f)),    // 駄菓子屋
        (11.65, 'A', Vector3.Zero),                    // 一言
        (13.30, 'A', Vector3.Zero),                    // 品書き
        (14.60, 'Z', Vector3.Zero),                    // 閉じる（MONTH_QUICKZ=1 なら 0.5 秒後）
        (17.00, 'P', new Vector3(-14f, 0.1f, 0f)),     // 団地の広場に戻る（チャイム前）
        (19.60, 'P', new Vector3(-1.5f, 0.1f, 15.2f)), // ベンチの おじいさん（チャイムの後）
        (19.65, 'A', Vector3.Zero),
    };
    private const int ZStep = 13;   // _plan の Z の位置
    private double _pressAcceptUntil = -1, _pressDexUntil = -1, _pressRightUntil = -1;

    public override void _Initialize()
    {
        var packed = GD.Load<PackedScene>("res://scenes/summer_main.tscn");
        _main = packed.Instantiate();
        string sph = OS.GetEnvironment("MONTH_SPH");
        _main.Set("SecondsPerHour", sph != "" ? sph.ToFloat() : 2.0);   // 1日 ≒ 22秒
        if (OS.GetEnvironment("MONTH_QUICKZ") == "1")
            _plan[ZStep].At = 13.80;   // 品書きを開いて 0.5 秒で Z（待ち行列の検査）
        if (OS.GetEnvironment("MONTH_BUY") == "1")
        {
            // 品書きで → を押して 0.4 秒後に買う（あたりくじ 20円）。Z は押さない
            _plan[ZStep] = (13.90, 'R', Vector3.Zero);
            _plan[ZStep + 1] = (14.30, 'A', Vector3.Zero);
        }
        if (OS.GetEnvironment("MONTH_NORADIO") == "1")
            _plan[1] = (1.30, 'N', Vector3.Zero);   // はんこを押さない（8/7 の日記の検査）
        if (OS.GetEnvironment("MONTH_SHELFHOLD") == "1")
        {
            // 品書きを 15:30 に開いたまま放置する。16:00 の合図と 17:00 の
            // チャイムが、開いたままの品書きをどうするかを見る
            _plan = new (double, char, Vector3)[]
            {
                (11.60, 'P', new Vector3(4f, 0.1f, 14.6f)),
                (11.65, 'A', Vector3.Zero),   // 一言
                (15.00, 'A', Vector3.Zero),   // 品書き（15:30）。以後 何も押さない
            };
        }
        if (OS.GetEnvironment("MONTH_ONLYSHOP") == "1")
        {
            // 駄菓子屋だけ。合図と押す間隔を 1.2 秒以上あけて、待ち行列を空のまま
            // 店に入り、→ の 0.4 秒後に買う。買った文がどこへ行くかを見る
            _plan = new (double, char, Vector3)[]
            {
                (11.60, 'P', new Vector3(4f, 0.1f, 14.6f)),
                (11.65, 'A', Vector3.Zero),   // 一言（13:00 の合図の 1.65 秒後）
                (14.50, 'A', Vector3.Zero),   // 品書き（15:00 の合図の 0.5 秒後・即時なので待たされない）
                (15.10, 'R', Vector3.Zero),   // → で あたりくじ
                (15.50, 'A', Vector3.Zero),   // 買う（品書きの描き直しから 0.4 秒）
                (17.00, 'P', new Vector3(-14f, 0.1f, 0f)),
            };
        }
        _main.Set("RngSeed", 2);
        _main.Set("SkipIntro", true);        // セーブは触らない
        string d = OS.GetEnvironment("MONTH_START");
        _main.Set("StartDay", d != "" ? d.ToInt() : 1);
        Root.AddChild(_main);
        _player = _main.GetNode<Node3D>("Player");
        _msg = _main.GetNode<Label>("UI/MessageLabel");
        _date = _main.GetNode<Label>("UI/DateLabel");
        _dex = _main.GetNodeOrNull<CanvasLayer>("Dex");
        _diary = _main.GetNodeOrNull<CanvasLayer>("Diary");
        _diaryText = _main.GetNodeOrNull<Label>("Diary/Paper/Text");
    }

    public override bool _Process(double delta)
    {
        _t += delta;
        _dt += delta;

        // 日付ラベルから日を読む。暗転中はラベルが更新されないので、
        // 変わった瞬間 ≒ 動けるようになった瞬間
        int day = ParseDay(_date.Text);
        if (day != _day && day > 0)
        {
            _day = day;
            _dt = 0.0;
            _step = 0;
            _dexBugLogged = false;
            GD.Print($"[day] {_date.Text}  (t={_t:F1}s)");
        }

        // その日の行動を順に実行
        while (_step < _plan.Length && _dt >= _plan[_step].At)
        {
            (double _, char kind, Vector3 pos) = _plan[_step];
            _step++;
            switch (kind)
            {
                case 'P': _player.GlobalPosition = pos; break;
                case 'A': _pressAcceptUntil = _t + 0.07; break;   // 2フレームだけ押す
                case 'Z': _pressDexUntil = _t + 0.07; break;
                case 'R': _pressRightUntil = _t + 0.07; break;
                case 'N': break;   // 何もしない
            }
        }
        // 品書きを Z で閉じたつもりで ずかんが開いたままなら、時間が止まって
        // 一日が終わらない。記録してもう一度 Z を押す
        // （IsActionJustPressed は離してから押し直さないと立たないので、
        // 離してから 0.1 秒あける）
        if (_dex != null && _dex.Visible && _t - _pressDexUntil > 0.1)
        {
            if (!_dexBugLogged)
            {
                _dexBugLogged = true;
                GD.Print($"[dex] 8月{_day}日 {_dt:F2}s | 品書きの Z で ずかんが開いた（Dex.Visible=true）→ もう一度 Z");
            }
            _pressDexUntil = _t + 0.07;
        }

        Set("ui_accept", _t < _pressAcceptUntil);
        Set("dex", _t < _pressDexUntil);
        Set("ui_right", _t < _pressRightUntil);

        if (_msg.Text != _lastMsg)
        {
            _lastMsg = _msg.Text;
            if (_lastMsg != "")
                GD.Print($"[label] {_date.Text} | {_lastMsg.Replace("\n", " / ")}");
        }
        if (_diary != null && _diary.Visible && _diaryText != null && _diaryText.Text != _lastDiary)
        {
            _lastDiary = _diaryText.Text;
            GD.Print($"[diary] {_lastDiary.Replace("\n", " / ")}");
        }
        return false;
    }

    private static int ParseDay(string label)
    {
        // "8月12日(土)  09:30  はれ"
        int a = label.IndexOf('月');
        int b = label.IndexOf('日');
        if (a < 0 || b < 0 || b <= a)
            return -1;
        return label.Substring(a + 1, b - a - 1).ToInt();
    }

    private static void Set(string action, bool on)
    {
        if (on)
            Input.ActionPress(action);
        else
            Input.ActionRelease(action);
    }
}

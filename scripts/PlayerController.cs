using Godot;

/// <summary>
/// 麦わら帽子の男の子。矢印キー（ui_left/right/up/down）でワールド基準の移動。
/// カメラは場面ごとの固定なので、操作はワールド軸基準で統一する。
/// </summary>
public partial class PlayerController : CharacterBody3D
{
    // 既定は歩き。Shift（run）で走る。
    // 4.5 m/s は時速16km の全力疾走で、のんびり眺める遊びと合わない。
    // 歩きを既定に落とし、急ぎたい時だけ走れるようにする。
    private const float WalkSpeed = 2.6f;
    private const float RunSpeed = 4.5f;
    private const float Gravity = 20.0f;

    /// <summary>日付切り替え演出中などに移動を止めるフラグ。</summary>
    public bool Frozen { get; set; }

    /// <summary>いま走っているか。虫が逃げるかどうかの判定に使う。</summary>
    public bool Running { get; private set; }

    // 歩行アニメ用。腕と脚を肩／腰から振る
    private Node3D _armL, _armR, _legL, _legR;
    private Node3D _net, _rod, _rodTip;
    private MeshInstance3D _line, _bob;   // 糸とウキ（ワールド座標に置く）
    private bool _fishing;
    private Vector3 _bobAt;               // ウキを落とす水面の一点
    private float _bobPhase;
    private float _walkPhase;
    private float _swingLeft;      // 虫あみを振っている残り時間
    private AudioStreamPlayer _step;
    private int _lastStep = -1;    // 直近で鳴らした歩数（同じ歩で二重に鳴らさない）
    private int _stepCount;
    private const float SwingTime = 0.42f;
    private const float FishHold = -8f;   // 糸を垂らしている間の右腕の角度

    public override void _Ready()
    {
        _armL = GetNodeOrNull<Node3D>("ArmL");
        _armR = GetNodeOrNull<Node3D>("ArmR");
        _legL = GetNodeOrNull<Node3D>("LegL");
        _legR = GetNodeOrNull<Node3D>("LegR");
        _net = GetNodeOrNull<Node3D>("ArmR/Net");
        _rod = GetNodeOrNull<Node3D>("ArmR/Rod");
        _rodTip = GetNodeOrNull<Node3D>("ArmR/Rod/Tip");
        BuildLineAndBob();

        var stream = GD.Load<AudioStreamWav>("res://assets/audio/sfx_step.wav");
        if (stream != null)
        {
            _step = new AudioStreamPlayer { Name = "Step", Stream = stream, VolumeDb = -7f };
            AddChild(_step);
        }
    }

    /// <summary>
    /// 速さに応じて腕と脚を前後に振る。止まると自然に振り幅が戻る。
    /// 足が動かないと「滑って移動している」ように見えるので、
    /// 見た目の説得力はここでほぼ決まる。
    /// </summary>
    private void Animate(float speed, double delta)
    {
        // moving は停止→歩きの立ち上がり、gait は歩き=1.0 / 走り≒1.7。
        // 歩幅と歩調を gait で変えないと、歩きと走りが同じ絵になる。
        float moving = Mathf.Clamp(speed / WalkSpeed, 0f, 1f);
        float gait = speed / WalkSpeed;
        _walkPhase += (float)delta * (5.2f + 2.4f * gait) * moving;
        float swing = Mathf.Sin(_walkPhase) * (26f + 10f * Mathf.Min(gait, 1.8f)) * moving;

        // 足が地面に着くのは脚の振りが端に来た瞬間＝半周ごと。
        // 位相を半周単位で数えて、変わったときだけ鳴らす
        int stepIndex = Mathf.FloorToInt(_walkPhase / Mathf.Pi);
        if (_step != null && moving > 0.15f && stepIndex != _lastStep)
        {
            _lastStep = stepIndex;
            _step.PitchScale = 0.92f + GD.Randf() * 0.16f;   // 毎回わずかに変える
            _step.Play();
            // 波形だけではセミの合唱と区別しにくいので、検査用に回数を出せるようにする
            if (OS.GetEnvironment("DEBUG_STEPS") == "1")
                GD.Print($"[step] {++_stepCount} 歩目");
        }
        if (_legL != null)
            _legL.RotationDegrees = new Vector3(swing, 0f, 0f);
        if (_legR != null)
            _legR.RotationDegrees = new Vector3(-swing, 0f, 0f);
        // 腕は脚と逆位相。歩きらしさはこの位相差から出る
        if (_armL != null)
            _armL.RotationDegrees = new Vector3(-swing * 0.75f, 0f, 0f);

        // 右腕は虫あみを持っているので、振っている間は歩きより優先する
        if (_armR == null)
            return;
        if (_swingLeft > 0f)
        {
            _swingLeft = Mathf.Max(0f, _swingLeft - (float)delta);
            float t = 1f - _swingLeft / SwingTime;          // 0→1
            // 虫あみは振りかぶって前へ抜く。竿は手首だけの短いあおりにする
            float arc = _fishing
                ? (t < 0.35f ? Mathf.Lerp(FishHold, FishHold - 34f, t / 0.35f)
                             : Mathf.Lerp(FishHold - 34f, FishHold, (t - 0.35f) / 0.65f))
                : (t < 0.3f ? Mathf.Lerp(0f, 55f, t / 0.3f)
                            : Mathf.Lerp(55f, -125f, (t - 0.3f) / 0.7f));
            _armR.RotationDegrees = new Vector3(arc, 0f, 0f);
        }
        else if (_fishing)
        {
            // 糸を垂らしている間は腕を水面へ向けたまま止める
            _armR.RotationDegrees = new Vector3(FishHold, 0f, 0f);
        }
        else
        {
            _armR.RotationDegrees = new Vector3(swing * 0.75f, 0f, 0f);
        }
    }

    /// <summary>虫あみを振る。捕獲の操作に見た目の答えを返すため。</summary>
    public void SwingNet() => _swingLeft = SwingTime;

    /// <summary>
    /// 糸とウキ。竿の先と水面の一点を結ぶので、プレイヤーの姿勢に関係なく
    /// 「水に浮いている」ように見える。TopLevel にしてワールド座標で扱う。
    /// </summary>
    private void BuildLineAndBob()
    {
        _line = new MeshInstance3D
        {
            Name = "FishLine",
            TopLevel = true,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new CylinderMesh { TopRadius = 0.005f, BottomRadius = 0.005f, Height = 1f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.95f, 0.95f, 0.92f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        AddChild(_line);

        _bob = new MeshInstance3D
        {
            Name = "FishBob",
            TopLevel = true,
            Visible = false,
            Mesh = new SphereMesh { Radius = 0.07f, Height = 0.16f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.25f, 0.2f), Roughness = 1f },
        };
        AddChild(_bob);
    }

    /// <summary>
    /// 池のふちで糸を垂らしている間は、虫あみを竿に持ち替える。
    /// target は水面のウキを落とす一点（ワールド座標）。
    /// </summary>
    public void SetFishing(bool on, Vector3 target = default)
    {
        if (on)
            _bobAt = target;
        if (_fishing == on)
            return;
        _fishing = on;
        if (_net != null)
            _net.Visible = !on;
        if (_rod != null)
            _rod.Visible = on;
        if (_line != null)
            _line.Visible = on;
        if (_bob != null)
            _bob.Visible = on;
    }

    /// <summary>竿の先とウキを毎フレーム結び直す。</summary>
    private void UpdateLine(double delta)
    {
        if (!_fishing || _line == null || _bob == null || _rodTip == null)
            return;
        _bobPhase += (float)delta * 1.6f;
        Vector3 b = _bobAt + new Vector3(0f, Mathf.Sin(_bobPhase) * 0.015f, 0f);
        _bob.GlobalPosition = b;

        Vector3 a = _rodTip.GlobalPosition;
        Vector3 d = b - a;
        float len = d.Length();
        if (len < 0.02f)
            return;
        // CylinderMesh は +Y 軸。基底の Y 列を長さぶん伸ばして、a→b を1本で張る
        Vector3 y = d / len;
        Vector3 x = y.Cross(Vector3.Forward);
        if (x.LengthSquared() < 1e-4f)
            x = y.Cross(Vector3.Right);
        x = x.Normalized();
        _line.GlobalTransform = new Transform3D(new Basis(x, y * len, x.Cross(y)), a + d * 0.5f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Frozen)
        {
            Velocity = Vector3.Zero;
            Running = false;   // 走りながら固まると true が残り、近くの虫が逃げ続ける
            Animate(0f, delta);
            return;
        }

        Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        Running = Input.IsActionPressed("run") && input.LengthSquared() > 0.01f;
        float speed = Running ? RunSpeed : WalkSpeed;
        Vector3 v = Velocity;
        v.X = input.X * speed;
        v.Z = input.Y * speed;
        v.Y = IsOnFloor() ? 0f : v.Y - Gravity * (float)delta;
        Velocity = v;
        MoveAndSlide();

        // マップ外へ出ない・公園の池に入らない
        Vector3 p = Position;
        p.X = Mathf.Clamp(p.X, -28f, 28f);
        p.Z = Mathf.Clamp(p.Z, -19.5f, 17.5f);
        var fromPond = new Vector2(p.X - 6f, p.Z + 15f);
        if (fromPond.Length() < 6.9f)
        {
            fromPond = fromPond.Normalized() * 6.9f;
            p.X = 6f + fromPond.X;
            p.Z = -15f + fromPond.Y;
        }
        Position = p;

        var dir = new Vector3(input.X, 0f, input.Y);
        if (dir.LengthSquared() > 0.01f)
            LookAt(GlobalPosition + dir, Vector3.Up);
        Animate(new Vector2(v.X, v.Z).Length(), delta);
        UpdateLine(delta);
    }
}

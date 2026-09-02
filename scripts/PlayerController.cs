using Godot;

/// <summary>
/// 麦わら帽子の男の子。矢印キー（ui_left/right/up/down）でワールド基準の移動。
/// カメラは場面ごとの固定なので、操作はワールド軸基準で統一する。
/// </summary>
public partial class PlayerController : CharacterBody3D
{
    private const float Speed = 4.5f;
    private const float Gravity = 20.0f;

    /// <summary>日付切り替え演出中などに移動を止めるフラグ。</summary>
    public bool Frozen { get; set; }

    // 歩行アニメ用。腕と脚を肩／腰から振る
    private Node3D _armL, _armR, _legL, _legR;
    private float _walkPhase;
    private float _swingLeft;      // 虫あみを振っている残り時間
    private AudioStreamPlayer _step;
    private int _lastStep = -1;    // 直近で鳴らした歩数（同じ歩で二重に鳴らさない）
    private int _stepCount;
    private const float SwingTime = 0.42f;

    public override void _Ready()
    {
        _armL = GetNodeOrNull<Node3D>("ArmL");
        _armR = GetNodeOrNull<Node3D>("ArmR");
        _legL = GetNodeOrNull<Node3D>("LegL");
        _legR = GetNodeOrNull<Node3D>("LegR");

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
        float amount = Mathf.Clamp(speed / Speed, 0f, 1f);
        _walkPhase += (float)delta * (6.0f + 4.0f * amount) * amount;
        float swing = Mathf.Sin(_walkPhase) * 34f * amount;

        // 足が地面に着くのは脚の振りが端に来た瞬間＝半周ごと。
        // 位相を半周単位で数えて、変わったときだけ鳴らす
        int stepIndex = Mathf.FloorToInt(_walkPhase / Mathf.Pi);
        if (_step != null && amount > 0.15f && stepIndex != _lastStep)
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
            // 後ろへ振りかぶってから一気に前へ抜く
            float arc = t < 0.3f
                ? Mathf.Lerp(0f, 55f, t / 0.3f)
                : Mathf.Lerp(55f, -125f, (t - 0.3f) / 0.7f);
            _armR.RotationDegrees = new Vector3(arc, 0f, 0f);
        }
        else
        {
            _armR.RotationDegrees = new Vector3(swing * 0.75f, 0f, 0f);
        }
    }

    /// <summary>虫あみを振る。捕獲の操作に見た目の答えを返すため。</summary>
    public void SwingNet() => _swingLeft = SwingTime;

    public override void _PhysicsProcess(double delta)
    {
        if (Frozen)
        {
            Velocity = Vector3.Zero;
            Animate(0f, delta);
            return;
        }

        Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        Vector3 v = Velocity;
        v.X = input.X * Speed;
        v.Z = input.Y * Speed;
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
    }
}

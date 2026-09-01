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

    public override void _Ready()
    {
        _armL = GetNodeOrNull<Node3D>("ArmL");
        _armR = GetNodeOrNull<Node3D>("ArmR");
        _legL = GetNodeOrNull<Node3D>("LegL");
        _legR = GetNodeOrNull<Node3D>("LegR");
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
        if (_legL != null)
            _legL.RotationDegrees = new Vector3(swing, 0f, 0f);
        if (_legR != null)
            _legR.RotationDegrees = new Vector3(-swing, 0f, 0f);
        // 腕は脚と逆位相。歩きらしさはこの位相差から出る
        if (_armL != null)
            _armL.RotationDegrees = new Vector3(-swing * 0.75f, 0f, 0f);
        if (_armR != null)
            _armR.RotationDegrees = new Vector3(swing * 0.75f, 0f, 0f);
    }

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

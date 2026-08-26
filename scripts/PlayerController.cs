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

    public override void _PhysicsProcess(double delta)
    {
        if (Frozen)
        {
            Velocity = Vector3.Zero;
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
    }
}

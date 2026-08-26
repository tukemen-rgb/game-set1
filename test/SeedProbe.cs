using Godot;

/// <summary>
/// キャプチャ用の RngSeed 探索。SummerMain.RespawnCicadas と同じ乱数消費順
/// （シャッフルに Randi ×9 → 最初の捕獲判定に Randf）を再現し、
/// 撮影動線が通る木 idx=4 (-4,-9) にセミが湧き、かつ捕獲成功になる seed を出す。
///   godot --headless --path . --script res://test/SeedProbe.cs
/// </summary>
public partial class SeedProbe : SceneTree
{
    public override void _Initialize()
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            var rng = new RandomNumberGenerator { Seed = (ulong)seed };
            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 10; i++)
                indices.Add(i);
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = (int)(rng.Randi() % (uint)(i + 1));
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }
            var chosen = indices.GetRange(0, 4);
            float catchRoll = rng.Randf();
            if (chosen.Contains(4))
                GD.Print($"seed={seed} chosen=[{string.Join(",", chosen)}] catchRoll={catchRoll:F2} {(catchRoll < 0.65f ? "CATCH" : "escape")}");
        }
        Quit(0);
    }
}

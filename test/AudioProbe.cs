using Godot;

/// <summary>
/// 音素材の読み込み結果を数字で出す検査。ループ長の取り違え
/// （圧縮後のバイト数をフレーム数と思い込んでいた）を二度と見逃さないため。
///   godot --headless --path . --script res://test/AudioProbe.cs
/// </summary>
public partial class AudioProbe : SceneTree
{
    public override void _Initialize()
    {
        string[] files = { "cicada_morning", "cicada_day", "cicada_evening", "rain",
                           "chime_five", "sfx_swing", "sfx_catch", "sfx_escape", "sfx_firework", "sfx_step" };
        foreach (string f in files)
        {
            var s = GD.Load<AudioStreamWav>($"res://assets/audio/{f}.wav");
            if (s == null)
            {
                GD.Print($"[audio] {f}: 読めない");
                continue;
            }
            int frames = Mathf.RoundToInt((float)(s.GetLength() * s.MixRate));
            GD.Print($"[audio] {f}: 形式={s.Format} {s.MixRate}Hz 長さ={s.GetLength():F3}s "
                   + $"フレーム={frames} Data/2={s.Data.Length / 2}");
        }
        Quit();
    }
}

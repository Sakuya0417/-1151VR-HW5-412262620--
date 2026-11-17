using UnityEngine;

/// <summary>
/// 以程式合成音效（不需外部音檔）：
/// 拋球呼嘯、命中、吸入、落地、搖晃喀啦聲、上鎖聲、收服成功音樂。
/// </summary>
public static class HW5Audio
{
    const int SR = 44100;

    delegate float Wave(float t);

    static AudioClip Make(string name, float dur, Wave f)
    {
        int n = Mathf.CeilToInt(SR * dur);
        var data = new float[n];
        for (int i = 0; i < n; i++)
            data[i] = Mathf.Clamp(f(i / (float)SR), -1f, 1f);
        var clip = AudioClip.Create(name, n, 1, SR, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

    /// <summary>拋球時的呼嘯聲（濾波雜訊，音量先升後降）。</summary>
    public static AudioClip Whoosh()
    {
        var r = new System.Random(11);
        float lp = 0f;
        const float dur = 0.5f;
        return Make("SFX_Whoosh", dur, t =>
        {
            float env = Mathf.Sin(Mathf.PI * t / dur);
            float k = Mathf.Lerp(0.03f, 0.3f, env);
            lp += (Noise(r) - lp) * k;
            return lp * env * 1.6f;
        });
    }

    /// <summary>命中時的「啵」聲（快速下滑的音高）。</summary>
    public static AudioClip Pop()
    {
        float phase = 0f;
        const float dur = 0.18f;
        return Make("SFX_Pop", dur, t =>
        {
            float freq = Mathf.Lerp(900f, 180f, t / dur);
            phase += 2f * Mathf.PI * freq / SR;
            return Mathf.Sin(phase) * Mathf.Exp(-t * 22f) * 0.9f;
        });
    }

    /// <summary>紅光吸入的上升音效。</summary>
    public static AudioClip Absorb()
    {
        float phase = 0f;
        const float dur = 0.9f;
        return Make("SFX_Absorb", dur, t =>
        {
            float k = t / dur;
            float freq = Mathf.Lerp(250f, 1400f, k * k) * (1f + 0.03f * Mathf.Sin(t * 60f));
            phase += 2f * Mathf.PI * freq / SR;
            float env = Mathf.Sin(Mathf.PI * k) * 0.45f;
            return (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f)) * env;
        });
    }

    /// <summary>球落地的「咚」聲。</summary>
    public static AudioClip Thud()
    {
        var r = new System.Random(3);
        const float dur = 0.25f;
        return Make("SFX_Thud", dur, t =>
            (Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.9f + Noise(r) * 0.25f) * Mathf.Exp(-t * 18f));
    }

    /// <summary>精靈球搖晃時的「喀啦—喀」聲（兩下塑膠碰撞＋低音）。</summary>
    public static AudioClip Wobble()
    {
        var r = new System.Random(7);
        const float dur = 0.35f;
        return Make("SFX_Wobble", dur, t =>
        {
            float s = 0f;
            foreach (float start in new[] { 0f, 0.14f })
            {
                float u = t - start;
                if (u < 0f) continue;
                s += Noise(r) * Mathf.Exp(-u * 90f) * 0.8f;                              // 喀
                s += Mathf.Sin(2f * Mathf.PI * 170f * u) * Mathf.Exp(-u * 30f) * 0.6f;    // 低音碰撞
                s += Mathf.Sin(2f * Mathf.PI * 1250f * u) * Mathf.Exp(-u * 60f) * 0.25f;  // 外殼共鳴
            }
            return s;
        });
    }

    /// <summary>收服成功瞬間的上鎖「喀嚓」聲。</summary>
    public static AudioClip Lock()
    {
        var r = new System.Random(9);
        const float dur = 0.2f;
        return Make("SFX_Lock", dur, t =>
            Noise(r) * Mathf.Exp(-t * 120f) * 0.9f
            + Mathf.Sin(2f * Mathf.PI * 2200f * t) * Mathf.Exp(-t * 40f) * 0.35f);
    }

    /// <summary>收服成功的勝利音樂（上行琶音＋和弦）。</summary>
    public static AudioClip Success()
    {
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };  // C5 E5 G5 C6
        const float step = 0.13f;
        const float dur = 1.6f;
        return Make("SFX_Success", dur, t =>
        {
            float s = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float u = t - i * step;
                if (u < 0f) continue;
                float hold = i == notes.Length - 1 ? 3f : 9f;
                s += Tone(notes[i], u) * Mathf.Exp(-u * hold) * 0.32f;
            }
            float c = t - notes.Length * step;                      // 最後的大和弦
            if (c > 0f)
            {
                float env = Mathf.Min(1f, c * 30f) * Mathf.Exp(-c * 2.2f) * 0.22f;
                s += (Tone(523.25f, c) + Tone(659.25f, c) + Tone(783.99f, c)) * env;
            }
            return s;
        });
    }

    // 帶一點方波感的電子音色
    static float Tone(float f, float t)
    {
        float x = Mathf.Sin(2f * Mathf.PI * f * t);
        return x * 0.7f + Mathf.Sign(x) * 0.15f + Mathf.Sin(4f * Mathf.PI * f * t) * 0.15f;
    }
}

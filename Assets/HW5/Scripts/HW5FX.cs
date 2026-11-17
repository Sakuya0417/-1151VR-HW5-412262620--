using UnityEngine;

/// <summary>
/// 以程式碼即時產生粒子特效（不需要任何外部特效素材）。
/// </summary>
public static class HW5FX
{
    static Texture2D _soft, _sparkle;

    public static Texture2D SoftTex { get { if (_soft == null) _soft = MakeSoft(64); return _soft; } }
    public static Texture2D SparkleTex { get { if (_sparkle == null) _sparkle = MakeSparkle(64); return _sparkle; } }

    /// <summary>建立加算混合 (Additive) 的發光材質。</summary>
    public static Material MakeMaterial(Texture2D tex)
    {
        Shader s = Shader.Find("Legacy Shaders/Particles/Additive");
        if (s == null) s = Shader.Find("Mobile/Particles/Additive");
        if (s == null) s = Shader.Find("Sprites/Default");
        var m = new Material(s);
        m.mainTexture = tex;
        return m;
    }

    /// <summary>球狀爆散粒子。</summary>
    public static ParticleSystem Burst(Vector3 pos, Color a, Color b, int count, float speed, float size,
                                       float life, Material mat, float gravity = 0f)
    {
        var go = new GameObject("FX_Burst");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.05f;

        ApplyFade(ps);
        go.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        return ps;
    }

    /// <summary>朝鏡頭擴散的光環衝擊波。</summary>
    public static ParticleSystem Ring(Vector3 pos, Color c, int count, float speed, Material mat)
    {
        var go = new GameObject("FX_Ring");
        go.transform.position = pos;
        if (Camera.main != null) go.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.45f;
        main.startSpeed = speed;
        main.startSize = 0.18f;
        main.startColor = c;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = 0.1f;
        sh.radiusThickness = 0f;

        ApplyFade(ps);
        go.GetComponent<ParticleSystemRenderer>().material = mat;
        ps.Play();
        return ps;
    }

    static void ApplyFade(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
    }

    static Texture2D MakeSoft(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        t.Apply();
        return t;
    }

    static Texture2D MakeSparkle(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.Pow(Mathf.Clamp01(1f - r * 2.2f), 2f);
                float h = Mathf.Exp(-Mathf.Abs(dy) * 28f) * Mathf.Clamp01(1f - Mathf.Abs(dx));
                float v = Mathf.Exp(-Mathf.Abs(dx) * 28f) * Mathf.Clamp01(1f - Mathf.Abs(dy));
                float a = Mathf.Clamp01(Mathf.Max(core, Mathf.Max(h, v)));
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }
}

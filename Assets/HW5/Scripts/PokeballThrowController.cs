using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主控制器：訓練家拋球 → 精靈球由左至右拋物線飛行 → 命中 → 紅光吸入寶可夢 →
/// 球落地搖晃 3 次 → 收服成功特效（星星、光環、Gotcha! 字樣）。
/// 操作：Space 拋球、R 重置、F12 截圖。
/// </summary>
[DefaultExecutionOrder(100)]
public class PokeballThrowController : MonoBehaviour
{
    [Header("場景物件")]
    public TrainerThrowAnimator trainer;
    public Transform ball;
    public Transform pokemon;

    [Header("飛行")]
    public float flightTime = 1.0f;
    public float arcHeight = 2.2f;
    public float spinSpeed = 900f;

    [Header("收服")]
    public int wobbleCount = 3;
    public Color captureColor = new Color(1f, 0.15f, 0.1f);

    [Header("播放")]
    public bool autoPlay = true;
    public float autoPlayDelay = 1.5f;
    [Tooltip("錄影用：收服完成後自動重置並再播一次")]
    public bool loopAutoPlay = false;

    enum State { Idle, Throwing, Capturing, Done }
    State state = State.Idle;
    bool holdInHand = true;

    Vector3 pokemonBasePos, pokemonBaseScale;
    Quaternion pokemonBaseRot, ballBaseRot;
    float ballRadius = 0.15f;
    TrailRenderer trail;
    LineRenderer beam;
    Material fxSoft, fxSparkle;
    WildPokemonIdle idle;
    string banner;
    float bannerUntil;
    GUIStyle bannerStyle, hintStyle;

    [Header("握球位置（相對訓練家：x 右、y 上、z 前，公尺）")]
    public Vector3 holdOffset = new Vector3(0f, 0.03f, 0.09f);
    bool holdCalibrated;
    Vector3 holdLocal;

    [Header("音效")]
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    AudioSource sfx;
    AudioClip sWhoosh, sPop, sAbsorb, sThud, sWobble, sLock, sSuccess;

    class MatSave { public Material m; public bool hasColor, hasBase, hadEmission; public Color color, baseColor, emission; }
    readonly List<MatSave> mats = new List<MatSave>();

    void Start()
    {
#if UNITY_2023_1_OR_NEWER
        if (trainer == null) trainer = FindAnyObjectByType<TrainerThrowAnimator>();
#else
        if (trainer == null) trainer = FindObjectOfType<TrainerThrowAnimator>();
#endif
        fxSoft = HW5FX.MakeMaterial(HW5FX.SoftTex);
        fxSparkle = HW5FX.MakeMaterial(HW5FX.SparkleTex);

        var b = GetBounds(ball);
        ballRadius = Mathf.Max(0.05f, Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)));
        ballBaseRot = ball.rotation;

        pokemonBasePos = pokemon.position;
        pokemonBaseRot = pokemon.rotation;
        pokemonBaseScale = pokemon.localScale;
        idle = pokemon.GetComponent<WildPokemonIdle>();

        trail = ball.GetComponent<TrailRenderer>();
        if (trail == null) trail = ball.gameObject.AddComponent<TrailRenderer>();
        trail.material = fxSoft;
        trail.time = 0.3f;
        trail.minVertexDistance = 0.05f;
        trail.widthCurve = AnimationCurve.Linear(0f, ballRadius * 1.6f, 1f, 0f);
        var tg = new Gradient();
        tg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.6f, 0.5f), 1f) },
                   new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = tg;
        trail.emitting = false;

        var beamGO = new GameObject("CaptureBeam");
        beam = beamGO.AddComponent<LineRenderer>();
        beam.material = fxSoft;
        beam.positionCount = 2;
        beam.widthMultiplier = 0.25f;
        beam.startColor = beam.endColor = captureColor;
        beam.enabled = false;

        SaveMaterials();

        // 音效（程式合成）
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        sWhoosh = HW5Audio.Whoosh();
        sPop = HW5Audio.Pop();
        sAbsorb = HW5Audio.Absorb();
        sThud = HW5Audio.Thud();
        sWobble = HW5Audio.Wobble();
        sLock = HW5Audio.Lock();
        sSuccess = HW5Audio.Success();

        if (autoPlay) StartCoroutine(AutoPlay());
    }

    IEnumerator AutoPlay()
    {
        yield return new WaitForSeconds(autoPlayDelay);
        if (state == State.Idle) StartCoroutine(ThrowSequence());
    }

    void Update()
    {
        if (HW5Input.GetKeyDown(KeyCode.Space) && state == State.Idle) StartCoroutine(ThrowSequence());
        if (HW5Input.GetKeyDown(KeyCode.R)) ResetAll();
    }

    void LateUpdate()
    {
        // 在手臂動畫（TrainerThrowAnimator.LateUpdate）之後，讓球黏在手上
        if (holdInHand && trainer != null && trainer.rightHand != null)
        {
            Transform hand = trainer.rightHand;
            if (!holdCalibrated)
            {
                // 第一次：以「手臂自然下垂」的姿勢決定球在手掌中的位置，
                // 再換算成手骨骼的區域座標，之後揮臂時球會跟著手掌一起轉。
                Vector3 armDir = trainer.rightForeArm != null
                    ? (hand.position - trainer.rightForeArm.position).normalized
                    : -trainer.transform.up;
                Vector3 desired = armDir * 0.06f
                                + trainer.transform.rotation * holdOffset
                                + trainer.transform.right * (ballRadius * 0.5f);
                holdLocal = Quaternion.Inverse(hand.rotation) * desired;
                holdCalibrated = true;
            }
            ball.position = hand.position + hand.rotation * holdLocal;
        }
    }

    IEnumerator ThrowSequence()
    {
        state = State.Throwing;
        holdInHand = true;
        ball.gameObject.SetActive(true);

        // ── 1. 訓練家做出拋球動作，到出手時間點放開球 ──
        trainer.PlayThrow();
        yield return new WaitForSeconds(trainer.ReleaseDelay);
        holdInHand = false;
        Play(sWhoosh);

        // ── 2. 由左至右拋物線飛行 ──
        Vector3 start = ball.position;
        Vector3 target = PokemonCenter();
        trail.Clear();
        trail.emitting = true;
        float t = 0f;
        while (t < flightTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / flightTime);
            Vector3 p = Vector3.Lerp(start, target, k);
            p.y += arcHeight * 4f * k * (1f - k);
            ball.position = p;
            ball.Rotate(Vector3.forward, -spinSpeed * Time.deltaTime, Space.World);
            yield return null;
        }
        trail.emitting = false;

        // ── 3. 命中：白色閃光 + 光環 ──
        state = State.Capturing;
        if (idle != null) idle.enabled = false;
        HW5FX.Burst(target, Color.white, new Color(1f, 0.9f, 0.5f), 40, 4f, 0.3f, 0.5f, fxSoft);
        HW5FX.Ring(target, Color.white, 60, 6f, fxSoft);
        Play(sPop);

        // 球彈回半空
        Vector3 hover = target + new Vector3(-0.9f, 0.9f, 0f);
        yield return MoveArc(target, hover, 0.35f, 0.3f);
        ball.rotation = ballBaseRot;

        // ── 4. 紅色光束把寶可夢吸進球裡 ──
        beam.enabled = true;
        Play(sAbsorb);
        Vector3 pStart = pokemon.position;
        float dur = 0.9f;
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            SetTint(Mathf.Clamp01(k * 2.5f));
            float s = Smooth(Mathf.Clamp01((k - 0.3f) / 0.7f));
            pokemon.localScale = pokemonBaseScale * Mathf.Lerp(1f, 0.02f, s);
            pokemon.position = Vector3.Lerp(pStart, hover, s);
            beam.SetPosition(0, ball.position);
            beam.SetPosition(1, PokemonCenter());
            beam.widthMultiplier = 0.25f * (1f - 0.6f * k);
            yield return null;
        }
        beam.enabled = false;
        pokemon.gameObject.SetActive(false);
        HW5FX.Burst(hover, captureColor, Color.white, 30, 2f, 0.25f, 0.45f, fxSoft);

        // ── 5. 球落地 ──
        Vector3 rest = new Vector3(hover.x, GroundHeight(hover) + ballRadius, hover.z);
        yield return Fall(hover, rest);

        // ── 6. 搖晃 3 次 ──
        for (int i = 0; i < wobbleCount; i++)
        {
            yield return new WaitForSeconds(0.45f);
            Play(sWobble);
            yield return Wobble(0.45f, 28f);
            HW5FX.Burst(rest + Vector3.up * ballRadius, Color.white, new Color(1f, 1f, 0.6f), 8, 1f, 0.15f, 0.4f, fxSparkle);
        }
        yield return new WaitForSeconds(0.4f);

        // ── 7. 收服成功特效 ──
        Play(sLock);
        Play(sSuccess, 0.9f);
        HW5FX.Ring(rest, new Color(1f, 0.95f, 0.5f), 80, 5f, fxSoft);
        HW5FX.Burst(rest, new Color(1f, 0.85f, 0.2f), Color.white, 60, 3.5f, 0.4f, 1.2f, fxSparkle, 0.3f);
        StartCoroutine(StarFountain(rest, 1.6f));
        banner = "Gotcha!\n收服成功！";
        bannerUntil = Time.time + 3.5f;
        state = State.Done;

        if (loopAutoPlay)
        {
            yield return new WaitForSeconds(4f);
            ResetAll();
            StartCoroutine(AutoPlay());
        }
    }

    IEnumerator MoveArc(Vector3 a, Vector3 b, float dur, float h)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            Vector3 p = Vector3.Lerp(a, b, Smooth(k));
            p.y += h * 4f * k * (1f - k);
            ball.position = p;
            ball.Rotate(Vector3.forward, spinSpeed * 0.5f * Time.deltaTime, Space.World);
            yield return null;
        }
    }

    IEnumerator Fall(Vector3 from, Vector3 to)
    {
        float t = 0f, dur = 0.45f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            ball.position = Vector3.Lerp(from, to, k * k);
            yield return null;
        }
        Play(sThud);
        // 小彈跳
        t = 0f; dur = 0.3f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            ball.position = to + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.18f;
            yield return null;
        }
        ball.position = to;
    }

    /// <summary>錄影工具用：每次播放音效時通知（clip, 音量）。</summary>
    public static event System.Action<AudioClip, float> SfxPlayed;
    public bool IsDone => state == State.Done;

    void Play(AudioClip clip, float vol = 1f)
    {
        if (sfx != null && clip != null) sfx.PlayOneShot(clip, vol * sfxVolume);
        SfxPlayed?.Invoke(clip, vol * sfxVolume);
    }

    IEnumerator Wobble(float dur, float amp)
    {
        Quaternion baseRot = ball.rotation;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            ball.rotation = Quaternion.AngleAxis(Mathf.Sin(k * Mathf.PI * 2f) * amp, Vector3.forward) * baseRot;
            yield return null;
        }
        ball.rotation = baseRot;
    }

    IEnumerator StarFountain(Vector3 pos, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            HW5FX.Burst(pos + Vector3.up * 0.3f, new Color(1f, 0.9f, 0.3f), new Color(1f, 1f, 0.8f), 6, 2.5f, 0.3f, 0.9f, fxSparkle, 0.5f);
            t += 0.12f;
            yield return new WaitForSeconds(0.12f);
        }
    }

    public void ResetAll()
    {
        StopAllCoroutines();
        state = State.Idle;
        holdInHand = true;
        pokemon.gameObject.SetActive(true);
        pokemon.position = pokemonBasePos;
        pokemon.rotation = pokemonBaseRot;
        pokemon.localScale = pokemonBaseScale;
        SetTint(0f, true);
        if (idle != null) idle.enabled = true;
        ball.gameObject.SetActive(true);
        ball.rotation = ballBaseRot;
        beam.enabled = false;
        trail.emitting = false;
        trail.Clear();
        banner = null;
        if (trainer != null) trainer.ResetPose();
    }

    // ───────── 工具函式 ─────────

    static float Smooth(float k) => k * k * (3f - 2f * k);

    Vector3 PokemonCenter()
    {
        var b = GetBounds(pokemon);
        return b.size.sqrMagnitude > 0f ? b.center : pokemon.position + Vector3.up * 0.5f;
    }

    static Bounds GetBounds(Transform root)
    {
        var rs = root.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(root.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    float GroundHeight(Vector3 p)
    {
        var terrain = Terrain.activeTerrain;
        if (terrain != null) return terrain.SampleHeight(p) + terrain.transform.position.y;
        if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out var hit, 200f)) return hit.point.y;
        return 0f;
    }

    void SaveMaterials()
    {
        mats.Clear();
        foreach (var r in pokemon.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var m in r.materials)
            {
                var s = new MatSave { m = m };
                s.hasColor = m.HasProperty("_Color");
                s.hasBase = m.HasProperty("_BaseColor");
                if (s.hasColor) s.color = m.GetColor("_Color");
                if (s.hasBase) s.baseColor = m.GetColor("_BaseColor");
                s.hadEmission = m.IsKeywordEnabled("_EMISSION");
                if (m.HasProperty("_EmissionColor")) s.emission = m.GetColor("_EmissionColor");
                mats.Add(s);
            }
        }
    }

    void SetTint(float k, bool restore = false)
    {
        foreach (var s in mats)
        {
            if (s.m == null) continue;
            if (s.hasColor) s.m.SetColor("_Color", Color.Lerp(s.color, captureColor, k));
            if (s.hasBase) s.m.SetColor("_BaseColor", Color.Lerp(s.baseColor, captureColor, k));
            if (s.m.HasProperty("_EmissionColor"))
            {
                if (restore || k <= 0f)
                {
                    s.m.SetColor("_EmissionColor", s.emission);
                    if (!s.hadEmission) s.m.DisableKeyword("_EMISSION");
                }
                else
                {
                    s.m.EnableKeyword("_EMISSION");
                    s.m.SetColor("_EmissionColor", captureColor * (k * 2f));
                }
            }
        }
    }

    void OnGUI()
    {
        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 40) };
            hintStyle.normal.textColor = Color.white;
            bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(32, Screen.height / 9),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
        GUI.Label(new Rect(16, Screen.height - 40, 600, 30), "Space：拋球　R：重置　F12：截圖", hintStyle);

        if (!string.IsNullOrEmpty(banner) && Time.time < bannerUntil)
        {
            float pop = Mathf.Clamp01((bannerUntil - Time.time) / 3.5f);
            var rect = new Rect(0, Screen.height * 0.12f, Screen.width, Screen.height * 0.35f);
            bannerStyle.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(rect.x + 4, rect.y + 4, rect.width, rect.height), banner, bannerStyle);
            bannerStyle.normal.textColor = Color.Lerp(new Color(1f, 0.85f, 0.1f), Color.white, 1f - pop);
            GUI.Label(rect, banner, bannerStyle);
        }
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// 訓練家拋球動作。
/// 預設以「程序化動畫」直接轉動右上臂與前臂骨骼（任何 Humanoid 模型或替代模型都可用）；
/// 若模型自帶拋球動畫 (例如 Mixamo 的 Throw)，可勾選 useAnimatorTrigger 改用 Animator。
/// 手臂角度定義：0° = 手臂自然下垂，90° = 向後，180° = 舉過頭頂，270° = 向前（朝寶可夢）。
/// </summary>
[DefaultExecutionOrder(50)]
public class TrainerThrowAnimator : MonoBehaviour
{
    [Header("骨骼 / 手臂樞紐（Humanoid 模型可留空，自動抓取）")]
    public Transform rightUpperArm;
    public Transform rightForeArm;
    public Transform rightHand;
    public Transform leftUpperArm;
    public Transform leftForeArm;

    [Header("若模型自帶拋球動畫，可改用 Animator Trigger")]
    public Animator animator;
    public bool useAnimatorTrigger = false;
    public string throwTriggerName = "Throw";
    public float animatorReleaseTime = 0.9f;
    public float animatorTotalTime = 2.0f;

    [Header("程序化拋球動作（秒）")]
    public float windupTime = 0.5f;
    public float throwTime = 0.28f;
    public float followTime = 0.45f;
    public float recoverTime = 0.5f;
    [Range(0f, 1f)] public float releaseFraction = 0.8f;
    [Tooltip("待機時也讓手臂自然下垂（T-Pose 模型建議開啟）")]
    public bool driveArmsWhenIdle = true;

    float armAngle;
    float elbowBend;
    bool throwing;

    public bool IsThrowing => throwing;
    public float ReleaseDelay => UsingAnimator ? animatorReleaseTime : windupTime + throwTime * releaseFraction;
    bool UsingAnimator => useAnimatorTrigger && animator != null && animator.runtimeAnimatorController != null;

    void Awake() { AutoSetup(); }

    /// <summary>自動尋找手臂骨骼：先試 Humanoid Avatar，再以名稱搜尋（支援 Mixamo 命名）。</summary>
    public void AutoSetup()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.avatar != null && animator.isHuman)
        {
            if (rightUpperArm == null) rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (rightForeArm == null) rightForeArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (rightHand == null) rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (leftUpperArm == null) leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (leftForeArm == null) leftForeArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        }
        if (rightUpperArm == null) rightUpperArm = FindByName("rightarm", "upperarm_r", "r_upperarm", "rightupperarm", "upperarm.r");
        if (rightForeArm == null) rightForeArm = FindByName("rightforearm", "lowerarm_r", "r_forearm", "rightlowerarm", "forearm.r");
        if (rightHand == null) rightHand = FindByName("righthand", "hand_r", "r_hand", "hand.r");
        if (leftUpperArm == null) leftUpperArm = FindByName("leftarm", "upperarm_l", "l_upperarm", "leftupperarm", "upperarm.l");
        if (leftForeArm == null) leftForeArm = FindByName("leftforearm", "lowerarm_l", "l_forearm", "leftlowerarm", "forearm.l");
    }

    Transform FindByName(params string[] keys)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant().Replace("mixamorig:", "").Replace(" ", "");
            foreach (var k in keys)
                if (n == k || n.EndsWith(k)) return t;
        }
        return null;
    }

    public void PlayThrow()
    {
        StopAllCoroutines();
        if (UsingAnimator)
        {
            animator.SetTrigger(throwTriggerName);
            StartCoroutine(FlagFor(animatorTotalTime));
            return;
        }
        StartCoroutine(ProceduralThrow());
    }

    public void ResetPose()
    {
        StopAllCoroutines();
        armAngle = 0f;
        elbowBend = 0f;
        throwing = false;
    }

    IEnumerator FlagFor(float t)
    {
        throwing = true;
        yield return new WaitForSeconds(t);
        throwing = false;
    }

    IEnumerator ProceduralThrow()
    {
        throwing = true;
        // 1. 舉手後拉（蓄力）：上臂往後上方，前臂彎曲
        yield return Phase(windupTime, 0f, 100f, 0f, 80f, 0);
        // 2. 揮臂：由後往上再往前加速甩出，手肘打直
        yield return Phase(throwTime, 100f, 280f, 80f, 0f, 1);
        // 3. 隨球動作（follow-through）
        yield return Phase(followTime, 280f, 330f, 0f, 20f, 2);
        // 4. 回到自然下垂
        yield return Phase(recoverTime, 330f, 360f, 20f, 0f, 0);
        armAngle = 0f;
        elbowBend = 0f;
        throwing = false;
    }

    IEnumerator Phase(float dur, float a0, float a1, float b0, float b1, int ease)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Ease(Mathf.Clamp01(t / dur), ease);
            armAngle = Mathf.Lerp(a0, a1, k);
            elbowBend = Mathf.Lerp(b0, b1, k);
            yield return null;
        }
    }

    static float Ease(float k, int mode)
    {
        switch (mode)
        {
            case 1: return k * k;                              // ease-in（加速）
            case 2: return 1f - (1f - k) * (1f - k);           // ease-out（減速）
            default: return k * k * (3f - 2f * k);             // smoothstep
        }
    }

    void LateUpdate()
    {
        if (UsingAnimator) return;
        if (!throwing && !driveArmsWhenIdle) return;

        Vector3 r = transform.right;
        Vector3 u = transform.up;

        if (rightUpperArm != null && rightForeArm != null)
        {
            Vector3 baseDir = (-u + 0.18f * r).normalized;
            Aim(rightUpperArm, rightForeArm.position, Quaternion.AngleAxis(armAngle, r) * baseDir);
            if (rightHand != null)
                Aim(rightForeArm, rightHand.position, Quaternion.AngleAxis(armAngle + elbowBend, r) * baseDir);
        }
        if (leftUpperArm != null && leftForeArm != null)
        {
            Aim(leftUpperArm, leftForeArm.position, (-u - 0.18f * r).normalized);
        }
    }

    /// <summary>旋轉骨骼，使「骨骼 → 子骨骼」的方向對準 desiredDir。</summary>
    static void Aim(Transform bone, Vector3 childPos, Vector3 desiredDir)
    {
        Vector3 cur = childPos - bone.position;
        if (cur.sqrMagnitude < 1e-8f) return;
        bone.rotation = Quaternion.FromToRotation(cur.normalized, desiredDir.normalized) * bone.rotation;
    }
}

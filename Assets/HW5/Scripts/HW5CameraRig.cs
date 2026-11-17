using UnityEngine;

/// <summary>側面鏡頭，會輕微跟隨飛行中的精靈球，讓畫面更有動態感。</summary>
[DefaultExecutionOrder(200)]
public class HW5CameraRig : MonoBehaviour
{
    public Transform ball;
    public Vector3 lookAt = new Vector3(0f, 1.0f, 0f);
    [Range(0f, 1f)] public float followFactor = 0.3f;
    public float smooth = 3f;

    Vector3 basePos;
    float offsetX;

    void Start() { basePos = transform.position; }

    void LateUpdate()
    {
        float target = ball != null && ball.gameObject.activeInHierarchy ? ball.position.x * followFactor : 0f;
        offsetX = Mathf.Lerp(offsetX, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        transform.position = basePos + Vector3.right * offsetX;
        transform.LookAt(lookAt + Vector3.right * offsetX);
    }
}

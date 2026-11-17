using UnityEngine;

/// <summary>野生寶可夢待機：原地輕跳與左右張望。收服時會被控制器停用。</summary>
public class WildPokemonIdle : MonoBehaviour
{
    public float hopHeight = 0.12f;
    public float hopSpeed = 2.2f;
    public float lookAngle = 12f;

    Vector3 basePos;
    Quaternion baseRot;

    void OnEnable()
    {
        basePos = transform.position;
        baseRot = transform.rotation;
    }

    void Update()
    {
        float t = Time.time * hopSpeed;
        transform.position = basePos + Vector3.up * Mathf.Abs(Mathf.Sin(t)) * hopHeight;
        transform.rotation = baseRot * Quaternion.Euler(0f, Mathf.Sin(t * 0.37f) * lookAngle, 0f);
    }

    void OnDisable()
    {
        transform.position = basePos;
        transform.rotation = baseRot;
    }
}

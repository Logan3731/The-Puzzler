using UnityEngine;

/// <summary>마법진처럼 커지면서 사라지는 납작한 고리. 동료가 공격을 쏠 때 발사 지점에 띄운다.</summary>
public class FxRing : MonoBehaviour
{
    float age, life, size;

    public static void Spawn(Vector3 position, Color color, float size = 1.4f, float life = 0.35f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(go.GetComponent<Collider>());
        go.name = "FxRing";
        go.GetComponent<Renderer>().sharedMaterial = MaterialUtil.Lit(color, 2f);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
        go.transform.localScale = Vector3.zero;
        var ring = go.AddComponent<FxRing>();
        ring.life = life;
        ring.size = size;
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(age / life);
        float s = size * (k < 0.4f ? Mathf.Lerp(0f, 1.15f, k / 0.4f) : Mathf.Lerp(1.15f, 0.9f, (k - 0.4f) / 0.6f));
        float thickness = 0.05f * (1f - k);
        transform.localScale = new Vector3(s, thickness, s);
        transform.Rotate(0f, 360f * Time.unscaledDeltaTime, 0f, Space.Self);
        if (age >= life) Destroy(gameObject);
    }
}

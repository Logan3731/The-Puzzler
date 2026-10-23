using UnityEngine;

/// <summary>줄 파괴 블록이 터질 때 한 줄을 가로지르는 빛줄기. 두께가 줄어들며 사라진다.</summary>
public class FxBeam : MonoBehaviour
{
    float age, life, thickness;
    bool horizontal;

    public static void Spawn(Vector3 center, float length, bool horizontal, Color color, float thickness = 0.7f, float life = 0.25f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.name = "FxBeam";
        go.GetComponent<Renderer>().sharedMaterial = MaterialUtil.Lit(color, 3f);
        go.transform.position = center + Vector3.back * 0.8f;
        go.transform.localScale = horizontal ? new Vector3(length, thickness, 0.05f) : new Vector3(thickness, length, 0.05f);
        var beam = go.AddComponent<FxBeam>();
        beam.life = life;
        beam.thickness = thickness;
        beam.horizontal = horizontal;
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        float k = 1f - Mathf.Clamp01(age / life);
        var s = transform.localScale;
        if (horizontal) s.y = thickness * k;
        else s.x = thickness * k;
        transform.localScale = s;
        if (age >= life) Destroy(gameObject);
    }
}

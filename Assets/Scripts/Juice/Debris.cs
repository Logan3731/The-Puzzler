using UnityEngine;

/// <summary>블록이 터질 때 사방으로 튀는 작은 파편.</summary>
public class Debris : MonoBehaviour
{
    Vector3 velocity, spin, baseScale;
    float age, life;

    public static void Burst(Vector3 position, Color color, int count, float force, float size = 0.16f)
    {
        var mat = MaterialUtil.Lit(color, 0.5f);
        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.name = "Debris";
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.transform.position = position + Vector3.back * 0.5f;
            go.transform.rotation = Random.rotation;
            go.transform.localScale = Vector3.one * size * Random.Range(0.6f, 1.3f);

            var d = go.AddComponent<Debris>();
            Vector2 dir = Random.insideUnitCircle.normalized;
            d.velocity = new Vector3(dir.x, dir.y + 0.6f, -Random.Range(0f, 1f)) * force * Random.Range(0.5f, 1f);
            d.spin = Random.insideUnitSphere * 720f;
            d.life = Random.Range(0.35f, 0.65f);
            d.baseScale = go.transform.localScale;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        velocity += Vector3.down * 25f * dt;
        transform.position += velocity * dt;
        transform.Rotate(spin * dt);
        transform.localScale = baseScale * Mathf.Clamp01(1f - age / life);
        if (age >= life) Destroy(gameObject);
    }
}

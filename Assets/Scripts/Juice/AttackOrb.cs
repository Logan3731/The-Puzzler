using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>터진 블록에서 튀어나와 휘어 날아가 적에게 꽂히는 빛구슬. 도착하면 콜백을 부른다.</summary>
public class AttackOrb : MonoBehaviour
{
    Enemy target;
    Vector3 from;
    float bend, duration, delay, t;
    Color color;
    Action onArrive;

    public static void Spawn(Vector3 from, Enemy target, Color color, float delay, Action onArrive)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.name = "AttackOrb";
        go.GetComponent<Renderer>().sharedMaterial = MaterialUtil.Lit(color, 3f);
        go.transform.position = from + Vector3.back;
        go.transform.localScale = Vector3.one * 0.4f;
        Fx.AddTrail(go.transform, Vector3.zero, 0.35f, 0.18f, Color.white, color);

        var orb = go.AddComponent<AttackOrb>();
        orb.target = target;
        orb.from = go.transform.position;
        orb.color = color;
        orb.delay = delay;
        orb.onArrive = onArrive;
        orb.bend = Random.Range(2f, 4f) * (Random.value < 0.5f ? -1f : 1f);
        orb.duration = Random.Range(0.3f, 0.4f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (delay > 0f)
        {
            delay -= dt;
            return;
        }

        t += dt;
        float k = Mathf.Clamp01(t / duration);
        Vector3 to = target != null ? target.HitPoint + Vector3.back : from + Vector3.up * 5f;
        transform.position = Fx.Curve(from, to, bend, k * k);

        if (k < 1f) return;
        Debris.Burst(transform.position, color, 4, 5f, 0.12f);
        onArrive?.Invoke();
        Destroy(gameObject);
    }
}

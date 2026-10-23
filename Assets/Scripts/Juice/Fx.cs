using UnityEngine;

/// <summary>여러 연출에서 같이 쓰는 이펙트 헬퍼.</summary>
public static class Fx
{
    public static TrailRenderer AddTrail(Transform parent, Vector3 localPos, float width, float time, Color head, Color tail)
    {
        var go = new GameObject("Trail");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = MaterialUtil.SpriteDefault;
        trail.time = time;
        trail.widthMultiplier = width;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(head, 0f), new GradientColorKey(tail, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) },
        };
        return trail;
    }

    /// <summary>a→b를 옆으로 휘어지는 2차 베지어 곡선으로 보간.</summary>
    public static Vector3 Curve(Vector3 a, Vector3 b, float bend, float t)
    {
        Vector3 dir = b - a;
        Vector3 side = new Vector3(-dir.y, dir.x, 0f).normalized;
        Vector3 control = (a + b) * 0.5f + side * bend;
        float u = 1f - t;
        return u * u * a + 2f * u * t * control + t * t * b;
    }
}

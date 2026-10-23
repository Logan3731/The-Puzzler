using UnityEngine;

/// <summary>카메라 흔들림 + 줌 펀치. 히트스톱 중에도 움직이도록 unscaled 시간을 쓴다.</summary>
public class CameraShake : MonoBehaviour
{
    Camera cam;
    Vector3 basePos;
    float amplitude, duration, elapsed;
    bool shaking;
    float zoom, baseSize;

    float Remaining => shaking ? amplitude * (1f - elapsed / duration) : 0f;

    void Awake() => cam = GetComponent<Camera>();

    public void Shake(float amp, float dur)
    {
        if (amp < Remaining) return; // 더 센 흔들림이 진행 중이면 유지
        if (!shaking) basePos = transform.localPosition;
        amplitude = amp;
        duration = Mathf.Max(0.01f, dur);
        elapsed = 0f;
        shaking = true;
    }

    /// <summary>순간적으로 화면을 확대했다가 돌아온다 (amount 0.05 = 5%).</summary>
    public void Punch(float amount)
    {
        if (cam == null) return;
        if (zoom <= 0f) baseSize = cam.orthographicSize;
        zoom = Mathf.Max(zoom, amount);
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;

        if (zoom > 0f)
        {
            zoom = Mathf.Max(0f, zoom - dt * 0.3f);
            cam.orthographicSize = baseSize * (1f - zoom);
        }

        if (!shaking) return;
        elapsed += dt;
        if (elapsed >= duration)
        {
            transform.localPosition = basePos;
            shaking = false;
            return;
        }
        float k = 1f - elapsed / duration;
        transform.localPosition = basePos + (Vector3)(Random.insideUnitCircle * amplitude * k * k);
    }
}

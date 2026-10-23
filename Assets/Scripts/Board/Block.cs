using System.Collections;
using UnityEngine;

/// <summary>
/// 보드 위 블록 하나. 이동·낙하·선택·펀치·파괴 애니메이션을 담당한다.
/// VARCO 3D 모델 프리팹에 붙여도 되고, 없으면 BoardManager가 임시 도형에 붙인다.
/// </summary>
public class Block : MonoBehaviour
{
    const float SquashTime = 0.12f;

    public int Type { get; private set; }
    public Color Color { get; private set; }
    public Vector2Int Cell { get; set; }

    Vector3 baseScale;
    bool selected, popping;
    float squash;
    Renderer[] renderers;
    MaterialPropertyBlock mpb;

    public void Init(int type, Color color, Vector2Int cell)
    {
        Type = type;
        Color = color;
        Cell = cell;
        baseScale = transform.localScale;
        renderers = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (popping) return;
        if (selected)
        {
            float s = 1f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f));
            transform.localScale = baseScale * s;
        }
        else if (squash > 0f)
        {
            squash = Mathf.Max(0f, squash - Time.deltaTime);
            float k = Mathf.Sin((1f - squash / SquashTime) * Mathf.PI) * 0.2f;
            transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + k, 1f - k, 1f));
        }
    }

    public void SetSelected(bool on)
    {
        selected = on;
        if (!on) transform.localScale = baseScale;
        Flash(on ? 0.35f : 0f);
    }

    public IEnumerator MoveTo(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            transform.position = Vector3.LerpUnclamped(start, target, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        transform.position = target;
    }

    /// <summary>가속하며 떨어지고, 착지하면 살짝 찌그러진다 (찌그러짐은 기다리지 않는다).</summary>
    public IEnumerator Fall(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            transform.position = Vector3.LerpUnclamped(start, target, k * k);
            yield return null;
        }
        transform.position = target;
        squash = SquashTime;
    }

    /// <summary>한 번 "팡" 하고 부풀었다 돌아온다.</summary>
    public IEnumerator Punch(float duration, float amount = 0.35f)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            transform.localScale = baseScale * (1f + amount * k);
            Flash(k * 0.8f);
            yield return null;
        }
        transform.localScale = baseScale;
        Flash(0f);
    }

    /// <summary>하얗게 번쩍이며 확 부풀었다가 회전하며 사라진 뒤 스스로 파괴된다.</summary>
    public IEnumerator Pop(float duration, float strength)
    {
        popping = true;
        selected = false;
        Flash(1f);
        Vector3 s0 = baseScale;
        Quaternion r0 = transform.rotation;
        float spin = Random.Range(-1f, 1f) * 360f * strength;
        float peak = 1.45f + 0.15f * strength;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / duration);
            float sc = k < 0.2f ? Mathf.Lerp(1f, peak, k / 0.2f) : Mathf.Lerp(peak, 0f, (k - 0.2f) / 0.8f);
            transform.localScale = s0 * sc;
            transform.rotation = r0 * Quaternion.Euler(0f, 0f, spin * k);
            Flash(1f - k * 0.5f);
            yield return null;
        }
        Destroy(gameObject);
    }

    void Flash(float amount)
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (amount <= 0f)
            {
                r.SetPropertyBlock(null);
                continue;
            }
            r.GetPropertyBlock(mpb);
            mpb.SetColor(MaterialUtil.BaseColorId, Color.Lerp(Color, Color.white, amount));
            mpb.SetColor(MaterialUtil.EmissionColorId, Color.white * amount);
            r.SetPropertyBlock(mpb);
        }
    }
}

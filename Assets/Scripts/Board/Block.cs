using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SpecialType { None, LineH, LineV, Bomb, Cross }

/// <summary>
/// 보드 위 블록 하나. 이동·낙하·선택·펀치·파괴 애니메이션과
/// 특수 블록(줄·원소 폭발), 적이 건 상태(점액으로 굳음, 불씨) 표시를 담당한다.
/// VARCO 3D 모델 프리팹에 붙여도 되고, 없으면 BoardManager가 임시 도형에 붙인다.
/// </summary>
public class Block : MonoBehaviour
{
    const float SquashTime = 0.12f;

    public int Type { get; private set; }
    public Color Color { get; private set; }
    public Vector2Int Cell { get; set; }

    public SpecialType Special { get; private set; }
    /// <summary>점액으로 굳음: 스왑할 수 없지만 매치에는 포함된다.</summary>
    public bool Locked { get; private set; }
    /// <summary>불씨: 다음 적 차례까지 터뜨리지 않으면 턴이 줄어든다.</summary>
    public bool Burning { get; private set; }

    // 표시용 오브젝트는 블록의 크기·회전을 물려받지 않도록 자식이 아닌 따로 두고 위치만 따라간다.
    readonly List<(Transform fx, Vector3 offset)> fxs = new List<(Transform, Vector3)>();
    Transform specialFx, lockFx, burnFx;

    Vector3 baseScale;
    bool selected, popping;
    float squash;
    Renderer[] renderers;
    MaterialPropertyBlock mpb;

    /// <summary>VARCO 그림을 붙인 블록이면 true (번쩍임을 색 섞기 대신 밝기로 한다).</summary>
    public bool Textured { get; set; }

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

    void LateUpdate()
    {
        float t = Time.unscaledTime;
        foreach (var (fx, offset) in fxs)
            if (fx != null) fx.position = transform.position + offset;

        if (burnFx != null)
        {
            float flicker = 0.85f + 0.25f * Mathf.Sin(t * 22f + Cell.x) * Mathf.Sin(t * 13f + Cell.y);
            burnFx.localScale = Vector3.one * flicker;
        }
        if (specialFx != null && Special == SpecialType.Bomb)
            specialFx.localScale = Vector3.one * (0.32f + 0.06f * Mathf.Sin(t * 8f));
    }

    void OnDestroy() => ClearFx();

    Transform AddFx(PrimitiveType shape, Vector3 offset, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(shape);
        Destroy(go.GetComponent<Collider>());
        go.name = "BlockFx";
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.transform.localScale = scale;
        go.transform.position = transform.position + offset;
        fxs.Add((go.transform, offset));
        return go.transform;
    }

    /// <summary>블록이 비쳐 보이도록 옅은 덮개 + 진한 테두리로 상태를 표시한다.</summary>
    Transform AddFrame(Color color, float tintAlpha, float z)
    {
        var root = new GameObject("BlockFrame").transform;
        root.position = transform.position + new Vector3(0f, 0f, z);
        fxs.Add((root, new Vector3(0f, 0f, z)));

        var tint = color;
        tint.a = tintAlpha;
        MaterialUtil.Primitive(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.9f, 0.9f, 0.02f), Color.white)
            .GetComponent<Renderer>().sharedMaterial = MaterialUtil.Unlit(tint);

        var edge = MaterialUtil.Lit(color, 1.2f);
        const float t = 0.09f, half = 0.45f;
        MaterialUtil.Primitive(PrimitiveType.Cube, root, new Vector3(0f, half, -0.02f), new Vector3(0.98f, t, 0.04f), color).GetComponent<Renderer>().sharedMaterial = edge;
        MaterialUtil.Primitive(PrimitiveType.Cube, root, new Vector3(0f, -half, -0.02f), new Vector3(0.98f, t, 0.04f), color).GetComponent<Renderer>().sharedMaterial = edge;
        MaterialUtil.Primitive(PrimitiveType.Cube, root, new Vector3(half, 0f, -0.02f), new Vector3(t, 0.98f, 0.04f), color).GetComponent<Renderer>().sharedMaterial = edge;
        MaterialUtil.Primitive(PrimitiveType.Cube, root, new Vector3(-half, 0f, -0.02f), new Vector3(t, 0.98f, 0.04f), color).GetComponent<Renderer>().sharedMaterial = edge;
        return root;
    }

    void RemoveFx(ref Transform fx)
    {
        if (fx == null) return;
        var target = fx;
        fxs.RemoveAll(f => f.fx == target);
        Destroy(fx.gameObject);
        fx = null;
    }

    void ClearFx()
    {
        foreach (var (fx, _) in fxs)
            if (fx != null) Destroy(fx.gameObject);
        fxs.Clear();
        specialFx = lockFx = burnFx = null;
    }

    public void SetSpecial(SpecialType special)
    {
        Special = special;
        RemoveFx(ref specialFx);
        var mat = MaterialUtil.Lit(Color.white, 2.5f);
        switch (special)
        {
            case SpecialType.LineH: specialFx = AddFx(PrimitiveType.Cube, new Vector3(0f, 0f, -0.55f), new Vector3(0.95f, 0.16f, 0.05f), mat); break;
            case SpecialType.LineV: specialFx = AddFx(PrimitiveType.Cube, new Vector3(0f, 0f, -0.55f), new Vector3(0.16f, 0.95f, 0.05f), mat); break;
            case SpecialType.Bomb: specialFx = AddFx(PrimitiveType.Sphere, new Vector3(0f, 0f, -0.55f), Vector3.one * 0.32f, mat); break;
            case SpecialType.Cross:
                var offset = new Vector3(0f, 0f, -0.55f);
                specialFx = new GameObject("CrossFx").transform;
                specialFx.position = transform.position + offset;
                fxs.Add((specialFx, offset));
                MaterialUtil.Primitive(PrimitiveType.Cube, specialFx, Vector3.zero, new Vector3(0.6f, 0.16f, 0.05f), Color.white).GetComponent<Renderer>().sharedMaterial = mat;
                MaterialUtil.Primitive(PrimitiveType.Cube, specialFx, Vector3.zero, new Vector3(0.16f, 0.6f, 0.05f), Color.white).GetComponent<Renderer>().sharedMaterial = mat;
                break;
        }
    }

    public void SetLocked(bool on)
    {
        Locked = on;
        RemoveFx(ref lockFx);
        if (on) lockFx = AddFrame(new Color(0.55f, 0.8f, 0.4f), 0.3f, -0.6f);
    }

    public void SetBurning(bool on)
    {
        Burning = on;
        RemoveFx(ref burnFx);
        if (on) burnFx = AddFrame(new Color(1f, 0.45f, 0.1f), 0.25f, -0.62f);
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
        ClearFx();
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
            mpb.SetColor(MaterialUtil.BaseColorId, Textured ? Color.white * (1f + 1.5f * amount) : Color.Lerp(Color, Color.white, amount));
            mpb.SetColor(MaterialUtil.EmissionColorId, Color.white * amount);
            r.SetPropertyBlock(mpb);
        }
    }
}

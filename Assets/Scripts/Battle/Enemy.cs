using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 적: HP, 피격 반응(흰색 번쩍임 + 흔들림 + 데미지 숫자), 사망 후 더 강해져서 재등장.
/// body에 VARCO 3D 모델을 넣으면 그 모델을 쓰고, 비어 있으면 임시 슬라임을 만든다.
/// </summary>
public class Enemy : MonoBehaviour
{
    public float baseMaxHp = 1800f;
    public float hpGrowthPerLevel = 1.3f;
    public Transform body;
    public Color bodyColor = new Color(0.35f, 0.85f, 0.45f);

    public int Level { get; private set; } = 1;
    public float MaxHp { get; private set; }
    public float Hp { get; private set; }
    public bool IsAlive => !dying && Hp > 0f;
    public Vector3 HitPoint => body.position;

    /// <summary>HP가 0이 된 순간.</summary>
    public event Action Defeated;
    /// <summary>다음 레벨로 다시 나타난 순간.</summary>
    public event Action Respawned;

    const float BarWidth = 2.6f;

    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    Transform hpFill;
    TextMesh hpText;
    ComboSystem combo;
    Vector3 bodyBasePos, bodyBaseScale;
    float shownRatio = 1f, flash, shake;
    bool dying;

    void Awake()
    {
        if (body == null) body = BuildSlime();
        bodyBasePos = body.localPosition;
        bodyBaseScale = body.localScale;
        renderers = body.GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = m != null && m.HasProperty(MaterialUtil.BaseColorId) ? m.GetColor(MaterialUtil.BaseColorId) : Color.white;
        }
        mpb = new MaterialPropertyBlock();
        BuildHpBar();
        MaxHp = Hp = baseMaxHp;
    }

    void Start()
    {
        combo = FindFirstObjectByType<ComboSystem>();
        RefreshHp();
    }

    /// <param name="popup">false면 데미지 숫자를 띄우지 않는다 (빛구슬처럼 잘게 나뉜 타격).</param>
    public void TakeDamage(int damage, int comboCount, string label = null, Color? color = null, bool popup = true)
    {
        if (!IsAlive) return;
        Hp = Mathf.Max(0f, Hp - damage);
        flash = popup ? 1f : Mathf.Max(flash, 0.7f);
        shake = Mathf.Max(shake, popup ? Mathf.Min(0.12f + 0.04f * comboCount, 0.5f) : 0.1f);

        if (label != null)
            DamagePopup.Spawn(body.position + Vector3.up * 0.4f, $"{label}\n{damage}", color ?? Color.white, 0.1f);
        else if (popup)
            DamagePopup.Spawn(body.position + Vector3.up * 0.8f, damage, comboCount);

        RefreshHp();
        if (Hp <= 0f)
        {
            dying = true;
            Defeated?.Invoke();
            Sfx.Play(SfxType.Impact, 0.8f, 1f);
            ScreenFlash.Flash(Color.white, 0.6f, 0.4f);
            var shakeCam = Camera.main != null ? Camera.main.GetComponent<CameraShake>() : null;
            if (shakeCam != null)
            {
                shakeCam.Shake(0.6f, 0.4f);
                shakeCam.Punch(0.08f);
            }
            StartCoroutine(DieAndRespawn());
        }
    }

    /// <summary>턴 제한 실패 후 재도전: 같은 레벨로 HP를 다시 채운다.</summary>
    public void RestoreHp()
    {
        Hp = MaxHp;
        RefreshHp();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        shownRatio = Mathf.MoveTowards(shownRatio, MaxHp > 0f ? Hp / MaxHp : 0f, dt * 1.5f);
        hpFill.localScale = new Vector3(shownRatio, 1f, 1f);

        if (!dying)
        {
            float bob = Mathf.Sin(Time.time * 3f) * 0.05f;
            body.localScale = Vector3.Scale(bodyBaseScale, new Vector3(1f + bob, 1f - bob, 1f));
        }

        Vector3 offset = Vector3.zero;
        if (shake > 0f)
        {
            offset = (Vector3)(Random.insideUnitCircle * shake);
            shake = Mathf.Max(0f, shake - dt * 2f);
        }
        body.localPosition = bodyBasePos + offset;

        if (flash > 0f) flash = Mathf.Max(0f, flash - dt * 6f);
        ApplyFlash(flash);
    }

    void ApplyFlash(float amount)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (amount <= 0f)
            {
                r.SetPropertyBlock(null);
                continue;
            }
            r.GetPropertyBlock(mpb);
            mpb.SetColor(MaterialUtil.BaseColorId, Color.Lerp(baseColors[i], Color.white, amount));
            mpb.SetColor(MaterialUtil.EmissionColorId, Color.white * amount);
            r.SetPropertyBlock(mpb);
        }
    }

    IEnumerator DieAndRespawn()
    {
        Quaternion r0 = body.localRotation;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float k = t / 0.5f;
            body.localScale = bodyBaseScale * (1f - k);
            body.localRotation = r0 * Quaternion.Euler(0f, 0f, 540f * k);
            yield return null;
        }
        body.localScale = Vector3.zero;
        body.localRotation = r0;
        Debris.Burst(body.position, bodyColor, 30, 9f, 0.3f);
        yield return new WaitForSeconds(1.2f);

        Level++;
        MaxHp = Mathf.Round(baseMaxHp * Mathf.Pow(hpGrowthPerLevel, Level - 1));
        Hp = MaxHp;
        RefreshHp();

        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            float k = t / 0.35f;
            body.localScale = bodyBaseScale * (k < 0.7f ? Mathf.Lerp(0f, 1.2f, k / 0.7f) : Mathf.Lerp(1.2f, 1f, (k - 0.7f) / 0.3f));
            yield return null;
        }
        dying = false;
        Respawned?.Invoke();
    }

    void RefreshHp()
    {
        string text = $"Lv.{Level}   {Mathf.CeilToInt(Hp)} / {Mathf.CeilToInt(MaxHp)}";
        if (combo != null && Hp > 0f && Hp <= combo.mageDamage + combo.warriorDamage)
            text = $"<color=#ffa040>{combo.warriorCombo} COMBO = KO!</color>\n" + text;
        hpText.text = text;
    }

    Transform BuildSlime()
    {
        var root = new GameObject("Body").transform;
        root.SetParent(transform, false);
        MaterialUtil.Primitive(PrimitiveType.Sphere, root, Vector3.zero, new Vector3(2.2f, 1.7f, 1.7f), bodyColor, 0.15f);
        MaterialUtil.Primitive(PrimitiveType.Sphere, root, new Vector3(-0.38f, 0.2f, -0.75f), Vector3.one * 0.28f, new Color(0.05f, 0.05f, 0.08f));
        MaterialUtil.Primitive(PrimitiveType.Sphere, root, new Vector3(0.38f, 0.2f, -0.75f), Vector3.one * 0.28f, new Color(0.05f, 0.05f, 0.08f));
        return root;
    }

    void BuildHpBar()
    {
        var bar = new GameObject("HpBar").transform;
        bar.SetParent(transform, false);
        bar.localPosition = new Vector3(0f, 1.45f, -0.5f);
        MaterialUtil.Primitive(PrimitiveType.Cube, bar, Vector3.zero, new Vector3(BarWidth + 0.1f, 0.26f, 0.05f), new Color(0.1f, 0.08f, 0.12f));

        hpFill = new GameObject("FillPivot").transform;
        hpFill.SetParent(bar, false);
        hpFill.localPosition = new Vector3(-BarWidth * 0.5f, 0f, -0.04f);
        MaterialUtil.Primitive(PrimitiveType.Cube, hpFill, new Vector3(BarWidth * 0.5f, 0f, 0f), new Vector3(BarWidth, 0.17f, 0.05f), new Color(1f, 0.2f, 0.25f), 0.5f);

        var label = new GameObject("HpText");
        label.transform.SetParent(bar, false);
        label.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        hpText = DamagePopup.CreateText(label, 0.045f, Color.white);
        hpText.anchor = TextAnchor.LowerCenter;
    }
}

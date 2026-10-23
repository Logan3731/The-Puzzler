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
    public float baseMaxHp = 600f;
    public float hpGrowthPerLevel = 1.15f;
    public Transform body;
    public Color bodyColor = new Color(0.35f, 0.85f, 0.45f);

    [Tooltip("스테이지마다 이 중에서 속성을 고른다 (0 불, 1 물, 2 바람, 3 번개, 4 어둠). 임시: 챕터 1 일반 몬스터 3종")]
    public int[] elementPool = { 2, 0, 1 };

    [Header("엘리트·보스 (챕터 1: 4·9 엘리트, 10 보스)")]
    public int[] eliteStages = { 4, 9 };
    public int bossStage = 10;
    public float eliteHp = 1.6f, eliteAttack = 1.3f, eliteScale = 1.25f;
    public float bossHp = 2.5f, bossAttack = 1.5f, bossScale = 1.6f;
    public int bossElement = 4;

    [Header("몬스터 그림 (속성 순서: 불·물·바람·번개·어둠, Assets/Resources/Art 이름)")]
    public string[] monsterArt = { "monster_goblin", "monster_toad", "monster_slime", "", "monster_wolf_boss" };
    public float[] monsterHeight = { 2.4f, 2.0f, 1.6f, 1.6f, 1.7f };

    public enum Rank { Normal, Elite, Boss }

    public int Level { get; private set; } = 1;
    public float MaxHp { get; private set; }
    public float Hp { get; private set; }
    public int Element { get; private set; } = 2;
    public Rank CurrentRank { get; private set; }
    public float AttackMultiplier => CurrentRank == Rank.Boss ? bossAttack : CurrentRank == Rank.Elite ? eliteAttack : 1f;
    public float RankScale => CurrentRank == Rank.Boss ? bossScale : CurrentRank == Rank.Elite ? eliteScale : 1f;
    /// <summary>적을 쓰러뜨리고 남은 데미지 (쓰러진 뒤 날아온 공격 포함). 마력 포인트가 된다.</summary>
    public event Action<int> Overkill;
    public bool IsAlive => !dying && Hp > 0f;
    public Vector3 HitPoint => body.position;
    /// <summary>쓰러진 뒤 보상 선택을 기다리는 중.</summary>
    public bool WaitingForNext { get; private set; }

    static readonly Color[] elementBodyColors =
    {
        new Color(1f, 0.42f, 0.35f), new Color(0.35f, 0.6f, 1f), new Color(0.35f, 0.85f, 0.45f),
        new Color(1f, 0.85f, 0.3f), new Color(0.65f, 0.4f, 0.95f),
    };

    /// <summary>HP가 0이 된 순간.</summary>
    public event Action Defeated;
    /// <summary>다음 레벨로 다시 나타난 순간.</summary>
    public event Action Respawned;

    const float BarWidth = 2.6f;

    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock mpb;
    Transform hpFill, hpBar;
    TextMesh hpText;
    Vector3 bodyBasePos, bodyBaseScale, bodyBasePosOffset;
    float shownRatio = 1f, flash, shake;
    bool dying;
    Renderer placeholderBody;
    Vector3 originalBodyScale;
    Transform decor;
    GameObject artQuad;
    float artHeight;

    const float FootY = -0.85f; // 몬스터 발 높이 (로컬)

    void Awake()
    {
        if (body == null)
        {
            var art = FirstMonsterArt();
            if (art != null)
            {
                // VARCO 몬스터 그림을 판에 붙여 세운다 (속성마다 그림을 바꾼다)
                body = new GameObject("Body").transform;
                body.SetParent(transform, false);
                artQuad = ArtLibrary.Quad(art, body, 2f, FootY, fitHeight: true);
            }
            else
            {
                body = BuildSlime();
                placeholderBody = body.GetComponentInChildren<Renderer>();
            }
        }
        bodyBasePos = body.localPosition;
        bodyBaseScale = originalBodyScale = body.localScale;
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
        ApplyStage();
        body.localScale = bodyBaseScale;
        RefreshHp();
    }

    /// <summary>현재 스테이지(Level)에 맞춰 등급·HP·속성·크기·장식을 정한다.</summary>
    void ApplyStage()
    {
        CurrentRank = Level == bossStage ? Rank.Boss : Array.IndexOf(eliteStages, Level) >= 0 ? Rank.Elite : Rank.Normal;
        float rankHp = CurrentRank == Rank.Boss ? bossHp : CurrentRank == Rank.Elite ? eliteHp : 1f;
        MaxHp = Hp = Mathf.Round(baseMaxHp * Mathf.Pow(hpGrowthPerLevel, Level - 1) * rankHp);
        shownRatio = 1f;
        PickElement();
        if (CurrentRank == Rank.Boss) SetElementColor(bossElement);

        bodyBaseScale = originalBodyScale * RankScale;
        bodyBasePos = new Vector3(0f, -FootY * (RankScale - 1f), 0f); // 커져도 발이 길에 붙어 있게
        float top = artQuad != null ? FootY + artHeight * RankScale : 0.85f + 1.6f * (RankScale - 1f);
        hpBar.localPosition = new Vector3(0f, top + 0.45f, -0.5f);
        BuildDecor();
    }

    Texture2D FirstMonsterArt()
    {
        foreach (var name in monsterArt)
        {
            var t = ArtLibrary.Get(name);
            if (t != null) return t;
        }
        return null;
    }

    /// <summary>현재 속성에 맞는 몬스터 그림으로 바꾼다 (그림이 없는 속성은 있는 그림 중 하나).</summary>
    void ApplyMonsterArt()
    {
        if (artQuad == null) return;
        var tex = ArtLibrary.Get(Element < monsterArt.Length ? monsterArt[Element] : null) ?? FirstMonsterArt();
        float h = Element < monsterHeight.Length ? monsterHeight[Element] : 1.8f;
        artQuad.GetComponent<Renderer>().sharedMaterial = ArtLibrary.MaterialFor(tex);
        ArtLibrary.SetSize(artQuad.transform, tex, h, FootY, fitHeight: true);
        artHeight = h;
        for (int i = 0; i < baseColors.Length; i++) baseColors[i] = Color.white;
    }

    /// <summary>임시 장식: 엘리트는 뿔 2개, 보스는 금색 왕관.</summary>
    void BuildDecor()
    {
        if (decor != null) Destroy(decor.gameObject);
        decor = null;
        if (CurrentRank == Rank.Normal || artQuad != null) return; // 그림 몬스터는 크기와 표시로만 구분
        decor = new GameObject("RankDecor").transform;
        decor.SetParent(body, false);
        if (CurrentRank == Rank.Elite)
        {
            var horn = new Color(0.35f, 0.08f, 0.1f);
            MaterialUtil.Primitive(PrimitiveType.Capsule, decor, new Vector3(-0.55f, 0.85f, -0.1f), new Vector3(0.18f, 0.32f, 0.18f), horn).transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            MaterialUtil.Primitive(PrimitiveType.Capsule, decor, new Vector3(0.55f, 0.85f, -0.1f), new Vector3(0.18f, 0.32f, 0.18f), horn).transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
        }
        else
        {
            var gold = new Color(1f, 0.8f, 0.2f);
            MaterialUtil.Primitive(PrimitiveType.Cylinder, decor, new Vector3(0f, 0.85f, 0f), new Vector3(0.9f, 0.08f, 0.9f), gold, 0.5f);
            for (int i = -1; i <= 1; i++)
                MaterialUtil.Primitive(PrimitiveType.Cube, decor, new Vector3(0.3f * i, 1.02f, -0.3f), new Vector3(0.14f, 0.26f, 0.06f), gold, 0.5f).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        foreach (var r in decor.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void SetElementColor(int element)
    {
        Element = element;
        bodyColor = elementBodyColors[Element];
        ApplyMonsterArt();
        if (placeholderBody == null) return;
        bodyColor = elementBodyColors[Element];
        placeholderBody.sharedMaterial = MaterialUtil.Lit(bodyColor, 0.15f);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] == placeholderBody) baseColors[i] = bodyColor;
    }

    void PickElement()
    {
        Element = elementPool != null && elementPool.Length > 0 ? elementPool[Random.Range(0, elementPool.Length)] : 2;
        bodyColor = elementBodyColors[Element];
        ApplyMonsterArt();
        if (placeholderBody == null) return;
        // 임시 슬라임은 속성 색으로 칠한다 (VARCO 모델이 들어오면 몬스터마다 모델이 다르다)
        bodyColor = elementBodyColors[Element];
        placeholderBody.sharedMaterial = MaterialUtil.Lit(bodyColor, 0.15f);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] == placeholderBody) baseColors[i] = bodyColor;
    }

    /// <summary>약점·저항 표시 (한 연쇄 단계에 한 번).</summary>
    public void ShowAffinity(bool weak)
    {
        if (!IsAlive) return;
        DamagePopup.Spawn(body.position + Vector3.up * 1.1f, weak ? "WEAK!" : "RESIST",
            weak ? new Color(1f, 0.6f, 0.15f) : new Color(0.7f, 0.7f, 0.75f), weak ? 0.09f : 0.06f);
    }

    /// <summary>보상을 고른 뒤 다음 적을 등장시킨다.</summary>
    public void ContinueToNext() => WaitingForNext = false;

    /// <param name="popup">false면 데미지 숫자를 띄우지 않는다 (빛구슬처럼 잘게 나뉜 타격).</param>
    public void TakeDamage(int damage, int comboCount, string label = null, Color? color = null, bool popup = true)
    {
        if (!IsAlive)
        {
            // 이미 쓰러진 뒤 도착한 공격은 전부 초과 데미지
            if (dying && damage > 0) Overkill?.Invoke(damage);
            return;
        }
        int overkill = Mathf.Max(0, damage - Mathf.CeilToInt(Hp));
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
            if (overkill > 0) Overkill?.Invoke(overkill);
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

    public void Heal(int amount)
    {
        if (!IsAlive || amount <= 0) return;
        Hp = Mathf.Min(MaxHp, Hp + amount);
        DamagePopup.Spawn(body.position + Vector3.up * 0.6f, $"+{amount}", new Color(0.4f, 1f, 0.5f), 0.1f);
        FxRing.Spawn(body.position + Vector3.back * 1.2f, new Color(0.4f, 1f, 0.5f), 2.8f, 0.4f);
        Sfx.Play(SfxType.Chime, 0.7f, 0.7f);
        RefreshHp();
    }

    /// <summary>행동할 때 몸을 부풀리며 화면 쪽으로 튀어나왔다 돌아온다.</summary>
    public IEnumerator Lunge()
    {
        Sfx.Play(SfxType.Whoosh, 0.7f, 0.8f);
        const float duration = 0.3f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            body.localScale = bodyBaseScale * (1f + 0.3f * k);
            bodyBasePosOffset = new Vector3(0f, -0.4f * k, -1f * k);
            yield return null;
        }
        bodyBasePosOffset = Vector3.zero;
        var cam = Camera.main != null ? Camera.main.GetComponent<CameraShake>() : null;
        if (cam != null) cam.Shake(0.25f, 0.2f);
    }

    /// <summary>게임 오버 후 새 판: 스테이지 1의 적으로 돌아간다.</summary>
    public void ResetRun()
    {
        StopAllCoroutines();
        dying = false;
        WaitingForNext = false;
        Level = 1;
        ApplyStage();
        hpBar.gameObject.SetActive(true);
        body.localScale = bodyBaseScale;
        body.localRotation = Quaternion.identity;
        bodyBasePosOffset = Vector3.zero;
        RefreshHp();
        Respawned?.Invoke();
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
        body.localPosition = bodyBasePos + bodyBasePosOffset + offset;

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
            mpb.SetColor(MaterialUtil.BaseColorId, Color.Lerp(baseColors[i], Color.white, amount) * (1f + amount));
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
        hpBar.gameObject.SetActive(false);

        // 보상 3중 택1을 고를 때까지 기다린다 (RunManager가 ContinueToNext를 부른다)
        WaitingForNext = true;
        while (WaitingForNext) yield return null;

        Level++;
        ApplyStage();
        RefreshHp();
        if (CurrentRank != Rank.Normal)
        {
            // 엘리트·보스 등장 경고
            DamagePopup.Spawn(body.position + Vector3.up * 2.5f, CurrentRank == Rank.Boss ? "BOSS" : "ELITE",
                CurrentRank == Rank.Boss ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.6f, 0.2f), 0.14f);
            Sfx.Play(SfxType.Impact, 0.6f, 1f);
        }

        // 길 저 끝(지평선)에서 작게 나타나 다가온 뒤 제자리에 쿵 하고 착지한다
        const float approach = 0.7f;
        for (float t = 0f; t < approach; t += Time.deltaTime)
        {
            float k = t / approach;
            float e = k * k;
            body.localScale = bodyBaseScale * Mathf.Lerp(0.12f, 1f, e);
            bodyBasePosOffset = new Vector3(0f, Mathf.Lerp(1.7f, 0f, e) + Mathf.Sin(k * Mathf.PI * 4f) * 0.12f * (1f - k), Mathf.Lerp(3f, 0f, e));
            yield return null;
        }
        bodyBasePosOffset = Vector3.zero;
        body.localScale = bodyBaseScale;
        Sfx.Play(SfxType.Hit, 0.7f, 0.8f);
        var landCam = Camera.main != null ? Camera.main.GetComponent<CameraShake>() : null;
        if (landCam != null) landCam.Shake(0.2f, 0.15f);
        hpBar.gameObject.SetActive(true);
        dying = false;
        Respawned?.Invoke();
    }

    void RefreshHp()
    {
        int weak = DamageCalculator.WeakTo(Element);
        int resist = DamageCalculator.Resists(Element);
        string rank = CurrentRank == Rank.Boss ? "<color=#ff5050>BOSS</color> · " : CurrentRank == Rank.Elite ? "<color=#ffa040>ELITE</color> · " : "";
        hpText.text = rank + $"<color={SkillCatalog.ElementHex[Element]}>{SkillCatalog.ElementNames[Element]}</color> 속성 · 약점 " +
                      $"<color={SkillCatalog.ElementHex[weak]}>{SkillCatalog.ElementNames[weak]}</color> · 저항 " +
                      $"<color={SkillCatalog.ElementHex[resist]}>{SkillCatalog.ElementNames[resist]}</color>\n" +
                      $"Lv.{Level}   {Mathf.CeilToInt(Hp)} / {Mathf.CeilToInt(MaxHp)}";
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
        hpBar = bar;
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
        hpText = DamagePopup.CreateText(label, 0.04f, Color.white);
        hpText.anchor = TextAnchor.LowerCenter;
    }
}

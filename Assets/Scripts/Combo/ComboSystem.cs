using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 연쇄 콤보 카운트와 단계별 연출·동료 공격을 지휘한다.
/// 매 콤보: 터지는 소리(콤보마다 음이 올라감) + 흔들림 + 빛구슬 공격 (블록 원소별 데미지)
/// 2콤보+: 콤보 텍스트 / 3·5·7…콤보 돌파 순간: 히트스톱 + 줌 펀치 + 차임
/// 동료(마법사 3콤보, 전사 5콤보, 궁수 7콤보)는 보상으로 합류한 뒤에만, 한 턴에 한 번 고정 데미지로 공격한다.
/// </summary>
public class ComboSystem : MonoBehaviour
{
    [Header("Mage (마법사)")]
    public int mageCombo = 3;
    public int mageDamage = 100;

    [Header("Warrior (전사)")]
    public int warriorCombo = 5;
    public int warriorDamage = 250;

    [Header("Archer (궁수)")]
    public int archerCombo = 7;
    public int archerDamage = 400;

    [Header("References (비워두면 자동으로 찾거나 만든다)")]
    public MagicMissile magicMissile;
    public WeaponStrike weaponStrike;
    public ArcherVolley archerVolley;
    public CameraShake cameraShake;
    public HitStop hitStop;
    public ComboText comboText;
    public Enemy enemy;
    public PlayerSkills skills;
    public Player player;

    public int Combo { get; private set; }
    public int ChainDamage { get; private set; }

    /// <summary>적에게 아직 날아가는 중인 공격(빛구슬·동료) 수.</summary>
    public int PendingHits { get; set; }

    bool mageFired, warriorFired, archerFired;

    public int MageDamage => Mathf.RoundToInt(mageDamage * skills.CompanionMultiplier(SkillId.Mage));
    public int WarriorDamage => Mathf.RoundToInt(warriorDamage * skills.CompanionMultiplier(SkillId.Warrior));
    public int ArcherDamage => Mathf.RoundToInt(archerDamage * skills.CompanionMultiplier(SkillId.Archer));

    void Awake()
    {
        if (magicMissile == null) magicMissile = GetOrAdd<MagicMissile>(gameObject);
        if (weaponStrike == null) weaponStrike = GetOrAdd<WeaponStrike>(gameObject);
        if (archerVolley == null) archerVolley = GetOrAdd<ArcherVolley>(gameObject);
        if (hitStop == null) hitStop = FindFirstObjectByType<HitStop>();
        if (hitStop == null) hitStop = gameObject.AddComponent<HitStop>();
        if (cameraShake == null && Camera.main != null) cameraShake = GetOrAdd<CameraShake>(Camera.main.gameObject);
        if (comboText == null) comboText = FindFirstObjectByType<ComboText>();
        if (comboText == null) comboText = new GameObject("ComboText").AddComponent<ComboText>();
        if (enemy == null) enemy = FindFirstObjectByType<Enemy>();
        if (skills == null) skills = FindFirstObjectByType<PlayerSkills>();
        if (skills == null) skills = gameObject.AddComponent<PlayerSkills>();
        if (player == null) player = FindFirstObjectByType<Player>();
    }

    static T GetOrAdd<T>(GameObject go) where T : Component =>
        go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();

    public void BeginChain()
    {
        Combo = 0;
        ChainDamage = 0;
        mageFired = warriorFired = archerFired = false;
        comboText.ResetChain();
    }

    /// <summary>연쇄가 끝났을 때: 공명 방패 확인.</summary>
    public void EndChain()
    {
        if (Combo >= 3 && skills.ShieldReduction > 0f && player != null)
            player.ArmShield(skills.ShieldReduction);
    }

    bool IsMilestone(int combo) =>
        combo == mageCombo || combo == warriorCombo || (combo > warriorCombo && (combo - warriorCombo) % 2 == 0);

    /// <summary>매치 한 번(연쇄 한 단계)이 터질 때 호출. 현재 콤보 수를 돌려준다.</summary>
    public int RegisterMatch()
    {
        Combo++;
        if (Combo >= 2) comboText.Show(Combo);

        // 콤보마다 1.5음씩 올라가는 터짐 소리 (최대 한 옥타브)
        float pitch = Mathf.Pow(2f, Mathf.Min(Combo - 1, 8) * 1.5f / 12f);
        Sfx.Play(SfxType.Pop, pitch, 0.7f);
        cameraShake?.Shake(Mathf.Min(0.06f + 0.03f * Combo, 0.4f), 0.12f + 0.02f * Combo);

        if (IsMilestone(Combo))
        {
            hitStop.Stop(0.06f);
            cameraShake?.Punch(0.035f + 0.005f * Combo);
            Sfx.Play(SfxType.Chime, 1f + 0.08f * (Combo - mageCombo), 0.6f);
            ScreenFlash.Flash(Color.white, 0.15f, 0.15f);
        }
        return Combo;
    }

    /// <summary>
    /// 터진 블록 위치마다 빛구슬을 쏜다. 블록 원소마다 데미지가 다르다 (상성, 원소 친화).
    /// firstStep = 이번 턴의 첫 매치 (첫 일격).
    /// </summary>
    public void LaunchOrbs(List<(Vector3 position, Color color, int element)> hits, bool firstStep)
    {
        if (enemy == null || hits.Count == 0) return;
        int combo = Combo;

        float total = 0f;
        bool anyWeak = false, anyResist = false;
        var damages = new List<float>(hits.Count);
        foreach (var h in hits)
        {
            float d = DamageCalculator.BlockDamage(h.element, combo, enemy.Element, skills, firstStep);
            damages.Add(d);
            total += d;
            if (DamageCalculator.Beats(h.element, enemy.Element)) anyWeak = true;
            else if (DamageCalculator.Beats(enemy.Element, h.element)) anyResist = true;
        }
        if (anyWeak) enemy.ShowAffinity(true);
        else if (anyResist) enemy.ShowAffinity(false);

        // 특수 블록으로 한꺼번에 많이 터지면 구슬 수만 줄인다 (데미지 합계는 그대로)
        const int maxOrbs = 16;
        int count = Mathf.Min(maxOrbs, hits.Count);
        int totalInt = Mathf.RoundToInt(total);
        int given = 0;
        for (int i = 0; i < count; i++)
        {
            var h = hits[i * hits.Count / count];
            int damage = i == count - 1 ? totalInt - given
                       : hits.Count <= maxOrbs ? Mathf.RoundToInt(damages[i])
                       : Mathf.RoundToInt(total / count);
            given += damage;

            PendingHits++;
            AttackOrb.Spawn(h.position, enemy, h.color, i * 0.025f, () =>
            {
                OnOrbHit(damage, combo);
                PendingHits--;
            });
        }
    }

    void OnOrbHit(int damage, int combo)
    {
        if (damage > 0) enemy.TakeDamage(damage, combo, popup: false);
        AddChainDamage(damage);
        Sfx.Play(SfxType.Hit, Random.Range(0.9f, 1.1f) + 0.04f * combo, 0.45f);
        cameraShake?.Shake(0.05f + 0.01f * combo, 0.08f);
    }

    void AddChainDamage(int damage)
    {
        ChainDamage += damage;
        comboText.SetDamage(ChainDamage);
    }

    public IEnumerator PlayBonuses(BoardManager board)
    {
        if (skills.Has(SkillId.Mage) && Combo >= mageCombo && !mageFired && magicMissile != null)
        {
            mageFired = true;
            yield return magicMissile.Play(board, this);
        }
        if (skills.Has(SkillId.Warrior) && Combo >= warriorCombo && !warriorFired && weaponStrike != null)
        {
            warriorFired = true;
            yield return weaponStrike.Play(board, this);
        }
        if (skills.Has(SkillId.Archer) && Combo >= archerCombo && !archerFired && archerVolley != null)
        {
            archerFired = true;
            yield return archerVolley.Play(board, this);
        }
    }

    public void CompanionHit(int damage, string label, Color color, int combo)
    {
        if (enemy == null) return;
        enemy.TakeDamage(damage, combo, label, color);
        AddChainDamage(damage);
    }
}

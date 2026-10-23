using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이번 판(런)에서 얻은 스킬 레벨과 마나. 게임 오버가 되면 모두 사라진다.
/// 다른 스크립트는 여기 있는 계산 값(BonusPerBlock, ComboBonus 등)만 읽는다.
/// </summary>
public class PlayerSkills : MonoBehaviour
{
    public int maxMana = 30;

    readonly Dictionary<SkillId, int> levels = new Dictionary<SkillId, int>();
    readonly Dictionary<SkillId, int> cooldowns = new Dictionary<SkillId, int>();

    public int Mana { get; private set; }
    public event Action Changed;

    public int Level(SkillId id) => levels.TryGetValue(id, out var l) ? l : 0;
    public bool Has(SkillId id) => Level(id) > 0;

    public bool HasAnyActive => Has(SkillId.ActiveConvert) || Has(SkillId.ActiveHammer) || Has(SkillId.ActiveShuffle) || Has(SkillId.ActiveDelay);

    // ── 계산 값 ──
    public int BonusPerBlock => 3 * Level(SkillId.Attack);
    public float ComboBonus => Has(SkillId.ComboRhythm) ? 0.2f + 0.1f * (Level(SkillId.ComboRhythm) - 1) : 0f;
    public float WeakMultiplier => 2f + 0.5f * Level(SkillId.WeakSpot);
    public float FirstStrikeMultiplier => Has(SkillId.FirstStrike) ? 2f + 0.5f * (Level(SkillId.FirstStrike) - 1) : 1f;
    public int Armor => 2 * Level(SkillId.Armor);
    public float ClearHealRatio => 0.2f + 0.15f * Level(SkillId.FirstAid);
    public int PurifyHeal => 3 * Level(SkillId.Purify);
    public float ShieldReduction => Has(SkillId.Shield) ? 0.5f + 0.15f * (Level(SkillId.Shield) - 1) : 0f;

    public float Affinity(int element)
    {
        int l = Level(SkillId.AffinityFire + element);
        return l > 0 ? 1.5f + 0.25f * (l - 1) : 1f;
    }

    public float CompanionMultiplier(SkillId id) => 1f + 0.5f * Mathf.Max(0, Level(id) - 1);

    public static int BaseCost(SkillId id) =>
        id == SkillId.ActiveHammer ? 8 : id == SkillId.ActiveConvert ? 12 : 15;

    /// <summary>액티브 스킬 비용: 레벨이 오를 때마다 20%씩 싸진다.</summary>
    public static int CostAt(SkillId id, int level) =>
        Mathf.Max(1, Mathf.RoundToInt(BaseCost(id) * (1f - 0.2f * (Mathf.Max(1, level) - 1))));

    public int Cost(SkillId id) => CostAt(id, Level(id));

    /// <summary>액티브 스킬 쿨타임 (플레이어 턴 수).</summary>
    public static int BaseCooldown(SkillId id) =>
        id == SkillId.ActiveHammer ? 2 : id == SkillId.ActiveConvert ? 3 : 4;

    public int Cooldown(SkillId id) => cooldowns.TryGetValue(id, out var c) ? c : 0;
    public bool Ready(SkillId id) => Cooldown(id) <= 0;

    public void StartCooldown(SkillId id)
    {
        cooldowns[id] = BaseCooldown(id);
        Changed?.Invoke();
    }

    /// <summary>플레이어 턴이 끝날 때마다 1씩 줄어든다.</summary>
    public void TickCooldowns()
    {
        if (cooldowns.Count == 0) return;
        foreach (var id in new List<SkillId>(cooldowns.Keys))
            if (cooldowns[id] > 0) cooldowns[id]--;
        Changed?.Invoke();
    }

    // ── 변경 ──
    public void Grant(SkillId id, Player player)
    {
        if (id == SkillId.Heal)
        {
            player.Heal(Mathf.RoundToInt(player.maxHp * 0.3f));
            return;
        }
        levels[id] = Level(id) + 1;
        if (id == SkillId.MaxHp) player.AddMaxHp(20);
        Changed?.Invoke();
    }

    public void AddMana(int amount)
    {
        if (!HasAnyActive || amount <= 0) return;
        Mana = Mathf.Min(maxMana, Mana + amount);
        Changed?.Invoke();
    }

    public bool SpendMana(int amount)
    {
        if (Mana < amount) return false;
        Mana -= amount;
        Changed?.Invoke();
        return true;
    }

    public void ResetRun()
    {
        levels.Clear();
        cooldowns.Clear();
        Mana = 0;
        Changed?.Invoke();
    }
}

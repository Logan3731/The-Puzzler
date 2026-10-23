using System;

public enum SkillId
{
    // 일반
    Heal, MaxHp, Attack, FirstAid, Armor,
    // 동료
    Mage, Warrior, Archer,
    // 특수 블록
    LineBlock, BombBlock, CrossBlock,
    // 공격 강화 (원소 친화는 원소 순서 불·물·바람·번개·어둠과 같다)
    ComboRhythm, AffinityFire, AffinityWater, AffinityWind, AffinityLightning, AffinityDark, WeakSpot, FirstStrike,
    // 생존·대응
    Purify, Shield,
    // 액티브
    ActiveConvert, ActiveHammer, ActiveShuffle, ActiveDelay,
}

public enum SkillCategory { Basic, Companion, Special, Offense, Defense, Active }

/// <summary>등급: 일반(초록) · 희귀(파랑) · 영웅(보라) · 전설(금색)</summary>
public enum SkillRarity { Common, Rare, Epic, Legendary }

public class SkillDef
{
    public SkillId id;
    public string name;
    public SkillCategory category;
    public SkillRarity rarity;
    public int maxLevel;
    /// <summary>고른 뒤의 레벨을 받아 설명을 돌려준다.</summary>
    public Func<int, string> describe;
    /// <summary>보상 후보로 뽑힐 상대 확률 (원소 친화 5종은 합쳐서 1개 정도가 되게 낮춘다).</summary>
    public float weight = 1f;
}

/// <summary>클리어 보상으로 얻는 스킬 목록. 숫자를 바꾸면 PlayerSkills의 계산도 같이 바꾼다.</summary>
public static class SkillCatalog
{
    public static readonly string[] ElementNames = { "불", "물", "바람", "번개", "어둠" };
    public static readonly string[] ElementHex = { "#ff5a5a", "#5a8cff", "#5ae070", "#ffd84a", "#b46aff" };

    public static readonly SkillDef[] All =
    {
        new SkillDef { id = SkillId.Heal, rarity = SkillRarity.Common, name = "회복", category = SkillCategory.Basic, maxLevel = 999, describe = _ => "체력 30% 즉시 회복" },
        new SkillDef { id = SkillId.MaxHp, rarity = SkillRarity.Common, name = "최대 체력 증가", category = SkillCategory.Basic, maxLevel = 5, describe = _ => "최대 HP +20\n(그만큼 회복)" },
        new SkillDef { id = SkillId.Attack, rarity = SkillRarity.Common, name = "기본 공격력 증가", category = SkillCategory.Basic, maxLevel = 5, describe = l => $"블록당 데미지 +3\n(합계 +{3 * l})" },
        new SkillDef { id = SkillId.FirstAid, rarity = SkillRarity.Common, name = "응급처치", category = SkillCategory.Basic, maxLevel = 3, describe = l => $"스테이지 클리어 회복\n{20 + 15 * (l - 1)}% → {20 + 15 * l}%" },
        new SkillDef { id = SkillId.Armor, rarity = SkillRarity.Common, name = "단단한 피부", category = SkillCategory.Basic, maxLevel = 3, describe = l => $"받는 데미지 -{2 * l}" },

        new SkillDef { id = SkillId.Mage, rarity = SkillRarity.Rare, name = "마법사 합류", category = SkillCategory.Companion, maxLevel = 3, describe = l => l == 1 ? "한 턴에 3콤보 →\n마법미사일 (적에게 100)" : $"마법사 데미지\n{Companion(100, l)}" },
        new SkillDef { id = SkillId.Warrior, rarity = SkillRarity.Epic, name = "전사 합류", category = SkillCategory.Companion, maxLevel = 3, describe = l => l == 1 ? "한 턴에 5콤보 →\n회전 검 (적에게 250)" : $"전사 데미지\n{Companion(250, l)}" },
        new SkillDef { id = SkillId.Archer, rarity = SkillRarity.Legendary, name = "궁수 합류", category = SkillCategory.Companion, maxLevel = 3, describe = l => l == 1 ? "한 턴에 7콤보 →\n화살비 (적에게 400)" : $"궁수 데미지\n{Companion(400, l)}" },

        new SkillDef { id = SkillId.LineBlock, rarity = SkillRarity.Rare, name = "줄 파괴", category = SkillCategory.Special, maxLevel = 1, describe = _ => "4개 일자 매치 →\n한 줄을 터뜨리는 블록" },
        new SkillDef { id = SkillId.BombBlock, rarity = SkillRarity.Legendary, name = "원소 폭발", category = SkillCategory.Special, maxLevel = 1, describe = _ => "5개 일자 매치 →\n같은 원소를 전부 터뜨리는 블록" },
        new SkillDef { id = SkillId.CrossBlock, rarity = SkillRarity.Epic, name = "폭탄 블록", category = SkillCategory.Special, maxLevel = 1, describe = _ => "L·T자 매치 →\n주변 3×3을 터뜨리는 폭탄 블록\n(특수 블록과 바꾸면 더 크게)" },

        new SkillDef { id = SkillId.ComboRhythm, rarity = SkillRarity.Rare, name = "연쇄의 리듬", category = SkillCategory.Offense, maxLevel = 3, describe = l => $"콤보당 데미지 +{20 + 10 * (l - 1)}%" },
        Affinity(SkillId.AffinityFire, 0),
        Affinity(SkillId.AffinityWater, 1),
        Affinity(SkillId.AffinityWind, 2),
        Affinity(SkillId.AffinityLightning, 3),
        Affinity(SkillId.AffinityDark, 4),
        new SkillDef { id = SkillId.WeakSpot, rarity = SkillRarity.Epic, name = "약점 간파", category = SkillCategory.Offense, maxLevel = 2, describe = l => $"약점 데미지\n×{2f + 0.5f * (l - 1):0.0} → ×{2f + 0.5f * l:0.0}" },
        new SkillDef { id = SkillId.FirstStrike, rarity = SkillRarity.Rare, name = "첫 일격", category = SkillCategory.Offense, maxLevel = 3, describe = l => $"매 턴 첫 매치 데미지\n×{2f + 0.5f * (l - 1):0.0}" },

        new SkillDef { id = SkillId.Purify, rarity = SkillRarity.Rare, name = "정화", category = SkillCategory.Defense, maxLevel = 3, describe = l => $"점액·불씨를 풀면\nHP +{3 * l}" },
        new SkillDef { id = SkillId.Shield, rarity = SkillRarity.Epic, name = "공명 방패", category = SkillCategory.Defense, maxLevel = 3, describe = l => $"한 턴에 3콤보 이상 →\n다음 공격 데미지 -{50 + 15 * (l - 1)}%" },

        new SkillDef { id = SkillId.ActiveConvert, rarity = SkillRarity.Epic, name = "원소 변환", category = SkillCategory.Active, maxLevel = 3, describe = l => Active("블록 1개의 원소를 바꾼다", SkillId.ActiveConvert, l) },
        new SkillDef { id = SkillId.ActiveHammer, rarity = SkillRarity.Epic, name = "망치", category = SkillCategory.Active, maxLevel = 3, describe = l => Active("블록 1개를 즉시 부순다", SkillId.ActiveHammer, l) },
        new SkillDef { id = SkillId.ActiveShuffle, rarity = SkillRarity.Rare, name = "대섞기", category = SkillCategory.Active, maxLevel = 3, describe = l => Active("보드 전체를 섞는다", SkillId.ActiveShuffle, l) },
        new SkillDef { id = SkillId.ActiveDelay, rarity = SkillRarity.Rare, name = "시간 지연", category = SkillCategory.Active, maxLevel = 3, describe = l => Active("적 행동을 1턴 늦춘다", SkillId.ActiveDelay, l) },
    };

    /// <summary>액티브 스킬 설명: 효과 + 비용 + 마나가 무엇인지 (처음 보는 사람을 위해).</summary>
    static string Active(string effect, SkillId id, int level) =>
        $"{effect}\n\n마나 {PlayerSkills.CostAt(id, level)} · 쿨타임 {PlayerSkills.BaseCooldown(id)}턴\n<size=24>마나는 블록을 부술 때마다 1씩 찬다 (최대 30)</size>";

    static int Companion(int baseDamage, int level) => UnityEngine.Mathf.RoundToInt(baseDamage * (1f + 0.5f * (level - 1)));

    static SkillDef Affinity(SkillId id, int element) => new SkillDef
    {
        id = id,
        rarity = SkillRarity.Common,
        name = $"{ElementNames[element]} 친화",
        category = SkillCategory.Offense,
        maxLevel = 3,
        weight = 0.25f,
        describe = l => $"{ElementNames[element]} 블록 데미지 ×{1.5f + 0.25f * (l - 1):0.00}",
    };

    public static SkillDef Get(SkillId id)
    {
        foreach (var def in All)
            if (def.id == id) return def;
        return null;
    }
}

using System;
using UnityEngine;

/// <summary>
/// 퍼즐러(플레이어)의 체력. 적의 공격과 불씨에 맞아 줄고, 0이 되면 게임 오버다.
/// 체력은 스테이지 사이에 이어지고, 클리어할 때 조금 회복한다 (응급처치로 늘어난다).
/// </summary>
public class Player : MonoBehaviour
{
    public int baseMaxHp = 100;

    public int maxHp { get; private set; }
    public int Hp { get; private set; }
    public bool IsDead => Hp <= 0;
    /// <summary>공명 방패: 다음 적 공격 데미지를 줄이는 비율 (0이면 없음).</summary>
    public float Shield { get; private set; }

    /// <summary>데미지(음수) 또는 회복(양수)이 들어올 때. 0이면 최대치 변화 등 표시만 갱신.</summary>
    public event Action<int> Changed;
    public event Action Died;

    PlayerSkills skills;

    void Awake()
    {
        maxHp = Hp = baseMaxHp;
        skills = FindFirstObjectByType<PlayerSkills>();
    }

    /// <param name="isAttack">적의 공격이면 공명 방패가 적용된다.</param>
    public void TakeDamage(int amount, bool isAttack = false)
    {
        if (IsDead || amount <= 0) return;
        if (isAttack && Shield > 0f)
        {
            amount = Mathf.RoundToInt(amount * (1f - Shield));
            Shield = 0f;
            Sfx.Play(SfxType.Chime, 0.6f, 0.6f);
        }
        if (skills != null) amount -= skills.Armor;
        amount = Mathf.Max(1, amount);

        Hp = Mathf.Max(0, Hp - amount);
        Changed?.Invoke(-amount);

        ScreenFlash.Flash(new Color(1f, 0.2f, 0.15f), 0.35f, 0.35f);
        Sfx.Play(SfxType.Impact, 1.1f, 0.9f);
        var shake = Camera.main != null ? Camera.main.GetComponent<CameraShake>() : null;
        if (shake != null) shake.Shake(0.35f + 0.01f * amount, 0.25f);

        if (IsDead) Died?.Invoke();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        int before = Hp;
        Hp = Mathf.Min(maxHp, Hp + amount);
        if (Hp > before) Changed?.Invoke(Hp - before);
    }

    public void HealForClear() => Heal(Mathf.RoundToInt(maxHp * (skills != null ? skills.ClearHealRatio : 0.2f)));

    public void AddMaxHp(int amount)
    {
        maxHp += amount;
        Heal(amount);
        Changed?.Invoke(0);
    }

    public void ArmShield(float reduction)
    {
        if (reduction <= Shield) return;
        Shield = reduction;
        Changed?.Invoke(0);
    }

    public void ResetRun()
    {
        maxHp = Hp = baseMaxHp;
        Shield = 0f;
        Changed?.Invoke(0);
    }
}

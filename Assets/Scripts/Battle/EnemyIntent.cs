using System.Collections;
using UnityEngine;

public enum IntentType { Attack, Slime, Ember, Heal }

/// <summary>
/// 적의 행동 예고. 적 옆에 "N턴 뒤: 행동"을 보여주고, 턴이 다 되면 행동한 뒤 다음 행동을 고른다.
/// 공격: 플레이어 HP를 깎는다 / 점액: 블록을 굳혀 스왑을 막는다 /
/// 불씨: 다음 적 차례까지 안 터뜨리면 하나당 데미지 / 회복: 적 HP 회복.
/// (임시: 몬스터 종류와 상관없이 가중치로 고른다. 나중에 몬스터별 행동 목록으로 바꾼다.)
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemyIntent : MonoBehaviour
{
    [Header("공격")]
    public int attackDamage = 12;
    [Tooltip("스테이지마다 공격력이 이만큼 비율로 늘어난다")]
    public float attackGrowthPerLevel = 0.1f;
    public int attackCountdown = 2;
    public int attackWeight = 3;

    [Header("점액")]
    public int slimeBlocks = 2;
    public int slimeCountdown = 3;
    public int slimeWeight = 1;

    [Header("불씨")]
    public int emberBlocks = 2;
    public int emberDamage = 8;
    public int emberCountdown = 2;
    public int emberWeight = 1;

    [Header("회복")]
    [Range(0f, 1f)] public float healRatio = 0.15f;
    public int healCountdown = 3;
    public int healWeight = 1;

    public IntentType Current { get; private set; }
    public int TurnsLeft { get; private set; }

    Enemy enemy;
    TextMesh title, detail;
    Transform labelRoot;

    int AttackDamage => Mathf.RoundToInt(attackDamage * (1f + attackGrowthPerLevel * (enemy.Level - 1)) * enemy.AttackMultiplier);

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        labelRoot = new GameObject("IntentLabel").transform;
        labelRoot.SetParent(transform, false);
        // 몬스터 바로 아래 가운데 (길 위)
        labelRoot.localPosition = new Vector3(0f, -1.25f, -0.5f);

        var t = new GameObject("Title");
        t.transform.SetParent(labelRoot, false);
        title = DamagePopup.CreateText(t, 0.045f, Color.white);

        var d = new GameObject("Detail");
        d.transform.SetParent(labelRoot, false);
        d.transform.localPosition = new Vector3(0f, -0.42f, 0f);
        detail = DamagePopup.CreateText(d, 0.036f, new Color(0.95f, 0.92f, 0.85f));
    }

    void OnEnable()
    {
        enemy.Defeated += OnDefeated;
        enemy.Respawned += PickNext;
    }

    void OnDisable()
    {
        enemy.Defeated -= OnDefeated;
        enemy.Respawned -= PickNext;
    }

    void Start() => PickNext();

    void OnDefeated() => labelRoot.gameObject.SetActive(false);

    void PickNext()
    {
        int total = attackWeight + slimeWeight + emberWeight + healWeight;
        int roll = Random.Range(0, Mathf.Max(1, total));
        Current = roll < attackWeight ? IntentType.Attack
                : roll < attackWeight + slimeWeight ? IntentType.Slime
                : roll < attackWeight + slimeWeight + emberWeight ? IntentType.Ember
                : IntentType.Heal;
        TurnsLeft = Current == IntentType.Attack ? attackCountdown
                  : Current == IntentType.Slime ? slimeCountdown
                  : Current == IntentType.Ember ? emberCountdown
                  : healCountdown;
        labelRoot.gameObject.SetActive(true);
        labelRoot.localPosition = new Vector3(0f, -1.25f, -0.5f); // 몬스터 발밑 (크기가 커져도 발 위치는 같다)
        Refresh();
    }

    void Refresh()
    {
        string name = Current == IntentType.Attack ? "공격" : Current == IntentType.Slime ? "점액" : Current == IntentType.Ember ? "불씨" : "회복";
        title.text = $"{TurnsLeft}턴 뒤: {name}";
        title.color = TurnsLeft <= 1 ? new Color(1f, 0.4f, 0.35f) : Color.white;
        detail.text = Current == IntentType.Attack ? $"{AttackDamage} 데미지"
                    : Current == IntentType.Slime ? $"블록 {slimeBlocks}개를 굳힌다"
                    : Current == IntentType.Ember ? $"블록 {emberBlocks}개에 불 · 남으면 개당 {emberDamage}"
                    : $"HP를 {Mathf.RoundToInt(healRatio * 100f)}% 회복한다";
    }

    /// <summary>시간 지연 스킬: 행동을 n턴 늦춘다.</summary>
    public void Delay(int turns)
    {
        TurnsLeft += turns;
        Refresh();
    }

    void Update()
    {
        float pulse = TurnsLeft <= 1 ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
        labelRoot.localScale = Vector3.one * pulse;
    }

    /// <summary>플레이어 턴(연쇄와 공격 명중)이 모두 끝난 뒤 보드가 부른다.</summary>
    public IEnumerator TakeTurn(BoardManager board, Player player)
    {
        if (!enemy.IsAlive) yield break;

        // 지난 적 차례에 붙인 불씨가 아직 남아 있으면 그만큼 데미지를 입는다.
        int burned = board.BurnOut(emberDamage);
        if (burned > 0)
        {
            player.TakeDamage(burned * emberDamage);
            yield return new WaitForSeconds(0.5f);
            if (player.IsDead) yield break;
        }

        TurnsLeft--;
        Refresh();
        if (TurnsLeft > 0) yield break;

        yield return enemy.Lunge();
        switch (Current)
        {
            case IntentType.Attack: player.TakeDamage(AttackDamage, isAttack: true); yield return new WaitForSeconds(0.4f); break;
            case IntentType.Slime: yield return board.ApplySlime(slimeBlocks, enemy.HitPoint); break;
            case IntentType.Ember: yield return board.ApplyEmber(emberBlocks, enemy.HitPoint); break;
            case IntentType.Heal: enemy.Heal(Mathf.RoundToInt(enemy.MaxHp * healRatio)); yield return new WaitForSeconds(0.4f); break;
        }
        if (!player.IsDead) PickNext();
    }
}

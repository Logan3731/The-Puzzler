using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 한 판(런)의 흐름: 스테이지 표시, 플레이어 HP·경험치 바, 레벨업 → 능력 3중 택1, 스테이지 클리어 → 달리기, 게임 오버.
/// 게임 오버에는 이어하기가 없고, 화면을 누르면 스테이지 1부터 (레벨과 스킬도 모두 잃고) 새로 시작한다.
/// </summary>
public class RunManager : MonoBehaviour
{
    public bool GameOver { get; private set; }
    public bool CanPlay => !GameOver && player != null && !player.IsDead && (enemy == null || enemy.IsAlive) && !reward.IsOpen;

    Enemy enemy;
    Player player;
    PlayerSkills skills;
    PlayerLevel level;
    BoardManager board;
    RewardScreen reward;
    Text stageText, hpText, hpDeltaText, centerText, skillsText, xpText;
    RectTransform hpFill, xpFill;
    bool processingPicks;
    float shownHp, centerHideAt = -1f, deltaAge = 99f, hudPunch;

    const float BarWidth = 560f;
    static readonly Vector2 BarPos = new Vector2(0f, -115f);

    void Awake()
    {
        enemy = FindFirstObjectByType<Enemy>();
        skills = FindFirstObjectByType<PlayerSkills>();
        if (skills == null) skills = gameObject.AddComponent<PlayerSkills>();
        level = FindFirstObjectByType<PlayerLevel>();
        if (level == null) level = gameObject.AddComponent<PlayerLevel>();
        player = FindFirstObjectByType<Player>();
        if (player == null) player = gameObject.AddComponent<Player>();
        board = FindFirstObjectByType<BoardManager>();
        reward = FindFirstObjectByType<RewardScreen>();
        if (reward == null) reward = gameObject.AddComponent<RewardScreen>();
        if (FindFirstObjectByType<SkillBar>() == null) gameObject.AddComponent<SkillBar>();
        if (FindFirstObjectByType<ManaCrystal>() == null) gameObject.AddComponent<ManaCrystal>();

        // UI 버튼(보상 카드, 스킬 버튼)을 누를 수 있게 새 Input System용 EventSystem을 만든다
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        BuildHud();
    }

    void OnEnable()
    {
        player.Changed += OnPlayerChanged;
        player.Died += OnPlayerDied;
        skills.Changed += RefreshHud;
        level.Changed += RefreshHud;
        level.LeveledUp += OnLeveledUp;
        if (enemy == null) return;
        enemy.Defeated += OnEnemyDefeated;
        enemy.Respawned += OnEnemyRespawned;
    }

    void OnDisable()
    {
        player.Changed -= OnPlayerChanged;
        player.Died -= OnPlayerDied;
        skills.Changed -= RefreshHud;
        level.Changed -= RefreshHud;
        level.LeveledUp -= OnLeveledUp;
        if (enemy == null) return;
        enemy.Defeated -= OnEnemyDefeated;
        enemy.Respawned -= OnEnemyRespawned;
    }

    void Start()
    {
        shownHp = player.Hp;
        RefreshHud();
    }

    void BuildHud()
    {
        var canvas = UiUtil.CreateCanvas("HudCanvas", transform, 5);
        // 가로 화면: 오른쪽 절반 위 = 스테이지, 오른쪽 절반 아래 = 플레이어 HP·스킬
        stageText = UiUtil.CreateText(canvas.transform, "StageText", new Vector2(0.75f, 1f), new Vector2(0f, -50f), 52);
        stageText.rectTransform.sizeDelta = new Vector2(400f, 100f);

        var bar = new GameObject("PlayerHpBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(canvas.transform, false);
        var barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.75f, 1f);
        barRect.sizeDelta = new Vector2(BarWidth + 12f, 46f);
        barRect.anchoredPosition = BarPos;
        bar.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.12f, 0.9f);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bar.transform, false);
        hpFill = fill.GetComponent<RectTransform>();
        hpFill.anchorMin = hpFill.anchorMax = new Vector2(0f, 0.5f);
        hpFill.pivot = new Vector2(0f, 0.5f);
        hpFill.anchoredPosition = new Vector2(6f, 0f);
        hpFill.sizeDelta = new Vector2(BarWidth, 34f);
        fill.GetComponent<Image>().color = new Color(0.35f, 0.85f, 0.45f);

        hpText = UiUtil.CreateText(bar.transform, "HpText", new Vector2(0.5f, 0.5f), Vector2.zero, 34);
        hpDeltaText = UiUtil.CreateText(bar.transform, "HpDelta", new Vector2(1f, 0.5f), new Vector2(70f, 0f), 44);
        hpDeltaText.rectTransform.sizeDelta = new Vector2(200f, 80f);
        hpDeltaText.enabled = false;

        // 경험치 바 (HP 바 바로 아래)
        var xpBar = new GameObject("PlayerXpBar", typeof(RectTransform), typeof(Image));
        xpBar.transform.SetParent(canvas.transform, false);
        var xpRect = xpBar.GetComponent<RectTransform>();
        xpRect.anchorMin = xpRect.anchorMax = new Vector2(0.75f, 1f);
        xpRect.sizeDelta = new Vector2(BarWidth + 12f, 30f);
        xpRect.anchoredPosition = BarPos + new Vector2(0f, -42f);
        xpBar.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.12f, 0.9f);

        var xpFillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        xpFillGo.transform.SetParent(xpBar.transform, false);
        xpFill = xpFillGo.GetComponent<RectTransform>();
        xpFill.anchorMin = xpFill.anchorMax = new Vector2(0f, 0.5f);
        xpFill.pivot = new Vector2(0f, 0.5f);
        xpFill.anchoredPosition = new Vector2(6f, 0f);
        xpFill.sizeDelta = new Vector2(0f, 20f);
        xpFillGo.GetComponent<Image>().color = new Color(1f, 0.8f, 0.25f);
        xpText = UiUtil.CreateText(xpBar.transform, "XpText", new Vector2(0.5f, 0.5f), Vector2.zero, 24);

        // 얻은 스킬 목록 (오른쪽 맨 아래)
        skillsText = UiUtil.CreateText(canvas.transform, "SkillsText", new Vector2(0.75f, 0f), new Vector2(0f, 20f), 26, FontStyle.Normal);
        skillsText.alignment = TextAnchor.LowerCenter;
        skillsText.rectTransform.pivot = new Vector2(0.5f, 0f);
        skillsText.rectTransform.sizeDelta = new Vector2(900f, 90f);
        skillsText.horizontalOverflow = HorizontalWrapMode.Wrap;

        centerText = UiUtil.CreateText(canvas.transform, "CenterText", new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), 110);
        centerText.enabled = false;
    }

    void OnPlayerChanged(int delta)
    {
        if (delta == 0) return;
        hpDeltaText.text = delta > 0 ? $"+{delta}" : delta.ToString();
        hpDeltaText.color = delta > 0 ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.3f);
        hpDeltaText.enabled = true;
        deltaAge = 0f;
        if (delta < 0) hudPunch = 1f;
        RefreshHud();
    }

    void OnPlayerDied()
    {
        GameOver = true;
        ShowCenter("GAME OVER\n<size=60>화면을 누르면 처음부터</size>", new Color(1f, 0.35f, 0.35f), -1f);
    }

    void OnEnemyDefeated()
    {
        // 보스를 쓰러뜨리면 챕터 클리어 (다음 챕터는 미정 → 처음부터)
        if (enemy.CurrentRank == Enemy.Rank.Boss)
        {
            GameOver = true;
            ShowCenter("CHAPTER 1 CLEAR!\n<size=56>여행의 시작 완료 · 화면을 누르면 처음부터</size>", new Color(1f, 0.85f, 0.3f), -1f);
            Sfx.Play(SfxType.Chime, 1f, 1f);
            ScreenFlash.Flash(new Color(1f, 0.9f, 0.5f), 0.6f, 0.8f);
            return;
        }

        // 원 턴 킬: 스테이지 첫 턴(또는 턴을 쓰기 전)에 쓰러뜨리면 보너스 선택 +1
        if (board != null && board.TurnInStage <= 1)
        {
            level.GrantBonusPick();
            ShowCenter("ONE TURN KILL!\n<size=56>보너스 선택 +1</size>", new Color(1f, 0.6f, 1f), 1.4f);
            Sfx.Play(SfxType.Chime, 1.7f, 0.9f);
        }
        else
        {
            ShowCenter("STAGE CLEAR!", new Color(1f, 0.85f, 0.2f), 1.2f);
        }
        StartCoroutine(RewardFlow());
    }

    void OnLeveledUp(int newLevel)
    {
        ShowCenter($"LEVEL UP!\n<size=56>Lv.{newLevel}</size>", new Color(1f, 0.85f, 0.3f), 1f);
        Sfx.Play(SfxType.Chime, 1.4f, 0.8f);
        ScreenFlash.Flash(new Color(1f, 0.85f, 0.4f), 0.2f, 0.25f);
    }

    /// <summary>쌓인 레벨업만큼 능력을 고르게 한다 (턴이 끝날 때, 스테이지를 넘어가기 전에 부른다).</summary>
    public IEnumerator ProcessLevelUps()
    {
        while (processingPicks) yield return null; // 다른 곳에서 이미 고르는 중이면 끝날 때까지 기다린다
        processingPicks = true;
        while (level.PendingPicks > 0 && !GameOver && !player.IsDead)
        {
            yield return new WaitForSeconds(0.3f);
            SkillId? picked = null;
            string heading = level.PendingPicks > 1 ? $"LEVEL UP! 능력을 고르세요 (남은 선택 {level.PendingPicks})" : "LEVEL UP! 능력을 하나 고르세요";
            reward.Show(RewardScreen.Roll(skills, player), skills, id => picked = id, heading);
            while (picked == null && !GameOver) yield return null;
            if (picked == null) break;
            skills.Grant(picked.Value, player);
            level.ConsumePick();
        }
        processingPicks = false;
    }

    IEnumerator RewardFlow()
    {
        yield return new WaitForSeconds(1.2f);
        while (!enemy.WaitingForNext) yield return null;
        // 적이 쏜 마지막 공격 등으로 이미 게임 오버면 끝
        if (GameOver) yield break;

        // 이번 스테이지에서 쌓인 레벨업을 먼저 고른다
        yield return ProcessLevelUps();
        if (GameOver) yield break;

        // 다음 몬스터를 향해 달려간다 (길이 다가오는 연출) → 몬스터가 멀리서 다가온다
        var road = FindFirstObjectByType<RoadScene>();
        if (road != null) yield return road.Run(1.5f);
        enemy.ContinueToNext();
    }

    void OnEnemyRespawned()
    {
        player.HealForClear();
        RefreshHud();
    }

    void Restart()
    {
        StopAllCoroutines();
        reward.Hide();
        processingPicks = false;
        GameOver = false;
        centerText.enabled = false;
        level.ResetRun();
        skills.ResetRun();
        player.ResetRun();
        if (enemy != null) enemy.ResetRun();
        if (board != null) board.ResetRun();
        RefreshHud();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (centerHideAt > 0f && Time.unscaledTime >= centerHideAt)
        {
            centerText.enabled = false;
            centerHideAt = -1f;
        }

        if (GameOver)
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasReleasedThisFrame) Restart();
        }

        shownHp = Mathf.MoveTowards(shownHp, player.Hp, dt * player.maxHp * 0.8f);
        float ratio = player.maxHp > 0 ? shownHp / player.maxHp : 0f;
        hpFill.sizeDelta = new Vector2(BarWidth * ratio, hpFill.sizeDelta.y);
        hpFill.GetComponent<Image>().color = ratio <= 0.3f ? new Color(1f, 0.35f, 0.3f) : new Color(0.35f, 0.85f, 0.45f);
        float xpRatio = Mathf.Clamp01(level.Xp / (float)level.XpToNext);
        xpFill.sizeDelta = new Vector2(Mathf.MoveTowards(xpFill.sizeDelta.x, BarWidth * xpRatio, dt * BarWidth * 2f), xpFill.sizeDelta.y);

        var barRect = (RectTransform)hpFill.parent;
        barRect.localScale = Vector3.one * (1f + 0.15f * hudPunch);
        barRect.anchoredPosition = BarPos + new Vector2(hudPunch > 0f ? Random.Range(-8f, 8f) * hudPunch : 0f, 0f);
        hudPunch = Mathf.Max(0f, hudPunch - dt * 3f);

        deltaAge += dt;
        if (hpDeltaText.enabled)
        {
            var c = hpDeltaText.color;
            c.a = Mathf.Clamp01(1.2f - deltaAge);
            hpDeltaText.color = c;
            if (deltaAge > 1.2f) hpDeltaText.enabled = false;
        }
    }

    void ShowCenter(string message, Color color, float duration)
    {
        centerText.text = message;
        centerText.color = color;
        centerText.enabled = true;
        centerHideAt = duration > 0f ? Time.unscaledTime + duration : -1f;
    }

    void RefreshHud()
    {
        string rank = enemy == null ? "" : enemy.CurrentRank == Enemy.Rank.Boss ? "  <color=#ff5050>BOSS</color>" : enemy.CurrentRank == Enemy.Rank.Elite ? "  <color=#ffa040>ELITE</color>" : "";
        stageText.text = $"STAGE {(enemy != null ? enemy.Level : 1)} / 10{rank}";
        xpText.text = $"Lv.{level.Level}   EXP {level.Xp} / {level.XpToNext}";
        hpText.text = player.Shield > 0f ? $"HP {player.Hp} / {player.maxHp}  방패 {Mathf.RoundToInt(player.Shield * 100f)}%" : $"HP {player.Hp} / {player.maxHp}";

        var sb = new StringBuilder();
        foreach (var def in SkillCatalog.All)
        {
            int l = skills.Level(def.id);
            if (l <= 0) continue;
            if (sb.Length > 0) sb.Append("  ·  ");
            sb.Append(def.maxLevel > 1 ? $"{def.name} {l}" : def.name);
        }
        skillsText.text = sb.ToString();
    }
}

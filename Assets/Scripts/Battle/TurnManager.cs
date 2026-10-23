using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 턴 제한: 매치에 성공한 스왑만 1턴을 쓴다. 적을 잡으면 턴이 다시 채워지고,
/// 턴을 다 쓰고도 적이 살아 있으면 실패 → 화면을 누르면 재도전.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public int turnsPerStage = 20;
    public int warningTurns = 3;

    public int TurnsLeft { get; private set; }
    public bool Failed { get; private set; }
    public bool CanPlay => !Failed && TurnsLeft > 0;

    Enemy enemy;
    Text hudText, centerText;
    float centerHideAt = -1f;

    void Awake()
    {
        enemy = FindFirstObjectByType<Enemy>();
        var canvas = UiUtil.CreateCanvas("HudCanvas", transform, 5);
        hudText = UiUtil.CreateText(canvas.transform, "TurnText", new Vector2(0.5f, 1f), new Vector2(0f, -90f), 64);
        centerText = UiUtil.CreateText(canvas.transform, "CenterText", new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), 110);
        centerText.enabled = false;
        TurnsLeft = turnsPerStage;
    }

    void OnEnable()
    {
        if (enemy == null) return;
        enemy.Defeated += OnEnemyDefeated;
        enemy.Respawned += OnEnemyRespawned;
    }

    void OnDisable()
    {
        if (enemy == null) return;
        enemy.Defeated -= OnEnemyDefeated;
        enemy.Respawned -= OnEnemyRespawned;
    }

    void Start() => RefreshHud();

    public void ConsumeTurn()
    {
        TurnsLeft = Mathf.Max(0, TurnsLeft - 1);
        RefreshHud();
    }

    /// <summary>한 턴의 연쇄가 모두 끝난 뒤 호출.</summary>
    public void EndTurn()
    {
        if (TurnsLeft > 0 || enemy == null || !enemy.IsAlive) return;
        Failed = true;
        ShowCenter("OUT OF TURNS\n<size=60>Tap to retry</size>", new Color(1f, 0.35f, 0.35f), -1f);
    }

    void OnEnemyDefeated() => ShowCenter("STAGE CLEAR!", new Color(1f, 0.85f, 0.2f), 1.5f);

    void OnEnemyRespawned()
    {
        TurnsLeft = turnsPerStage;
        RefreshHud();
    }

    void Update()
    {
        if (centerHideAt > 0f && Time.unscaledTime >= centerHideAt)
        {
            centerText.enabled = false;
            centerHideAt = -1f;
        }

        if (Failed)
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasReleasedThisFrame) Retry();
        }

        if (TurnsLeft <= warningTurns && !Failed)
        {
            float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 10f);
            hudText.rectTransform.localScale = Vector3.one * pulse;
        }
        else
        {
            hudText.rectTransform.localScale = Vector3.one;
        }
    }

    void Retry()
    {
        Failed = false;
        TurnsLeft = turnsPerStage;
        enemy?.RestoreHp();
        centerText.enabled = false;
        RefreshHud();
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
        int stage = enemy != null ? enemy.Level : 1;
        hudText.text = $"STAGE {stage}      TURN {TurnsLeft}";
        hudText.color = TurnsLeft <= warningTurns ? new Color(1f, 0.35f, 0.35f) : Color.white;
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// 궁수 동료 (7콤보+): 보드 오른쪽 아래에서 화살 3발이 블록 3개를 꿰뚫고,
/// 마지막 큰 화살이 적에게 꽂혀 고정 데미지를 준다.
/// </summary>
public class ArcherVolley : MonoBehaviour
{
    [Tooltip("화살 프리팹 (VARCO 3D 모델). 비어 있으면 임시 화살을 만든다. 화살촉이 +Y 방향이어야 한다.")]
    public GameObject arrowPrefab;
    public int hitCount = 3;
    public Color color = new Color(0.95f, 0.85f, 0.4f);
    public float flySpeed = 40f;

    public IEnumerator Play(BoardManager board, ComboSystem combo)
    {
        var cells = board.GetRandomOccupiedCells(hitCount);
        if (cells.Count == 0) yield break;
        int firedCombo = combo.Combo;

        Vector3 origin = board.CellToWorld(board.width - 1, 0) + new Vector3(0.6f, -0.7f, -1.5f);
        FxRing.Spawn(origin, color, 1.2f, 0.3f);

        foreach (var cell in cells)
        {
            var arrow = CreateArrow(origin, 1f);
            Sfx.Play(SfxType.Whoosh, 1.6f, 0.6f);
            yield return Fly(arrow, board.CellToWorld(cell) + Vector3.back * 1.5f);
            board.BreakBlocks(new System.Collections.Generic.List<Vector2Int> { cell }, 2f); // 동료가 부순 블록도 데미지, 특수 블록이면 발동
            Sfx.Play(SfxType.Hit, 1.5f, 0.7f);
            combo.cameraShake?.Shake(0.2f, 0.1f);
            Destroy(arrow.gameObject);
        }

        // 마지막 큰 화살이 적에게 날아가는 구간은 보드 진행을 막지 않는다.
        combo.PendingHits++;
        StartCoroutine(HitEnemy(origin, combo, firedCombo));
    }

    IEnumerator HitEnemy(Vector3 origin, ComboSystem combo, int firedCombo)
    {
        if (combo.enemy != null)
        {
            var arrow = CreateArrow(origin, 1.8f);
            Sfx.Play(SfxType.Whoosh, 1.1f, 0.9f);
            Vector3 to = combo.enemy.HitPoint + Vector3.back * 1.5f;
            yield return Fly(arrow, to);
            Debris.Burst(to, color, 26, 10f, 0.2f);
            FxRing.Spawn(to, color, 3.2f, 0.35f);
            Sfx.Play(SfxType.Impact, 1.2f, 1f);
            combo.cameraShake?.Shake(0.4f, 0.22f);
            combo.hitStop?.Stop(0.07f);
            combo.CompanionHit(combo.ArcherDamage, "ARCHER", color, firedCombo);
            Destroy(arrow.gameObject);
        }
        combo.PendingHits--;
    }

    IEnumerator Fly(Transform arrow, Vector3 target)
    {
        Vector3 dir = (target - arrow.position).normalized;
        arrow.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        while ((arrow.position - target).sqrMagnitude > 0.0004f)
        {
            arrow.position = Vector3.MoveTowards(arrow.position, target, flySpeed * Time.deltaTime);
            yield return null;
        }
    }

    Transform CreateArrow(Vector3 position, float size)
    {
        Transform root;
        if (arrowPrefab != null)
        {
            root = Instantiate(arrowPrefab, position, Quaternion.identity).transform;
        }
        else
        {
            root = new GameObject("Arrow").transform;
            root.position = position;
            MaterialUtil.Primitive(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.07f, 0.9f, 0.07f), new Color(0.6f, 0.4f, 0.2f));
            MaterialUtil.Primitive(PrimitiveType.Cube, root, new Vector3(0f, 0.5f, 0f), new Vector3(0.18f, 0.18f, 0.08f), color, 1.5f);
        }
        root.localScale = Vector3.one * size;
        Fx.AddTrail(root, Vector3.zero, 0.15f, 0.12f, Color.white, color);
        return root;
    }
}

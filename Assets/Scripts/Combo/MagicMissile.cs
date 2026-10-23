using System.Collections;
using UnityEngine;

/// <summary>
/// 마법사 동료 (3콤보+): 보드 아래 구석의 마법진에서 마법미사일이 휘어 날아와
/// 블록 1개를 터뜨리고, 그대로 적에게 꽂혀 고정 데미지를 준다.
/// </summary>
public class MagicMissile : MonoBehaviour
{
    [Tooltip("미사일 프리팹 (VARCO 3D 모델). 비어 있으면 빛나는 구체를 만든다.")]
    public GameObject missilePrefab;
    public Color color = new Color(0.75f, 0.4f, 1f);
    public float toBlockTime = 0.28f;
    public float toEnemyTime = 0.3f;
    public float bend = 2f;

    public IEnumerator Play(BoardManager board, ComboSystem combo)
    {
        var cells = board.GetRandomOccupiedCells(1);
        if (cells.Count == 0) yield break;
        var cell = cells[0];
        int firedCombo = combo.Combo;

        Vector3 origin = board.CellToWorld(0, 0) + new Vector3(-0.2f, -0.7f, -1.5f);
        FxRing.Spawn(origin, color);
        yield return new WaitForSeconds(0.08f);

        var missile = CreateMissile(origin);
        Sfx.Play(SfxType.Whoosh, 1.3f, 0.7f);
        Vector3 target = board.CellToWorld(cell) + Vector3.back * 1.5f;
        yield return Fly(missile, origin, target, toBlockTime, bend);

        board.BreakBlocks(new System.Collections.Generic.List<Vector2Int> { cell }, 2f); // 동료가 부순 블록도 데미지, 특수 블록이면 발동
        Debris.Burst(target, color, 14, 8f);
        FxRing.Spawn(target, color, 1.8f, 0.25f);
        Sfx.Play(SfxType.Hit, 1.4f, 0.8f);
        combo.cameraShake?.Shake(0.2f, 0.12f);

        // 적에게 날아가는 구간은 보드 진행을 막지 않는다.
        combo.PendingHits++;
        StartCoroutine(HitEnemy(missile, combo, firedCombo));
    }

    IEnumerator HitEnemy(Transform missile, ComboSystem combo, int firedCombo)
    {
        if (combo.enemy != null)
        {
            Vector3 from = missile.position;
            Vector3 to = combo.enemy.HitPoint + Vector3.back * 1.5f;
            yield return Fly(missile, from, to, toEnemyTime, -bend);
            Debris.Burst(to, color, 20, 9f, 0.2f);
            FxRing.Spawn(to, color, 2.6f, 0.3f);
            Sfx.Play(SfxType.Impact, 1.3f, 0.8f);
            combo.cameraShake?.Shake(0.25f, 0.15f);
            combo.CompanionHit(combo.MageDamage, "MAGE", color, firedCombo);
        }
        Destroy(missile.gameObject);
        combo.PendingHits--;
    }

    static IEnumerator Fly(Transform m, Vector3 from, Vector3 to, float duration, float bend)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            m.position = Fx.Curve(from, to, bend, k * k);
            yield return null;
        }
        m.position = to;
    }

    Transform CreateMissile(Vector3 position)
    {
        Transform root;
        if (missilePrefab != null)
        {
            root = Instantiate(missilePrefab, position, Quaternion.identity).transform;
        }
        else
        {
            root = new GameObject("MagicMissile").transform;
            root.position = position;
            var art = ArtLibrary.Get("missile");
            if (art != null)
            {
                ArtLibrary.Quad(art, root, 0.9f);
            }
            else
            {
                MaterialUtil.Primitive(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.45f, color, 2.5f);
                MaterialUtil.Primitive(PrimitiveType.Sphere, root, Vector3.back * 0.1f, Vector3.one * 0.25f, Color.white, 2f);
            }
        }
        Fx.AddTrail(root, Vector3.zero, 0.4f, 0.2f, Color.white, color);
        return root;
    }
}

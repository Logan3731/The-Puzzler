using System.Collections;
using UnityEngine;

/// <summary>
/// 전사 동료 (5콤보+): 화면 옆에서 검이 회전하며 날아와 블록 2개를 베고,
/// 그대로 적에게 꽂혀 고정 데미지를 준다.
/// </summary>
public class WeaponStrike : MonoBehaviour
{
    [Tooltip("무기 프리팹 (VARCO 3D 모델). 비어 있으면 임시 검을 만든다. 칼날이 +Y 방향이어야 한다.")]
    public GameObject weaponPrefab;
    public int hitCount = 2;
    public Color color = new Color(1f, 0.55f, 0.1f);
    public float flySpeed = 30f;
    public float spinSpeed = 1080f;
    public float hitStopTime = 0.06f;

    public IEnumerator Play(BoardManager board, ComboSystem combo)
    {
        var cells = board.GetRandomOccupiedCells(hitCount);
        if (cells.Count == 0) yield break;
        int firedCombo = combo.Combo;

        bool fromLeft = Random.value < 0.5f;
        cells.Sort((a, b) => fromLeft ? a.x.CompareTo(b.x) : b.x.CompareTo(a.x));

        float side = fromLeft ? -1f : 1f;
        float edge = board.HalfWidth + 3f;
        Vector3 first = board.CellToWorld(cells[0]);
        var weapon = CreateWeapon(new Vector3(board.transform.position.x + side * edge, first.y + 1.5f, -1.5f));
        Sfx.Play(SfxType.Whoosh, 0.9f, 0.9f);

        foreach (var cell in cells)
        {
            yield return Fly(weapon, board.CellToWorld(cell) + Vector3.back * 1.5f, -side);
            board.DestroyBlock(cell, 2.2f + firedCombo * 0.1f);
            Sfx.Play(SfxType.Impact, 1.5f, 0.7f);
            combo.cameraShake?.Shake(0.35f, 0.2f);
            combo.hitStop?.Stop(hitStopTime);
        }
        Sfx.Play(SfxType.Whoosh, 0.8f, 0.8f);

        // 적에게 꽂히는 구간은 보드 진행을 막지 않는다.
        combo.PendingHits++;
        StartCoroutine(HitEnemy(weapon, combo, firedCombo, -side));
    }

    IEnumerator HitEnemy(Transform weapon, ComboSystem combo, int firedCombo, float spinDir)
    {
        if (combo.enemy != null)
        {
            Vector3 to = combo.enemy.HitPoint + Vector3.back * 1.5f;
            yield return Fly(weapon, to, spinDir);
            Debris.Burst(to, color, 30, 11f, 0.24f);
            FxRing.Spawn(to, color, 3.5f, 0.35f);
            Sfx.Play(SfxType.Impact, 0.9f, 1f);
            ScreenFlash.Flash(new Color(1f, 0.85f, 0.6f), 0.45f, 0.3f);
            combo.cameraShake?.Shake(0.45f, 0.25f);
            combo.cameraShake?.Punch(0.06f);
            combo.hitStop?.Stop(hitStopTime * 1.5f);
            combo.CompanionHit(combo.warriorDamage, "WARRIOR", color, firedCombo);
        }
        Destroy(weapon.gameObject);
        combo.PendingHits--;
    }

    IEnumerator Fly(Transform w, Vector3 target, float spinDir)
    {
        while ((w.position - target).sqrMagnitude > 0.0004f)
        {
            w.position = Vector3.MoveTowards(w.position, target, flySpeed * Time.deltaTime);
            w.Rotate(0f, 0f, spinSpeed * spinDir * Time.deltaTime, Space.World);
            yield return null;
        }
    }

    Transform CreateWeapon(Vector3 position)
    {
        Transform root;
        if (weaponPrefab != null)
        {
            root = Instantiate(weaponPrefab, position, Quaternion.identity).transform;
        }
        else
        {
            root = new GameObject("Weapon_Sword").transform;
            root.position = position;
            root.localScale = Vector3.one * 1.3f;
            MaterialUtil.Primitive(PrimitiveType.Cube, root, new Vector3(0f, 0.45f, 0f), new Vector3(0.2f, 1.3f, 0.06f), new Color(0.85f, 0.95f, 1f), 0.6f);
            MaterialUtil.Primitive(PrimitiveType.Cube, root, new Vector3(0f, -0.25f, 0f), new Vector3(0.65f, 0.12f, 0.12f), new Color(1f, 0.8f, 0.2f), 0.4f);
            MaterialUtil.Primitive(PrimitiveType.Cube, root, new Vector3(0f, -0.5f, 0f), new Vector3(0.12f, 0.4f, 0.1f), new Color(0.45f, 0.25f, 0.12f));
        }
        Fx.AddTrail(root, new Vector3(0f, 1.05f, 0f), 0.3f, 0.15f, Color.white, new Color(0.5f, 0.9f, 1f));
        return root;
    }
}

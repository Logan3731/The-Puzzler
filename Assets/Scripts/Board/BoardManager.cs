using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

/// <summary>
/// 8x8 보드: 생성, 입력(마우스 드래그·클릭 / 터치 스와이프·탭), 스왑, 매치, 낙하, 리필, 연쇄.
/// </summary>
public class BoardManager : MonoBehaviour
{
    [Header("Board")]
    public int width = 8;
    public int height = 8;
    public float cellSize = 1f;

    [Header("Blocks")]
    [Tooltip("타입별 블록 프리팹 (VARCO 3D 모델). 비어 있으면 임시 도형을 쓴다.")]
    public GameObject[] blockPrefabs;
    public Color[] blockColors =
    {
        new Color(1f, 0.25f, 0.3f),   // 빨강
        new Color(0.25f, 0.5f, 1f),   // 파랑
        new Color(0.3f, 0.9f, 0.4f),  // 초록
        new Color(1f, 0.85f, 0.2f),   // 노랑
        new Color(0.7f, 0.35f, 1f),   // 보라
    };
    [Range(0.5f, 1f)] public float blockScale = 0.8f;

    [Header("Timing")]
    public float swapTime = 0.12f;
    [Tooltip("1칸 떨어지는 시간. n칸은 이 값 × √n (중력처럼 가속)")]
    public float fallTime = 0.11f;
    public float popTime = 0.16f;
    [Tooltip("블록이 터진 뒤 다음 동작까지 기다리는 시간")]
    public float afterPopDelay = 0.06f;

    [Header("Input")]
    [Tooltip("스와이프로 인정할 최소 드래그 거리 (칸 단위)")]
    public float dragThreshold = 0.35f;

    [Header("Layout")]
    [Tooltip("보드 위쪽에 적이 들어갈 공간 (칸 단위)")]
    public float enemySpace = 4.5f;
    [Tooltip("화면 맨 위 턴/스테이지 표시용 여백 (칸 단위)")]
    public float hudSpace = 1.5f;
    public Color backgroundColor = new Color(0.08f, 0.06f, 0.14f);

    public ComboSystem combo;
    public Enemy enemy;
    public TurnManager turns;

    public float HalfWidth => width * cellSize * 0.5f;
    public bool IsBusy => busy;

    Block[,] grid;
    bool busy;
    Camera cam;
    float lastAspect;

    bool pressing, swiped;
    Vector2 pressScreen;
    Vector2Int pressCell;
    Vector2Int? selectedCell;

    static readonly PrimitiveType[] shapes =
    {
        PrimitiveType.Sphere, PrimitiveType.Cube, PrimitiveType.Cylinder, PrimitiveType.Capsule, PrimitiveType.Cube,
    };

    void Awake()
    {
        if (combo == null) combo = FindFirstObjectByType<ComboSystem>();
        if (combo == null) combo = gameObject.AddComponent<ComboSystem>();
        if (enemy == null) enemy = FindFirstObjectByType<Enemy>();
        if (turns == null) turns = FindFirstObjectByType<TurnManager>();
        if (turns == null) turns = gameObject.AddComponent<TurnManager>();
    }

    void Start()
    {
        cam = Camera.main;
        FitCamera();
        PlaceEnemy();
        BuildBackground();
        grid = new Block[width, height];
        StartCoroutine(FillBoard());
    }

    void Update()
    {
        if (cam != null && !Mathf.Approximately(cam.aspect, lastAspect)) FitCamera();
        HandleInput();
    }

    // ───────────────────────── 레이아웃 ─────────────────────────

    public Vector3 CellToWorld(Vector2Int c) => CellToWorld(c.x, c.y);

    public Vector3 CellToWorld(int x, int y) =>
        transform.position + new Vector3((x - (width - 1) * 0.5f) * cellSize, (y - (height - 1) * 0.5f) * cellSize, 0f);

    bool InBounds(Vector2Int c) => c.x >= 0 && c.x < width && c.y >= 0 && c.y < height;

    void FitCamera()
    {
        if (cam == null) return;
        lastAspect = cam.aspect;
        float boardH = height * cellSize;
        float topSpace = (enemySpace + hudSpace) * cellSize;
        float totalH = boardH + topSpace + cellSize;
        float totalW = width * cellSize + cellSize;
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(totalH * 0.5f, totalW / cam.aspect * 0.5f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        cam.transform.SetPositionAndRotation(
            transform.position + new Vector3(0f, topSpace * 0.5f, -20f), Quaternion.identity);
    }

    void PlaceEnemy()
    {
        if (enemy == null) return;
        enemy.transform.position = transform.position + Vector3.up * (height * cellSize * 0.5f + enemySpace * cellSize * 0.4f);
    }

    void BuildBackground()
    {
        var root = new GameObject("BoardBackground").transform;
        root.SetParent(transform, false);
        var dark = new Color(0.16f, 0.13f, 0.24f);
        var light = new Color(0.2f, 0.17f, 0.3f);
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            var tile = MaterialUtil.Primitive(PrimitiveType.Cube, root, Vector3.zero,
                new Vector3(cellSize * 0.96f, cellSize * 0.96f, 0.1f), (x + y) % 2 == 0 ? dark : light);
            tile.transform.position = CellToWorld(x, y) + new Vector3(0f, 0f, 0.6f);
        }
    }

    // ───────────────────────── 블록 생성 ─────────────────────────

    int TypeCount => blockColors.Length;

    Block SpawnBlock(int type, Vector2Int cell, Vector3 position)
    {
        GameObject go;
        var prefab = blockPrefabs != null && type < blockPrefabs.Length ? blockPrefabs[type] : null;
        if (prefab != null)
        {
            go = Instantiate(prefab, position, prefab.transform.rotation, transform);
        }
        else
        {
            var shape = shapes[type % shapes.Length];
            go = MaterialUtil.Primitive(shape, transform, Vector3.zero, Vector3.one * (cellSize * blockScale),
                blockColors[type], 0.25f);
            go.transform.position = position;
            ShapePlaceholder(go.transform, type);
        }

        go.name = $"Block_{type}";
        var block = go.GetComponent<Block>();
        if (block == null) block = go.AddComponent<Block>();
        block.Init(type, blockColors[type], cell);
        grid[cell.x, cell.y] = block;
        return block;
    }

    void ShapePlaceholder(Transform t, int type)
    {
        float s = cellSize * blockScale;
        switch (type % shapes.Length)
        {
            case 1: t.localScale = Vector3.one * s * 0.85f; break;                                  // 큐브
            case 2: t.localScale = new Vector3(s, s * 0.25f, s); t.rotation = Quaternion.Euler(90f, 0f, 0f); break; // 원반
            case 3: t.localScale = new Vector3(s * 0.7f, s * 0.5f, s * 0.7f); break;               // 캡슐
            case 4: t.localScale = Vector3.one * s * 0.68f; t.rotation = Quaternion.Euler(0f, 0f, 45f); break;     // 다이아
        }
    }

    int[,] GetTypes()
    {
        var t = new int[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            t[x, y] = grid[x, y] != null ? grid[x, y].Type : -1;
        return t;
    }

    int[,] GenerateTypes()
    {
        var t = new int[width, height];
        do
        {
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int type;
                do type = Random.Range(0, TypeCount);
                while ((x >= 2 && t[x - 1, y] == type && t[x - 2, y] == type) ||
                       (y >= 2 && t[x, y - 1] == type && t[x, y - 2] == type));
                t[x, y] = type;
            }
        } while (!MatchFinder.HasPossibleMove(t));
        return t;
    }

    IEnumerator FillBoard()
    {
        busy = true;
        var types = GenerateTypes();
        var moves = new List<IEnumerator>();
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            var cell = new Vector2Int(x, y);
            var b = SpawnBlock(types[x, y], cell, CellToWorld(x, y + height));
            moves.Add(DelayedFall(b, CellToWorld(cell), FallTime(height), x * 0.03f + y * 0.015f));
        }
        yield return RunAll(moves);
        busy = false;
    }

    IEnumerator DelayedFall(Block b, Vector3 target, float duration, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        yield return b.Fall(target, duration);
    }

    // ───────────────────────── 입력 ─────────────────────────

    void HandleInput()
    {
        if (busy || cam == null || !turns.CanPlay) return;
        var pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 screen = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            pressing = TryGetCell(screen, out pressCell);
            pressScreen = screen;
            swiped = false;
        }
        else if (pressing && !swiped && pointer.press.isPressed)
        {
            Vector2 delta = ScreenToWorld(screen) - ScreenToWorld(pressScreen);
            if (delta.magnitude >= dragThreshold * cellSize)
            {
                swiped = true;
                var dir = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                    ? new Vector2Int(Math.Sign(delta.x), 0)
                    : new Vector2Int(0, Math.Sign(delta.y));
                Deselect();
                var target = pressCell + dir;
                if (InBounds(target)) StartCoroutine(TrySwap(pressCell, target));
            }
        }

        if (pointer.press.wasReleasedThisFrame && pressing)
        {
            pressing = false;
            if (!swiped) HandleTap(pressCell);
        }
    }

    void HandleTap(Vector2Int cell)
    {
        if (selectedCell == null)
        {
            Select(cell);
            return;
        }

        var prev = selectedCell.Value;
        Deselect();
        if (prev == cell) return;

        if (Mathf.Abs(prev.x - cell.x) + Mathf.Abs(prev.y - cell.y) == 1)
            StartCoroutine(TrySwap(prev, cell));
        else
            Select(cell);
    }

    void Select(Vector2Int cell)
    {
        var b = grid[cell.x, cell.y];
        if (b == null) return;
        selectedCell = cell;
        b.SetSelected(true);
    }

    void Deselect()
    {
        if (selectedCell is Vector2Int c && grid[c.x, c.y] != null) grid[c.x, c.y].SetSelected(false);
        selectedCell = null;
    }

    Vector3 ScreenToWorld(Vector2 screen)
    {
        screen = PixelRender.ScreenToCamera(cam, screen); // 픽셀 렌더 중이면 저해상도 좌표로
        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, transform.position.z - cam.transform.position.z));
    }

    bool TryGetCell(Vector2 screen, out Vector2Int cell)
    {
        Vector3 local = ScreenToWorld(screen) - transform.position;
        cell = new Vector2Int(
            Mathf.RoundToInt(local.x / cellSize + (width - 1) * 0.5f),
            Mathf.RoundToInt(local.y / cellSize + (height - 1) * 0.5f));
        return InBounds(cell) && grid[cell.x, cell.y] != null;
    }

    // ───────────────────────── 스왑 & 연쇄 ─────────────────────────

    IEnumerator TrySwap(Vector2Int a, Vector2Int b)
    {
        if (grid[a.x, a.y] == null || grid[b.x, b.y] == null) yield break;
        busy = true;
        yield return SwapBlocks(a, b);

        var matches = MatchFinder.FindMatches(GetTypes());
        if (matches.Count == 0)
        {
            yield return SwapBlocks(a, b); // 매치가 없으면 되돌린다
            busy = false;
            yield break;
        }

        turns.ConsumeTurn();
        yield return ResolveBoard(matches);
        turns.EndTurn();
        busy = false;
    }

    IEnumerator SwapBlocks(Vector2Int a, Vector2Int b)
    {
        var ba = grid[a.x, a.y];
        var bb = grid[b.x, b.y];
        grid[a.x, a.y] = bb;
        grid[b.x, b.y] = ba;
        ba.Cell = b;
        bb.Cell = a;
        Sfx.Play(SfxType.Swap, Random.Range(0.95f, 1.05f), 0.6f);
        yield return RunAll(new List<IEnumerator>
        {
            ba.MoveTo(CellToWorld(b), swapTime),
            bb.MoveTo(CellToWorld(a), swapTime),
        });
    }

    IEnumerator ResolveBoard(HashSet<Vector2Int> matches)
    {
        combo.BeginChain();
        var hits = new List<(Vector3 position, Color color)>();
        while (matches.Count > 0)
        {
            int c = combo.RegisterMatch();
            float strength = 1f + 0.1f * c;
            hits.Clear();
            foreach (var cell in matches)
            {
                var b = grid[cell.x, cell.y];
                if (b != null) hits.Add((b.transform.position, b.Color));
                DestroyBlock(cell, strength);
            }
            // 터진 블록마다 빛구슬이 날아가 적을 때린다
            combo.LaunchOrbs(hits, DamageCalculator.Calculate(matches.Count, c));
            yield return new WaitForSeconds(afterPopDelay);

            // 3콤보+ 마법사, 5콤보+ 전사 (각각 턴당 한 번, 고정 데미지)
            yield return combo.PlayBonuses(this);

            yield return Collapse();
            matches = MatchFinder.FindMatches(GetTypes());
        }
        combo.EndChain();
        while (combo.PendingHits > 0) yield return null; // 적에게 날아가는 동료 공격이 다 맞을 때까지

        if (!MatchFinder.HasPossibleMove(GetTypes())) yield return Reshuffle();
    }

    IEnumerator Collapse()
    {
        var moves = new List<IEnumerator>();
        for (int x = 0; x < width; x++)
        {
            int writeY = 0;
            for (int y = 0; y < height; y++)
            {
                var b = grid[x, y];
                if (b == null) continue;
                if (y != writeY)
                {
                    grid[x, writeY] = b;
                    grid[x, y] = null;
                    b.Cell = new Vector2Int(x, writeY);
                    moves.Add(b.Fall(CellToWorld(x, writeY), FallTime(y - writeY)));
                }
                writeY++;
            }

            for (int y = writeY, k = 0; y < height; y++, k++)
            {
                var cell = new Vector2Int(x, y);
                var b = SpawnBlock(Random.Range(0, TypeCount), cell, CellToWorld(x, height + k));
                moves.Add(b.Fall(CellToWorld(cell), FallTime(height + k - y)));
            }
        }
        yield return RunAll(moves);
    }

    float FallTime(int cells) => fallTime * Mathf.Sqrt(cells);

    IEnumerator Reshuffle()
    {
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            DestroyBlock(new Vector2Int(x, y), 0.5f, debris: false);
        yield return new WaitForSeconds(popTime);
        yield return FillBoard();
    }

    // ───────────────────────── 콤보 시스템에서 쓰는 API ─────────────────────────

    public Block GetBlock(Vector2Int c) => InBounds(c) ? grid[c.x, c.y] : null;

    /// <summary>블록을 즉시 파괴(그리드에서 제거 + 파편 + 팝 애니메이션).</summary>
    public bool DestroyBlock(Vector2Int c, float strength, bool debris = true)
    {
        var b = GetBlock(c);
        if (b == null) return false;
        grid[c.x, c.y] = null;
        if (debris)
        {
            Debris.Burst(b.transform.position, b.Color, Mathf.RoundToInt(9 * strength), 6f * strength);
            // 블록 뒤(타일 앞)에서 퍼지는 충격파
            FxRing.Spawn(new Vector3(b.transform.position.x, b.transform.position.y, transform.position.z + 0.47f), b.Color, 1.1f * cellSize, 0.22f);
        }
        b.StartCoroutine(b.Pop(popTime, strength));
        return true;
    }

    public List<Vector2Int> GetRandomOccupiedCells(int count)
    {
        var cells = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (grid[x, y] != null) cells.Add(new Vector2Int(x, y));

        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }
        if (cells.Count > count) cells.RemoveRange(count, cells.Count - count);
        return cells;
    }

    // ───────────────────────── 유틸 ─────────────────────────

    IEnumerator RunAll(List<IEnumerator> routines)
    {
        int running = routines.Count;
        foreach (var r in routines) StartCoroutine(Track(r, () => running--));
        while (running > 0) yield return null;
    }

    static IEnumerator Track(IEnumerator routine, Action done)
    {
        yield return routine;
        done();
    }
}

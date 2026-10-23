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

    [Header("Layout (가로 16:9: 왼쪽 보드, 오른쪽 길과 몬스터)")]
    [Tooltip("화면 세로 높이 (월드 단위). 픽셀 그림판 360줄에 들어가는 높이")]
    public float viewHeight = 10f;
    [Tooltip("보드와 화면 왼쪽 끝 사이 여백 (칸 단위)")]
    public float boardMargin = 0.5f;
    [Tooltip("몬스터가 서는 높이 (오른쪽 패널 안)")]
    public float enemyY = -0.4f;
    public Color backgroundColor = new Color(0.08f, 0.06f, 0.14f);

    /// <summary>오른쪽 패널(길과 몬스터)의 가운데 x.</summary>
    public float RightPanelX { get; private set; }
    public RoadScene road;

    public ComboSystem combo;
    public Enemy enemy;
    public RunManager run;
    public Player player;
    public EnemyIntent intent;
    public PlayerSkills skills;
    public PlayerLevel level;

    /// <summary>원소 변환·망치처럼 블록을 골라야 하는 액티브 스킬을 쓰는 중이면 그 스킬.</summary>
    public SkillId? Targeting { get; private set; }
    /// <summary>이번 스테이지에서 쓴 턴 수 (원 턴 킬 보너스 판정).</summary>
    public int TurnInStage { get; private set; }
    public event Action<Vector2Int> ConvertTargetChosen;

    public float HalfWidth => width * cellSize * 0.5f;
    public bool IsBusy => busy;

    Block[,] grid;
    bool busy;
    Camera cam;

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
        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null) player = gameObject.AddComponent<Player>();
        if (run == null) run = FindFirstObjectByType<RunManager>();
        if (run == null) run = gameObject.AddComponent<RunManager>();
        if (skills == null) skills = FindFirstObjectByType<PlayerSkills>();
        if (skills == null) skills = gameObject.AddComponent<PlayerSkills>();
        if (level == null) level = FindFirstObjectByType<PlayerLevel>();
        if (level == null) level = gameObject.AddComponent<PlayerLevel>();
        if (intent == null && enemy != null) intent = enemy.GetComponent<EnemyIntent>();
        if (intent == null && enemy != null) intent = enemy.gameObject.AddComponent<EnemyIntent>();
    }

    void OnEnable()
    {
        if (enemy != null) enemy.Respawned += ClearStatuses;
    }

    void OnDisable()
    {
        if (enemy != null) enemy.Respawned -= ClearStatuses;
    }

    void Start()
    {
        cam = Camera.main;
        FitCamera();
        PlaceEnemy();
        BuildRoad();
        BuildBackground();
        grid = new Block[width, height];
        StartCoroutine(FillBoard());
    }

    void Update()
    {
        HandleInput();
    }

    // ───────────────────────── 레이아웃 ─────────────────────────

    public Vector3 CellToWorld(Vector2Int c) => CellToWorld(c.x, c.y);

    public Vector3 CellToWorld(int x, int y) => CellToWorld((float)x, (float)y);

    public Vector3 CellToWorld(float x, float y) =>
        transform.position + new Vector3((x - (width - 1) * 0.5f) * cellSize, (y - (height - 1) * 0.5f) * cellSize, 0f);

    bool InBounds(Vector2Int c) => c.x >= 0 && c.x < width && c.y >= 0 && c.y < height;

    /// <summary>가로 16:9 화면: 보드는 왼쪽 절반, 오른쪽 절반은 길과 몬스터.</summary>
    void FitCamera()
    {
        if (cam == null) return;
        float halfH = viewHeight * 0.5f;
        float halfW = halfH * 16f / 9f;
        cam.orthographic = true;
        cam.orthographicSize = halfH;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;

        // 카메라 왼쪽 끝 + 여백 = 보드 왼쪽 끝
        float camX = transform.position.x - width * cellSize * 0.5f - boardMargin * cellSize + halfW;
        cam.transform.SetPositionAndRotation(new Vector3(camX, transform.position.y, -20f), Quaternion.identity);
        RightPanelX = camX + halfW * 0.5f;
    }

    void PlaceEnemy()
    {
        if (enemy == null) return;
        enemy.transform.position = new Vector3(RightPanelX, transform.position.y + enemyY, 0f);
    }

    void BuildRoad()
    {
        if (road == null) road = FindFirstObjectByType<RoadScene>();
        if (road == null) road = new GameObject("RoadScene").AddComponent<RoadScene>();
        float halfH = viewHeight * 0.5f;
        road.Build(RightPanelX, halfH * 16f / 9f, transform.position.y + halfH);
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
        var art = type < ArtLibrary.ElementArt.Length ? ArtLibrary.Get(ArtLibrary.ElementArt[type]) : null;
        bool textured = false;
        if (prefab != null)
        {
            go = Instantiate(prefab, position, prefab.transform.rotation, transform);
        }
        else if (art != null)
        {
            // VARCO 원소 조각 그림
            go = ArtLibrary.Quad(art, transform, cellSize * 0.92f);
            go.transform.position = position;
            textured = true;
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
        block.Textured = textured;
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
        if (busy || cam == null || !run.CanPlay) return;
        var pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 screen = pointer.position.ReadValue();

        if (Targeting != null)
        {
            HandleTargeting(pointer, screen);
            return;
        }

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

    /// <summary>스킬 대상 고르기: 드래그 없이 블록 하나를 누르면 그 블록에 스킬을 쓴다.</summary>
    void HandleTargeting(Pointer pointer, Vector2 screen)
    {
        if (!pointer.press.wasReleasedThisFrame || !TryGetCell(screen, out var cell)) return;
        var skill = Targeting.Value;
        if (skill == SkillId.ActiveHammer)
        {
            Targeting = null;
            StartCoroutine(UseHammer(cell));
        }
        else if (skill == SkillId.ActiveConvert)
        {
            Targeting = null;
            Select(cell);
            ConvertTargetChosen?.Invoke(cell); // 스킬 바가 원소 고르기를 띄운다
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
        var ba = grid[a.x, a.y];
        var bb = grid[b.x, b.y];
        if (ba == null || bb == null) yield break;
        busy = true;

        // 점액으로 굳은 블록은 움직일 수 없다
        if (ba.Locked || bb.Locked)
        {
            var stuck = ba.Locked ? ba : bb;
            Sfx.Play(SfxType.Hit, 0.6f, 0.5f);
            yield return stuck.Punch(0.12f, 0.15f);
            busy = false;
            yield break;
        }

        yield return SwapBlocks(a, b);

        // 특수 블록끼리 바꾸면 매치가 없어도 터진다 (조합에 따라 더 크게)
        HashSet<Vector2Int> forced = null;
        if (ba.Special != SpecialType.None && bb.Special != SpecialType.None)
        {
            forced = SpecialComboArea(ba, bb, a, b);
            Sfx.Play(SfxType.Chime, 1.6f, 0.8f);
            combo.cameraShake?.Punch(0.05f);
        }

        var runs = MatchFinder.FindRuns(GetTypes());
        if (runs.Count == 0 && forced == null)
        {
            yield return SwapBlocks(a, b); // 매치가 없으면 되돌린다
            busy = false;
            yield break;
        }

        TurnInStage++;
        yield return ResolveBoard(runs, a, b, forced);
        skills.TickCooldowns(); // 액티브 스킬 쿨타임은 플레이어 턴마다 줄어든다

        // 적 차례: 불씨 확인, 행동 예고 카운트, 행동 (공격하면 플레이어 HP가 준다)
        if (intent != null && !player.IsDead) yield return intent.TakeTurn(this, player);

        // 이번 턴에 레벨업했으면 능력을 고른다 (적을 쓰러뜨린 턴이면 RunManager가 스테이지 전환 전에 처리한다)
        if (!player.IsDead && enemy.IsAlive) yield return run.ProcessLevelUps();

        if (!player.IsDead && !MatchFinder.HasPossibleMove(GetTypes(), GetLocks())) yield return Reshuffle();
        busy = false;
    }

    /// <summary>게임 오버 후 새 판: 상태를 지우고 보드를 새로 깐다.</summary>
    public void ResetRun()
    {
        if (busy) return;
        ClearStatuses();
        StartCoroutine(ResetBoard());
    }

    IEnumerator ResetBoard()
    {
        busy = true;
        yield return Reshuffle();
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

    /// <param name="forced">첫 단계에 매치와 상관없이 터뜨릴 칸 (특수 블록끼리 바꾼 경우)</param>
    IEnumerator ResolveBoard(List<MatchFinder.Run> runs, Vector2Int swapA, Vector2Int swapB, HashSet<Vector2Int> forced = null)
    {
        combo.BeginChain();
        var hits = new List<(Vector3 position, Color color, int element)>();
        bool firstStep = true;
        while (runs.Count > 0 || (firstStep && forced != null))
        {
            int c = combo.RegisterMatch();
            float strength = 1f + 0.1f * c;

            var matched = new HashSet<Vector2Int>();
            foreach (var run in runs)
                foreach (var cell in run.cells) matched.Add(cell);
            if (firstStep && forced != null) matched.UnionWith(forced);

            // 특수 블록 생성 (보상으로 해금한 것만). 그 칸의 블록은 터지지 않고 특수 블록이 된다.
            var creates = FindSpecialCreations(runs, firstStep, swapA, swapB);
            if (firstStep && forced != null) creates.RemoveAll(cr => forced.Contains(cr.cell));
            foreach (var (cell, _) in creates) matched.Remove(cell);

            // 터지는 특수 블록이 다른 블록을 끌어들인다 (특수 블록끼리 연쇄)
            var destroyed = ExpandSpecials(matched);

            hits.Clear();
            foreach (var cell in destroyed)
            {
                var b = grid[cell.x, cell.y];
                if (b == null) continue;
                if ((b.Locked || b.Burning) && skills.PurifyHeal > 0) player.Heal(skills.PurifyHeal); // 정화
                hits.Add((b.transform.position, b.Color, b.Type));
                DestroyBlock(cell, strength);
            }
            skills.AddMana(hits.Count);
            level.AddBlocks(hits.Count); // 블록 1개 = 경험치 1

            foreach (var (cell, type) in creates)
            {
                var b = grid[cell.x, cell.y];
                if (b == null || destroyed.Contains(cell)) continue;
                b.SetLocked(false);
                b.SetBurning(false);
                b.SetSpecial(type);
                b.StartCoroutine(b.Punch(0.2f, 0.5f));
                FxRing.Spawn(b.transform.position + Vector3.back * 0.7f, Color.white, 1.4f, 0.3f);
                Sfx.Play(SfxType.Chime, type == SpecialType.Bomb ? 1.5f : 1.25f, 0.5f);
            }

            // 터진 블록마다 빛구슬이 날아가 적을 때린다 (원소 상성·스킬 반영)
            combo.LaunchOrbs(hits, firstStep);
            yield return new WaitForSeconds(destroyed.Count > matched.Count ? afterPopDelay + 0.12f : afterPopDelay);

            // 3콤보+ 마법사, 5콤보+ 전사 (각각 턴당 한 번, 고정 데미지)
            yield return combo.PlayBonuses(this);

            yield return Collapse();
            runs = MatchFinder.FindRuns(GetTypes());
            firstStep = false;
        }
        combo.EndChain();
        while (combo.PendingHits > 0) yield return null; // 적에게 날아가는 공격이 다 맞을 때까지
    }

    /// <summary>
    /// 가로·세로 줄이 한 칸에서 겹치면(L·T자) 십자 폭발, 4개 일자는 줄 파괴, 5개 이상 일자는 원소 폭발.
    /// 해당 스킬이 없으면 만들지 않는다 (원소 폭발이 없고 줄 파괴만 있으면 5개 일자도 줄 파괴).
    /// </summary>
    List<(Vector2Int cell, SpecialType type)> FindSpecialCreations(List<MatchFinder.Run> runs, bool firstStep, Vector2Int swapA, Vector2Int swapB)
    {
        var creates = new List<(Vector2Int, SpecialType)>();
        var used = new HashSet<int>();

        if (skills.Has(SkillId.CrossBlock))
        {
            for (int i = 0; i < runs.Count; i++)
            for (int j = 0; j < runs.Count; j++)
            {
                if (used.Contains(i) || used.Contains(j) || !runs[i].horizontal || runs[j].horizontal) continue;
                foreach (var cell in runs[i].cells)
                {
                    if (!runs[j].cells.Contains(cell)) continue;
                    if (grid[cell.x, cell.y].Special == SpecialType.None) creates.Add((cell, SpecialType.Cross));
                    used.Add(i);
                    used.Add(j);
                    break;
                }
            }
        }

        for (int i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (used.Contains(i) || run.cells.Count < 4) continue;
            SpecialType type;
            if (run.cells.Count >= 5 && skills.Has(SkillId.BombBlock)) type = SpecialType.Bomb;
            else if (skills.Has(SkillId.LineBlock)) type = run.horizontal ? SpecialType.LineH : SpecialType.LineV;
            else continue;

            var pivot = PickPivot(run.cells, firstStep, swapA, swapB);
            if (grid[pivot.x, pivot.y].Special != SpecialType.None) continue;
            creates.Add((pivot, type));
        }
        return creates;
    }

    static bool IsLine(Block b) => b.Special == SpecialType.LineH || b.Special == SpecialType.LineV;

    /// <summary>
    /// 특수 블록끼리 바꿨을 때 터질 칸. 옮긴 칸(center)이 중심이다.
    /// 일자+일자 = 십자(가로·세로 한 줄씩), 일자+폭탄(십자 폭발 블록) = 일자 방향 3줄, 폭탄+폭탄 = 5×5.
    /// 그 밖의 조합(원소 폭발 등)은 두 블록이 각자 터진다.
    /// </summary>
    HashSet<Vector2Int> SpecialComboArea(Block x, Block y, Vector2Int other, Vector2Int center)
    {
        var area = new HashSet<Vector2Int>();
        bool xLine = IsLine(x), yLine = IsLine(y);
        bool xBomb = x.Special == SpecialType.Cross, yBomb = y.Special == SpecialType.Cross;
        var pos = CellToWorld(center);
        var shake = combo.cameraShake;

        if (xLine && yLine)
        {
            for (int i = 0; i < width; i++) area.Add(new Vector2Int(i, center.y));
            for (int j = 0; j < height; j++) area.Add(new Vector2Int(center.x, j));
            FxBeam.Spawn(CellToWorld((width - 1) * 0.5f, center.y), width * cellSize, true, Color.white, 0.9f);
            FxBeam.Spawn(CellToWorld(center.x, (height - 1) * 0.5f), height * cellSize, false, Color.white, 0.9f);
        }
        else if ((xLine && yBomb) || (xBomb && yLine))
        {
            bool horizontal = (xLine ? x : y).Special == SpecialType.LineH;
            for (int d = -1; d <= 1; d++)
            {
                if (horizontal)
                {
                    int row = center.y + d;
                    if (row < 0 || row >= height) continue;
                    for (int i = 0; i < width; i++) area.Add(new Vector2Int(i, row));
                    FxBeam.Spawn(CellToWorld((width - 1) * 0.5f, row), width * cellSize, true, Color.white, 0.9f);
                }
                else
                {
                    int col = center.x + d;
                    if (col < 0 || col >= width) continue;
                    for (int j = 0; j < height; j++) area.Add(new Vector2Int(col, j));
                    FxBeam.Spawn(CellToWorld(col, (height - 1) * 0.5f), height * cellSize, false, Color.white, 0.9f);
                }
            }
        }
        else if (xBomb && yBomb)
        {
            for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
            {
                var n = new Vector2Int(center.x + dx, center.y + dy);
                if (InBounds(n)) area.Add(n);
            }
            FxRing.Spawn(pos + Vector3.back, Color.white, 5.5f, 0.4f);
            ScreenFlash.Flash(Color.white, 0.35f, 0.3f);
        }
        else
        {
            return new HashSet<Vector2Int> { other, center }; // 각자 터진다
        }

        // 조합 효과로 대신 터지므로 두 블록 자신의 효과는 지운다 (영역 안의 다른 특수 블록은 그대로 연쇄)
        x.SetSpecial(SpecialType.None);
        y.SetSpecial(SpecialType.None);
        area.Add(other);
        area.Add(center);
        Sfx.Play(SfxType.Impact, 0.9f, 1f);
        Sfx.Play(SfxType.Whoosh, 1.2f, 0.9f);
        shake?.Shake(0.5f, 0.3f);
        shake?.Punch(0.06f);
        return area;
    }

    /// <summary>특수 블록이 생길 칸: 첫 단계에선 플레이어가 옮긴 칸, 연쇄 중엔 줄의 가운데.</summary>
    static Vector2Int PickPivot(List<Vector2Int> cells, bool firstStep, Vector2Int swapA, Vector2Int swapB)
    {
        if (firstStep)
        {
            if (cells.Contains(swapB)) return swapB;
            if (cells.Contains(swapA)) return swapA;
        }
        return cells[cells.Count / 2];
    }

    HashSet<Vector2Int> ExpandSpecials(HashSet<Vector2Int> start)
    {
        var result = new HashSet<Vector2Int>(start);
        var queue = new Queue<Vector2Int>(start);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            var b = grid[c.x, c.y];
            if (b == null || b.Special == SpecialType.None) continue;

            PlaySpecialFx(b, c);
            foreach (var e in SpecialArea(b, c))
                if (result.Add(e)) queue.Enqueue(e);
        }
        return result;
    }

    IEnumerable<Vector2Int> SpecialArea(Block b, Vector2Int c)
    {
        switch (b.Special)
        {
            case SpecialType.LineH:
                for (int x = 0; x < width; x++) yield return new Vector2Int(x, c.y);
                break;
            case SpecialType.LineV:
                for (int y = 0; y < height; y++) yield return new Vector2Int(c.x, y);
                break;
            case SpecialType.Bomb:
                for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (grid[x, y] != null && grid[x, y].Type == b.Type) yield return new Vector2Int(x, y);
                break;
            case SpecialType.Cross:
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var n = new Vector2Int(c.x + dx, c.y + dy);
                    if (InBounds(n)) yield return n;
                }
                break;
        }
    }

    void PlaySpecialFx(Block b, Vector2Int c)
    {
        var shake = combo.cameraShake;
        if (b.Special == SpecialType.Bomb)
        {
            FxRing.Spawn(b.transform.position + Vector3.back, b.Color, 5f, 0.4f);
            ScreenFlash.Flash(b.Color, 0.3f, 0.3f);
            Sfx.Play(SfxType.Impact, 1.1f, 0.9f);
            shake?.Shake(0.4f, 0.25f);
            shake?.Punch(0.05f);
        }
        else if (b.Special == SpecialType.Cross)
        {
            FxRing.Spawn(b.transform.position + Vector3.back, Color.white, 3.2f, 0.3f);
            FxBeam.Spawn(b.transform.position, 3f * cellSize, true, Color.white, 0.5f);
            FxBeam.Spawn(b.transform.position, 3f * cellSize, false, Color.white, 0.5f);
            Sfx.Play(SfxType.Impact, 1.3f, 0.8f);
            shake?.Shake(0.35f, 0.2f);
        }
        else
        {
            bool horizontal = b.Special == SpecialType.LineH;
            var center = horizontal ? CellToWorld((width - 1) * 0.5f, c.y) : CellToWorld(c.x, (height - 1) * 0.5f);
            FxBeam.Spawn(center, (horizontal ? width : height) * cellSize, horizontal, Color.white);
            Sfx.Play(SfxType.Whoosh, 1.4f, 0.8f);
            Sfx.Play(SfxType.Impact, 1.6f, 0.5f);
            shake?.Shake(0.3f, 0.18f);
        }
    }

    // ───────────────────────── 액티브 스킬 (SkillBar가 부른다) ─────────────────────────
    // 액티브 스킬은 턴을 쓰지 않는다 (적이 행동하지 않는다).

    public bool CanUseSkills => !busy && run.CanPlay;

    public bool TryUseActive(SkillId id)
    {
        if (!CanUseSkills || !skills.Has(id) || skills.Mana < skills.Cost(id) || !skills.Ready(id)) return false;
        switch (id)
        {
            case SkillId.ActiveHammer:
            case SkillId.ActiveConvert:
                Deselect();
                Targeting = id;
                return true;
            case SkillId.ActiveShuffle:
                skills.SpendMana(skills.Cost(id));
                skills.StartCooldown(id);
                StartCoroutine(UseShuffle());
                return true;
            case SkillId.ActiveDelay:
                if (intent == null) return false;
                skills.SpendMana(skills.Cost(id));
                skills.StartCooldown(id);
                intent.Delay(1);
                FxRing.Spawn(enemy.HitPoint + Vector3.back * 1.2f, new Color(0.5f, 0.8f, 1f), 3f, 0.4f);
                Sfx.Play(SfxType.Chime, 0.8f, 0.8f);
                return true;
        }
        return false;
    }

    public void CancelTargeting()
    {
        Targeting = null;
        Deselect();
    }

    IEnumerator UseHammer(Vector2Int cell)
    {
        var b = grid[cell.x, cell.y];
        if (b == null || !skills.SpendMana(skills.Cost(SkillId.ActiveHammer))) yield break;
        skills.StartCooldown(SkillId.ActiveHammer);
        busy = true;

        combo.BeginChain();
        FxRing.Spawn(b.transform.position + Vector3.back, Color.white, 2f, 0.3f);
        Sfx.Play(SfxType.Impact, 1.4f, 0.9f);
        combo.cameraShake?.Shake(0.3f, 0.15f);
        BreakBlocks(new List<Vector2Int> { cell }, 2f); // 특수 블록이면 효과가 터진다
        yield return new WaitForSeconds(afterPopDelay + 0.1f);
        yield return Collapse();

        var runs = MatchFinder.FindRuns(GetTypes());
        if (runs.Count > 0) yield return ResolveBoard(runs, cell, cell);
        else while (combo.PendingHits > 0) yield return null;
        yield return AfterSkill();
    }

    /// <summary>원소 변환: 스킬 바에서 원소를 고른 뒤 부른다.</summary>
    public void ConvertBlock(Vector2Int cell, int type) => StartCoroutine(UseConvert(cell, type));

    IEnumerator UseConvert(Vector2Int cell, int type)
    {
        Deselect();
        var old = grid[cell.x, cell.y];
        if (old == null || old.Type == type || !skills.SpendMana(skills.Cost(SkillId.ActiveConvert))) yield break;
        skills.StartCooldown(SkillId.ActiveConvert);
        busy = true;

        Vector3 pos = old.transform.position;
        grid[cell.x, cell.y] = null;
        Destroy(old.gameObject);
        var b = SpawnBlock(type, cell, pos);
        b.StartCoroutine(b.Punch(0.2f, 0.5f));
        FxRing.Spawn(pos + Vector3.back * 0.7f, blockColors[type], 1.5f, 0.3f);
        Sfx.Play(SfxType.Chime, 1.3f, 0.7f);
        yield return new WaitForSeconds(0.25f);

        var runs = MatchFinder.FindRuns(GetTypes());
        if (runs.Count > 0) yield return ResolveBoard(runs, cell, cell);
        yield return AfterSkill();
    }

    IEnumerator UseShuffle()
    {
        busy = true;
        Sfx.Play(SfxType.Whoosh, 1f, 0.9f);
        yield return Reshuffle();
        yield return AfterSkill();
    }

    IEnumerator AfterSkill()
    {
        if (!player.IsDead && enemy.IsAlive) yield return run.ProcessLevelUps();
        if (!MatchFinder.HasPossibleMove(GetTypes(), GetLocks())) yield return Reshuffle();
        busy = false;
    }

    // ───────────────────────── 적 행동 (EnemyIntent가 부른다) ─────────────────────────

    bool[,] GetLocks()
    {
        var locks = new bool[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            locks[x, y] = grid[x, y] != null && grid[x, y].Locked;
        return locks;
    }

    List<Block> PickStatusTargets(int count)
    {
        var list = new List<Block>();
        foreach (var cell in GetRandomOccupiedCells(width * height))
        {
            var b = grid[cell.x, cell.y];
            if (b.Locked || b.Burning || b.Special != SpecialType.None) continue;
            list.Add(b);
            if (list.Count >= count) break;
        }
        return list;
    }

    public IEnumerator ApplySlime(int count, Vector3 from)
    {
        foreach (var b in PickStatusTargets(count))
        {
            b.SetLocked(true);
            b.StartCoroutine(b.Punch(0.15f, 0.25f));
            FxRing.Spawn(b.transform.position + Vector3.back * 0.7f, new Color(0.55f, 0.75f, 0.45f), 1.3f, 0.3f);
            Sfx.Play(SfxType.Hit, 0.7f, 0.6f);
            yield return new WaitForSeconds(0.12f);
        }
        yield return new WaitForSeconds(0.2f);
    }

    public IEnumerator ApplyEmber(int count, Vector3 from)
    {
        foreach (var b in PickStatusTargets(count))
        {
            b.SetBurning(true);
            b.StartCoroutine(b.Punch(0.15f, 0.25f));
            FxRing.Spawn(b.transform.position + Vector3.back * 0.7f, new Color(1f, 0.5f, 0.1f), 1.3f, 0.3f);
            Debris.Burst(b.transform.position, new Color(1f, 0.5f, 0.1f), 6, 4f, 0.1f);
            Sfx.Play(SfxType.Hit, 1.3f, 0.6f);
            yield return new WaitForSeconds(0.12f);
        }
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>남은 불씨를 모두 꺼뜨리고 그 개수를 돌려준다 (개수만큼 플레이어가 데미지를 입는다).</summary>
    public int BurnOut(int damageEach)
    {
        int count = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            var b = grid[x, y];
            if (b == null || !b.Burning) continue;
            b.SetBurning(false);
            Debris.Burst(b.transform.position, new Color(1f, 0.45f, 0.1f), 10, 6f, 0.12f);
            DamagePopup.Spawn(b.transform.position, $"-{damageEach}", new Color(1f, 0.5f, 0.2f), 0.07f);
            count++;
        }
        return count;
    }

    /// <summary>
    /// 동료 공격·망치처럼 매치 밖에서 블록을 부술 때. 특수 블록이면 효과가 터지고,
    /// 부서진 블록 모두 빛구슬 데미지·경험치·마나를 준다.
    /// </summary>
    public void BreakBlocks(List<Vector2Int> cells, float strength)
    {
        var start = new HashSet<Vector2Int>();
        foreach (var c in cells)
            if (InBounds(c) && grid[c.x, c.y] != null) start.Add(c);
        if (start.Count == 0) return;

        var hits = new List<(Vector3 position, Color color, int element)>();
        foreach (var cell in ExpandSpecials(start))
        {
            var b = grid[cell.x, cell.y];
            if (b == null) continue;
            if ((b.Locked || b.Burning) && skills.PurifyHeal > 0) player.Heal(skills.PurifyHeal);
            hits.Add((b.transform.position, b.Color, b.Type));
            DestroyBlock(cell, strength);
        }
        skills.AddMana(hits.Count);
        level.AddBlocks(hits.Count);
        combo.LaunchOrbs(hits, false);
    }

    void ClearStatuses()
    {
        TurnInStage = 0;
        if (grid == null) return;
        foreach (var b in grid)
        {
            if (b == null) continue;
            b.SetLocked(false);
            b.SetBurning(false);
        }
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

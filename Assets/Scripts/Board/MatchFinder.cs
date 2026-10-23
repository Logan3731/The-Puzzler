using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 블록 타입 배열(int[x,y], 빈칸은 -1)에서 가로·세로 3매치를 찾는다.
/// </summary>
public static class MatchFinder
{
    /// <summary>가로 또는 세로로 이어진 같은 원소 3개 이상 한 줄.</summary>
    public struct Run
    {
        public List<Vector2Int> cells;
        public bool horizontal;
    }

    public static List<Run> FindRuns(int[,] t)
    {
        int w = t.GetLength(0), h = t.GetLength(1);
        var runs = new List<Run>();

        for (int y = 0; y < h; y++)
        {
            int x = 0;
            while (x < w)
            {
                int type = t[x, y];
                int len = 1;
                while (type >= 0 && x + len < w && t[x + len, y] == type) len++;
                if (type >= 0 && len >= 3)
                {
                    var cells = new List<Vector2Int>();
                    for (int i = 0; i < len; i++) cells.Add(new Vector2Int(x + i, y));
                    runs.Add(new Run { cells = cells, horizontal = true });
                }
                x += len;
            }
        }

        for (int x = 0; x < w; x++)
        {
            int y = 0;
            while (y < h)
            {
                int type = t[x, y];
                int len = 1;
                while (type >= 0 && y + len < h && t[x, y + len] == type) len++;
                if (type >= 0 && len >= 3)
                {
                    var cells = new List<Vector2Int>();
                    for (int i = 0; i < len; i++) cells.Add(new Vector2Int(x, y + i));
                    runs.Add(new Run { cells = cells, horizontal = false });
                }
                y += len;
            }
        }

        return runs;
    }

    public static HashSet<Vector2Int> FindMatches(int[,] t)
    {
        var result = new HashSet<Vector2Int>();
        foreach (var run in FindRuns(t))
            foreach (var c in run.cells) result.Add(c);
        return result;
    }

    /// <summary>스왑 한 번으로 매치를 만들 수 있는 수가 있는지. locked가 true인 칸은 움직일 수 없다.</summary>
    public static bool HasPossibleMove(int[,] t, bool[,] locked = null)
    {
        int w = t.GetLength(0), h = t.GetLength(1);
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (locked != null && locked[x, y]) continue;
            if (x + 1 < w && (locked == null || !locked[x + 1, y]) && SwapMakesMatch(t, x, y, x + 1, y)) return true;
            if (y + 1 < h && (locked == null || !locked[x, y + 1]) && SwapMakesMatch(t, x, y, x, y + 1)) return true;
        }
        return false;
    }

    static bool SwapMakesMatch(int[,] t, int ax, int ay, int bx, int by)
    {
        (t[ax, ay], t[bx, by]) = (t[bx, by], t[ax, ay]);
        bool match = HasMatchAt(t, ax, ay) || HasMatchAt(t, bx, by);
        (t[ax, ay], t[bx, by]) = (t[bx, by], t[ax, ay]);
        return match;
    }

    static bool HasMatchAt(int[,] t, int x, int y)
    {
        int type = t[x, y];
        if (type < 0) return false;
        int w = t.GetLength(0), h = t.GetLength(1);

        int horizontal = 1;
        for (int i = x - 1; i >= 0 && t[i, y] == type; i--) horizontal++;
        for (int i = x + 1; i < w && t[i, y] == type; i++) horizontal++;
        if (horizontal >= 3) return true;

        int vertical = 1;
        for (int i = y - 1; i >= 0 && t[x, i] == type; i--) vertical++;
        for (int i = y + 1; i < h && t[x, i] == type; i++) vertical++;
        return vertical >= 3;
    }
}

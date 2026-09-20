using System.Collections.Generic;

// 纯逻辑规则集（无 Unity 依赖），便于 EditMode 单测与二期每日挑战复用。
public static class PuzzleRules
{
    // 评级：d=难度点数 gridSize²；S≈少步快通，A≈中游，B 其余（阈值随难度线性放大）
    public static string Rating(int moves, float seconds, int gridSize)
    {
        int d = gridSize * gridSize;
        if (moves <= d * 5 && seconds <= d * 7) return "S";
        if (moves <= d * 8 || seconds <= d * 12) return "A";
        return "B";
    }

    // current[i,j] = 该格当前拼图块编号（== i*n+j 为正确位置），-1 为空格
    public static int CountMisplaced(int[,] board, int gridSize)
    {
        int n = 0;
        for (int i = 0; i < gridSize; i++)
            for (int j = 0; j < gridSize; j++)
            {
                int tile = board[i, j];
                if (tile != -1 && tile != i * gridSize + j) n++;
            }
        return n;
    }

    // 生成洗牌后的棋盘布局（右下角为空格 -1）。
    // seed < 0 → 随机；seed ≥ 0 → 确定性（每日挑战：同一天全球同局）。
    // 洗牌方式 = 从复原态出发的合法滑块随机游走，天然保证可解。
    public static int[,] ShuffledBoard(int gridSize, int seed)
    {
        var rng = seed < 0 ? new System.Random() : new System.Random(seed);
        var board = new int[gridSize, gridSize];
        int k = 0;
        for (int i = 0; i < gridSize; i++)
            for (int j = 0; j < gridSize; j++)
                board[i, j] = k++;
        board[gridSize - 1, gridSize - 1] = -1; // 空格

        int ei = gridSize - 1, ej = gridSize - 1;
        int prevI = -1, prevJ = -1;
        int steps = gridSize * gridSize * 10;
        var dirs = new List<(int di, int dj)>(4);
        for (int n = 0; n < steps; n++)
        {
            dirs.Clear();
            if (ei > 0) dirs.Add((-1, 0));
            if (ei < gridSize - 1) dirs.Add((1, 0));
            if (ej > 0) dirs.Add((0, -1));
            if (ej < gridSize - 1) dirs.Add((0, 1));
            dirs.RemoveAll(d => ei + d.di == prevI && ej + d.dj == prevJ); // 不立即回退
            var (di, dj) = dirs[rng.Next(dirs.Count)];
            board[ei, ej] = board[ei + di, ej + dj];
            board[ei + di, ej + dj] = -1;
            prevI = ei; prevJ = ej;
            ei += di; ej += dj;
        }
        return board;
    }
}

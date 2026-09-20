using System.Collections.Generic;
using NUnit.Framework;

public class PuzzleRulesTests
{
    const int G = 4;

    [Test]
    public void Rating_S_WhenLowMovesAndTime()
    {
        Assert.AreEqual("S", PuzzleRules.Rating(30, 60f, G));   // d=16: ≤80 步且 ≤112s
    }

    [Test]
    public void Rating_A_WhenModerateMoves()
    {
        Assert.AreEqual("A", PuzzleRules.Rating(120, 300f, G)); // ≤128 步
    }

    [Test]
    public void Rating_A_WhenFastEvenWithManyMoves()
    {
        Assert.AreEqual("A", PuzzleRules.Rating(200, 100f, G)); // 步数超标但 ≤192s
    }

    [Test]
    public void Rating_B_WhenBothExceeded()
    {
        Assert.AreEqual("B", PuzzleRules.Rating(200, 300f, G));
    }

    [Test]
    public void Rating_ScalesWithDifficulty()
    {
        // 同样 40 步：3×3(d=9, S≤45 步) 是 S；5×5(d=25, S≤125) 也是 S；4×4(≤80) 是 S
        Assert.AreEqual("S", PuzzleRules.Rating(40, 30f, 3));
        Assert.AreEqual("S", PuzzleRules.Rating(40, 30f, 5));
    }

    [Test]
    public void CountMisplaced_SolvedBoard_IsZero()
    {
        var b = new int[G, G];
        for (int i = 0; i < G; i++)
            for (int j = 0; j < G; j++)
                b[i, j] = i * G + j;
        b[G - 1, G - 1] = -1;
        Assert.AreEqual(0, PuzzleRules.CountMisplaced(b, G));
    }

    [Test]
    public void CountMisplaced_SingleSwap_CountsTwo()
    {
        var b = new int[G, G];
        for (int i = 0; i < G; i++)
            for (int j = 0; j < G; j++)
                b[i, j] = i * G + j;
        b[G - 1, G - 1] = -1;
        // 交换 (0,0) 与 (0,1)：两块都错位
        (b[0, 0], b[0, 1]) = (b[0, 1], b[0, 0]);
        Assert.AreEqual(2, PuzzleRules.CountMisplaced(b, G));
    }

    [Test]
    public void ShuffledBoard_IsDeterministicForSameSeed()
    {
        var a = PuzzleRules.ShuffledBoard(G, 20260920);
        var b = PuzzleRules.ShuffledBoard(G, 20260920);
        for (int i = 0; i < G; i++)
            for (int j = 0; j < G; j++)
                Assert.AreEqual(a[i, j], b[i, j], $"({i},{j}) 同种子必须同布局");
    }

    [Test]
    public void ShuffledBoard_DifferentSeeds_Vary()
    {
        // 20 个种子至少产生 2 种布局（全部相同的概率≈0）
        var seen = new HashSet<string>();
        for (int s = 0; s < 20; s++)
        {
            var b = PuzzleRules.ShuffledBoard(G, s);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < G; i++)
                for (int j = 0; j < G; j++)
                    sb.Append(b[i, j]).Append(',');
            seen.Add(sb.ToString());
        }
        Assert.GreaterOrEqual(seen.Count, 2);
    }

    [Test]
    public void ShuffledBoard_IsValidPermutation()
    {
        foreach (var grid in new[] { 3, 4, 5 })
        {
            var b = PuzzleRules.ShuffledBoard(grid, 42);
            var tiles = new HashSet<int>();
            int holes = 0;
            for (int i = 0; i < grid; i++)
                for (int j = 0; j < grid; j++)
                {
                    var v = b[i, j];
                    if (v == -1) { holes++; continue; }
                    Assert.True(tiles.Add(v), $"拼图块 {v} 重复");
                    Assert.GreaterOrEqual(v, 0);
                    Assert.Less(v, grid * grid - 1);
                }
            Assert.AreEqual(1, holes, "必须恰好一个空格");
            Assert.AreEqual(grid * grid - 1, tiles.Count);
        }
    }
}

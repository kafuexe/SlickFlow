using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.SlickFlow;

public class ItemSearcher : IItemSearcher
{
    public List<(string name, int score, Item item)> Search(string query, List<Item> items)
    {
        var results = new List<(string, int, Item)>();
        var queryLower = query.ToLower();

        foreach (var item in items)
        {
            foreach (var name in item.Aliases)
            {
                var nameLower = name.ToLower();
                int score = 0;

                if (nameLower == queryLower)
                    score += 1000;
                if (nameLower.StartsWith(queryLower))
                    score += 800;
                if (queryLower.Contains(nameLower))
                    score += 400;
                if (nameLower.EndsWith(queryLower))
                    score += 50;

                int distance = LevenshteinDistance(nameLower, queryLower);
                if (distance == 1)
                    score += 50; // small boost for 1 character difference

                if (score > 0)
                {
                    score += Math.Max(0, 50 - Math.Abs(nameLower.Length - queryLower.Length) * 2);
                    results.Add((name, score, item));
                }
            }
        }

        return results
            .OrderByDescending(r => r.Item2) // score
            .ThenBy(r => r.Item1.Length) // shorter names first
            .ToList();
    }

    private int LevenshteinDistance(string a, string b)
    {
        int[,] dp = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost
                );
            }
        }

        return dp[a.Length, b.Length];
    }
}
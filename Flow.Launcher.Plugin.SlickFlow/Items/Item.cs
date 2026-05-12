using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace Flow.Launcher.Plugin.SlickFlow.Items;
public class Item
{
    public string Id { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string SubTitle { get; set; } = string.Empty;
    public int RunAs { get; set; } = 0;
    public int StartMode { get; set; } = 0;
    public string WorkingDir { get; set; } = string.Empty;
    public int ExecCount { get; set; } = 0;
    public List<string> Aliases { get; set; } = new();
    public string IconPath { get; set; } = string.Empty;

    public Item() { }
    public Item(string id, string fileName, IEnumerable<string>? aliases = null)
    {
        Id = id;
        FileName = fileName;
        if (aliases != null)
            Aliases = new List<string>(aliases);
    }
    public void AddAlias(string alias)
    {
        if (!Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
            Aliases.Add(alias);
    }
    public int RemoveAlias(string alias)
    {
        return Aliases.RemoveAll(a => string.Equals(a, alias, StringComparison.OrdinalIgnoreCase));
    }
    public bool MatchesQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        query = query.ToLowerInvariant();

        return FileName.ToLowerInvariant().Contains(query)
            || SubTitle.ToLowerInvariant().Contains(query)
            || Aliases.Any(a => a.ToLowerInvariant().Contains(query));
    }
    private static readonly Regex MetaItemPattern = new(@"^(@[^@]+@)+$", RegexOptions.Compiled);
    private static readonly Regex AliasExtractor = new(@"@([^@]+)@", RegexOptions.Compiled);

    public bool IsMetaItem => MetaItemPattern.IsMatch(FileName ?? string.Empty);

    public void Execute(bool forceAdminExec = false, IItemRepository? itemRepo = null)
    {
        if (IsMetaItem)
        {
            ExecuteMetaItem(forceAdminExec, itemRepo);
            return;
        }

        try
        {
            if (!IsUrl(FileName) && !File.Exists(FileName))
            {
                string sysPath = Path.Combine(Environment.SystemDirectory, FileName);
                if (File.Exists(sysPath))
                {
                    FileName = sysPath;
                }
            }

            var psi = new ProcessStartInfo
            {
                FileName = FileName,
                Arguments = Arguments,
                WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDir)
                    ? Environment.CurrentDirectory
                    : WorkingDir,
                UseShellExecute = true
            };

            if (!IsUrl(FileName) && (RunAs == 1 || forceAdminExec))
                psi.Verb = "runas";

            psi.WindowStyle = StartMode switch
            {
                1 => ProcessWindowStyle.Minimized,
                2 => ProcessWindowStyle.Maximized,
                _ => ProcessWindowStyle.Normal
            };

            Process.Start(psi);
            ExecCount++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to execute '{FileName}': {ex.Message}");
        }
    }

    private void ExecuteMetaItem(bool forceAdminExec, IItemRepository? itemRepo)
    {
        if (itemRepo == null)
            throw new InvalidOperationException("Meta items require an item repository to resolve alias references.");

        var leaves = new List<Item>();
        var metas = new List<Item>();
        CollectMetaItemPlan(itemRepo, new HashSet<Item>(), leaves, metas);

        foreach (var leaf in leaves)
            leaf.Execute(forceAdminExec, itemRepo);

        foreach (var meta in metas)
            meta.ExecCount++;
    }

    private void CollectMetaItemPlan(
        IItemRepository itemRepo,
        HashSet<Item> visited,
        List<Item> leaves,
        List<Item> metas)
    {
        // visited tracks the current DFS call path. Re-adding means we've looped
        // back to an ancestor on this same path - a cycle. Removed in finally so
        // sibling branches that legitimately revisit a meta item aren't flagged.
        if (!visited.Add(this))
            throw new InvalidOperationException(
                $"Cycle detected in meta item chain at item \"{Id}\". Execution aborted.");

        try
        {
            var aliases = AliasExtractor.Matches(FileName)
                .Select(m => m.Groups[1].Value)
                .ToList();

            var missing = new List<string>();
            var resolved = new List<Item>();

            foreach (var alias in aliases)
            {
                var target = itemRepo.GetItemByAlias(alias);
                if (target == null)
                    missing.Add(alias);
                else
                    resolved.Add(target);
            }

            if (missing.Count > 0)
            {
                var names = string.Join(", ", missing.Select(m => $"\"{m}\""));
                throw new InvalidOperationException($"Unknown aliases in item: {names}");
            }

            foreach (var target in resolved)
            {
                if (target.IsMetaItem)
                    target.CollectMetaItemPlan(itemRepo, visited, leaves, metas);
                else
                    leaves.Add(target);
            }

            metas.Add(this);
        }
        finally
        {
            visited.Remove(this);
        }
    }
    public bool IsUrl(string fileName)
    {
        return Uri.TryCreate(fileName, UriKind.Absolute, out var uriResult) 
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);   
    }
    public override string ToString()
    {
        var aliases = Aliases.Count > 0 ? string.Join(", ", Aliases) : "none";
        return $"[#{Id}] {FileName} ({Arguments}) | Aliases=[{aliases}] | RunAs={RunAs}, StartMode={StartMode}, ExecCount={ExecCount}";
    }
}
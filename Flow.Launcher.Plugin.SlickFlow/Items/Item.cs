using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

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
    private static readonly IReadOnlyDictionary<string, string> EmptyValues = new Dictionary<string, string>();

    public bool IsMetaItem => MetaItemPattern.IsMatch(FileName ?? string.Empty);

    /// <summary>
    /// True when the item has placeholders the user must be prompted for.
    /// Built-in tokens (<c>&lt;&lt;$clip&gt;&gt;</c>, ...) resolve automatically
    /// and don't count.
    /// </summary>
    public bool IsParameterized =>
        PlaceholderParser.Extract(FileName)
            .Concat(PlaceholderParser.Extract(Arguments))
            .Any(p => !BuiltInTokens.IsToken(p.Name));

    /// <summary>
    /// Returns a copy with placeholders replaced by <paramref name="values"/> and
    /// built-in tokens resolved against <paramref name="tokens"/> (the real
    /// clipboard/clock/environment when omitted).
    /// </summary>
    public Item Substitute(IReadOnlyDictionary<string, string> values, ITokenSources? tokens = null)
    {
        tokens ??= new CachingTokenSources(SystemTokenSources.Instance);
        return new Item
        {
            Id = Id,
            FileName = PlaceholderParser.Substitute(FileName, values, tokens),
            Arguments = PlaceholderParser.Substitute(Arguments, values, tokens),
            SubTitle = SubTitle,
            RunAs = RunAs,
            StartMode = StartMode,
            WorkingDir = WorkingDir,
            ExecCount = ExecCount,
            Aliases = new List<string>(Aliases),
            IconPath = IconPath
        };
    }

    public void Execute(
        bool forceAdminExec = false,
        IItemRepository? itemRepo = null,
        IReadOnlyDictionary<string, string>? values = null,
        ITokenSources? tokens = null)
    {
        // One snapshot per launch: the clipboard is read at most once and every
        // meta-chain leaf sees the same $date/$time.
        var snapshot = tokens as CachingTokenSources
            ?? new CachingTokenSources(tokens ?? SystemTokenSources.Instance);

        if (IsMetaItem)
        {
            ExecuteMetaItem(forceAdminExec, itemRepo, values, snapshot);
            return;
        }

        var substituted = Substitute(values ?? EmptyValues, snapshot);
        var fileName = substituted.FileName;
        var arguments = substituted.Arguments;

        try
        {
            if (!IsUrl(fileName) && !File.Exists(fileName))
            {
                string sysPath = Path.Combine(Environment.SystemDirectory, fileName);
                if (File.Exists(sysPath))
                    fileName = sysPath;
            }

            var target = IsUrl(fileName)
                ? new ScriptLauncher.LaunchTarget(fileName, arguments, false)
                : ScriptLauncher.Resolve(fileName, arguments);

            var psi = new ProcessStartInfo
            {
                FileName = target.FileName,
                Arguments = target.Arguments,
                WorkingDirectory = ResolveWorkingDirectory(fileName, target.IsScript),
                UseShellExecute = true
            };

            if (!IsUrl(fileName) && (RunAs == 1 || forceAdminExec))
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
            Console.WriteLine($"[Error] Failed to execute '{fileName}': {ex.Message}");
        }
    }

    private string ResolveWorkingDirectory(string fileName, bool isScript)
    {
        if (!string.IsNullOrWhiteSpace(WorkingDir))
            return WorkingDir;

        // Scripts commonly use paths relative to themselves, so run them from their own folder.
        if (isScript && Path.IsPathRooted(fileName))
        {
            var scriptDir = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(scriptDir) && Directory.Exists(scriptDir))
                return scriptDir;
        }

        return Environment.CurrentDirectory;
    }

    private void ExecuteMetaItem(
        bool forceAdminExec,
        IItemRepository? itemRepo,
        IReadOnlyDictionary<string, string>? values,
        ITokenSources tokens)
    {
        if (itemRepo == null)
            throw new InvalidOperationException("Meta items require an item repository to resolve alias references.");

        var leaves = new List<Item>();
        var metas = new List<Item>();
        WalkMetaChain(itemRepo, new HashSet<Item>(), leaves.Add, metas.Add);

        foreach (var leaf in leaves)
            leaf.Execute(forceAdminExec, itemRepo, values, tokens);

        foreach (var meta in metas)
            meta.ExecCount++;
    }

    /// <summary>
    /// Depth-first traversal of the meta-item alias chain rooted at this item.
    /// Invokes <paramref name="onLeaf"/> for each non-meta item encountered,
    /// and <paramref name="onMetaExit"/> after a meta item's children are visited.
    /// Throws on cycles (revisited ancestor) and unresolved aliases.
    /// </summary>
    internal void WalkMetaChain(
        IItemRepository itemRepo,
        HashSet<Item> visited,
        Action<Item> onLeaf,
        Action<Item>? onMetaExit = null)
    {
        // visited tracks the current DFS call path. Re-adding means we've looped
        // back to an ancestor on this same path - a cycle. Removed in finally so
        // sibling branches that legitimately revisit a meta item aren't flagged.
        if (!visited.Add(this))
            throw new InvalidOperationException(
                $"Cycle detected in meta item chain at item \"{Id}\". Execution aborted.");

        try
        {
            if (!IsMetaItem)
            {
                onLeaf(this);
                return;
            }

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
                target.WalkMetaChain(itemRepo, visited, onLeaf, onMetaExit);

            onMetaExit?.Invoke(this);
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
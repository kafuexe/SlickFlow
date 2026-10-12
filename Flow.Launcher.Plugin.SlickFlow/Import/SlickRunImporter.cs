using System.IO;
using System.Text;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace Flow.Launcher.Plugin.SlickFlow.Import;

public enum SkipReason
{
    /// <summary>Every alias of the entry already exists in the repository.</summary>
    AliasExists,
    /// <summary>Every alias of the entry was already taken by an earlier entry in the same file.</summary>
    Duplicate,
    /// <summary>The entry could not be mapped (e.g. no Filename).</summary>
    Invalid,
}

public sealed record SkippedEntry(string Name, SkipReason Reason, string Detail);

public sealed class SlickRunImportResult
{
    public int ImportedCount => ImportedAliases.Count;
    /// <summary>The first alias of each imported item.</summary>
    public List<string> ImportedAliases { get; } = new();
    public List<SkippedEntry> Skipped { get; } = new();
    public List<string> Warnings { get; } = new();

    private const int MaxWarningsInSummary = 10;

    /// <summary>
    /// Human-readable summary, e.g.
    /// "Imported 42, skipped 3 (alias already exists: g, mail; invalid: bad)."
    /// followed by any warnings.
    /// </summary>
    public string ToSummary()
    {
        var sb = new StringBuilder($"Imported {ImportedCount}, skipped {Skipped.Count}");

        var groups = new (SkipReason Reason, string Label)[]
            {
                (SkipReason.AliasExists, "alias already exists"),
                (SkipReason.Duplicate, "duplicate in file"),
                (SkipReason.Invalid, "invalid"),
            }
            .Select(g => (g.Label, Names: Skipped.Where(s => s.Reason == g.Reason).Select(s => s.Name).ToList()))
            .Where(g => g.Names.Count > 0)
            .Select(g => $"{g.Label}: {string.Join(", ", g.Names)}")
            .ToList();

        if (groups.Count > 0)
            sb.Append(" (").Append(string.Join("; ", groups)).Append(')');
        sb.Append('.');

        if (Warnings.Count > 0)
        {
            sb.AppendLine().AppendLine().AppendLine("Warnings:");
            foreach (var w in Warnings.Take(MaxWarningsInSummary))
                sb.Append("• ").AppendLine(w);
            if (Warnings.Count > MaxWarningsInSummary)
                sb.AppendLine($"…and {Warnings.Count - MaxWarningsInSummary} more.");
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Imports SlickRun MagicWords into an <see cref="IItemRepository"/>. Strictly additive: existing
/// items are never modified or removed.
/// <para>Conflict rules (aliases compared case-insensitively):</para>
/// <list type="bullet">
/// <item>Aliases that already exist in the repository are dropped from the entry.</item>
/// <item>Aliases already claimed by an earlier entry in the same file are dropped (first one wins).</item>
/// <item>If no alias remains the entry is skipped (<see cref="SkipReason.AliasExists"/> when any alias
/// was in the repository, otherwise <see cref="SkipReason.Duplicate"/>); if some remain, the entry is
/// imported with those and a warning names the dropped ones.</item>
/// <item>Entries without a Filename or usable alias are skipped as <see cref="SkipReason.Invalid"/>.</item>
/// </list>
/// All new items are added in a single batch (one save).
/// </summary>
public class SlickRunImporter
{
    private readonly IItemRepository _repo;
    private readonly string? _iconDir;

    /// <param name="repo">Target repository.</param>
    /// <param name="iconDir">
    /// SlickFlow's icon folder. SlickRun icons are copied there, because deleting an item deletes its
    /// icon file and we must never delete the user's SlickRun files. When null, icons are not imported.
    /// </param>
    public SlickRunImporter(IItemRepository repo, string? iconDir = null)
    {
        _repo = repo;
        _iconDir = string.IsNullOrWhiteSpace(iconDir) ? null : iconDir;
    }

    public SlickRunImportResult ImportFile(string path) => Import(QrsParser.ParseFile(path));

    public SlickRunImportResult Import(IEnumerable<QrsEntry> entries)
    {
        var result = new SlickRunImportResult();
        var existing = new HashSet<string>(
            _repo.GetAllItems().SelectMany(i => i.Aliases),
            StringComparer.OrdinalIgnoreCase);
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toAdd = new List<Item>();

        foreach (var entry in entries)
        {
            var mapped = QrsItemMapper.Map(entry);
            if (mapped.Item == null)
            {
                result.Skipped.Add(new SkippedEntry(entry.SectionName, SkipReason.Invalid, mapped.Error ?? "invalid"));
                continue;
            }

            var item = mapped.Item;
            var inRepo = item.Aliases.Where(existing.Contains).ToList();
            var inFile = item.Aliases.Where(a => !existing.Contains(a) && claimed.Contains(a)).ToList();
            var free = item.Aliases.Except(inRepo).Except(inFile).ToList();

            if (free.Count == 0)
            {
                var reason = inRepo.Count > 0 ? SkipReason.AliasExists : SkipReason.Duplicate;
                result.Skipped.Add(new SkippedEntry(entry.SectionName, reason,
                    $"alias(es) taken: {string.Join(", ", item.Aliases)}"));
                continue;
            }

            if (free.Count < item.Aliases.Count)
            {
                var dropped = string.Join(", ", inRepo.Concat(inFile));
                result.Warnings.Add(
                    $"{entry.SectionName}: alias(es) already in use and not imported: {dropped}; imported as {string.Join(", ", free)}");
                item.Aliases = free;
            }

            result.Warnings.AddRange(mapped.Warnings);
            item.IconPath = ImportIcon(item.IconPath, entry.SectionName, result.Warnings);

            foreach (var alias in free)
                claimed.Add(alias);
            toAdd.Add(item);
            result.ImportedAliases.Add(free[0]);
        }

        if (toAdd.Count > 0)
            _repo.AddItems(toAdd);

        return result;
    }

    private string ImportIcon(string sourcePath, string name, List<string> warnings)
    {
        if (string.IsNullOrEmpty(sourcePath) || _iconDir == null)
            return string.Empty;

        try
        {
            Directory.CreateDirectory(_iconDir);
            var target = Path.Combine(_iconDir, $"slickrun_{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}");
            File.Copy(sourcePath, target);
            return target;
        }
        catch (Exception ex)
        {
            warnings.Add($"{name}: could not copy icon '{sourcePath}': {ex.Message}");
            return string.Empty;
        }
    }
}

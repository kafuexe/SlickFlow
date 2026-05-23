using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

/// <summary>
/// Resolves the ordered, deduplicated set of placeholders that need to be
/// filled before an item (or a meta-item chain) can be executed.
/// </summary>
public static class PlaceholderSchema
{
    public static IReadOnlyList<Placeholder> From(Item item, IItemRepository? repo = null)
    {
        if (item.IsMetaItem && repo == null)
            throw new InvalidOperationException(
                "Meta items require an item repository to resolve alias references.");

        var seenNames = new HashSet<string>();
        var result = new List<Placeholder>();

        void CollectFromLeaf(Item leaf)
        {
            AddDistinct(PlaceholderParser.Extract(leaf.FileName));
            AddDistinct(PlaceholderParser.Extract(leaf.Arguments));
        }

        void AddDistinct(IEnumerable<Placeholder> placeholders)
        {
            foreach (var ph in placeholders)
                if (seenNames.Add(ph.Name))
                    result.Add(ph);
        }

        item.WalkMetaChain(repo!, new HashSet<Item>(), CollectFromLeaf);
        return result;
    }
}

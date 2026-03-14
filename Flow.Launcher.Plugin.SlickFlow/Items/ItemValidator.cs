using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace Flow.Launcher.Plugin.SlickFlow.items;

public class ItemValidator
{
    private readonly IItemRepository _itemRepo;
    private readonly string _slickFlowIcon;

    public ItemValidator(IItemRepository itemRepo, string slickFlowIcon)
    {
        _itemRepo = itemRepo;
        _slickFlowIcon = slickFlowIcon;
    }

    public List<Result> ValidateAliases(List<string> aliases)
    {
        var results = new List<Result>();
        if (!aliases.Any())
        {
            results.Add(new Result
            {
                Title = "No valid aliases provided",
                IcoPath = _slickFlowIcon,
                Score = int.MaxValue - 1000
            });
            return results;
        }

        var allItems = _itemRepo.GetAllItems();
        var existing = allItems.SelectMany(i => i.Aliases.Select(a => a.ToLowerInvariant()))
            .Intersect(aliases.Select(a => a.ToLowerInvariant()))
            .ToList() as List<string>;

        if (existing.Any())
        {
            results.Add(new Result
            {
                Title = $"Alias already exists: {string.Join(", ", existing)}",
                IcoPath = _slickFlowIcon,
                Score = int.MaxValue - 1000
            });
        }

        return results;
    }

    public bool IsValidProperty(string prop)
    {
        return prop.ToLowerInvariant() switch
        {
            "args" or "arguments" or "runas" or "startmode" or "subtitle" or "workingdir" or "workdir" => true,
            _ => false
        };
    }
}

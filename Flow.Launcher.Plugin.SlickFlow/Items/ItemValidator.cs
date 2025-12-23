using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.SlickFlow;

public class ItemValidator
{
    private readonly SlickFlow _plugin;

    public ItemValidator(SlickFlow plugin)
    {
        _plugin = plugin;
    }

    public List<Result> ValidateAliases(List<string> aliases)
    {
        var results = new List<Result>();
        if (!aliases.Any())
        {
            results.Add(new Result
            {
                Title = "No valid aliases provided",
                IcoPath = _plugin._slickFlowIcon,
                Score = int.MaxValue - 1000
            });
            return results;
        }

        var allItems = _plugin._itemRepo.GetAllItems();
        var existing = allItems.SelectMany(i => i.Aliases.Select(a => a.ToLowerInvariant()))
            .Intersect(aliases.Select(a => a.ToLowerInvariant()))
            .ToList();

        if (existing.Any())
        {
            results.Add(new Result
            {
                Title = $"Alias already exists: {string.Join(", ", existing)}",
                IcoPath = _plugin._slickFlowIcon,
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
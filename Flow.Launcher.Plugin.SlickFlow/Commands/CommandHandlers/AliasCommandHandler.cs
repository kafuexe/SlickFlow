using Flow.Launcher.Plugin.SlickFlow.Items;

namespace Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;

public class AliasCommandHandler : ICommandHandler
{
    private readonly SlickFlow _plugin;

    public AliasCommandHandler(SlickFlow plugin)
    {
        _plugin = plugin;
    }

    public List<Result> Handle(string[] args)
    {
        var results = new List<Result>();

        if (args.Length < 2)
        {
            results.Add(new Result
            {
                Title = "Usage: alias <existing-alias-or-id> <newAlias1|newAlias2>",
                Score = int.MaxValue - 1000,
                IcoPath = _plugin._slickFlowIcon
            });
            return results;
        }

        string target = args[0];
        Item? item = _plugin._itemRepo.GetItemById(target) ?? _plugin._itemRepo.GetItemByAlias(target);
        if (item == null)
        {
            results.Add(new Result { Title = $"No item found with '{target}'",
                IcoPath = _plugin._slickFlowIcon, Score = int.MaxValue - 1000 });
            return results;
        }

        var newAliases = args[1].Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();

        var validationResults = _plugin._itemValidator.ValidateAliases(newAliases);
        if (validationResults.Any())
        {
            return validationResults;
        }

        results.Add(new Result
        {
            Title = $"Add {newAliases.Count} alias(es) to item {item.Id}",
            SubTitle = $"Existing aliases: {string.Join(", ", item.Aliases)}",
            Score = int.MaxValue - 1000,
            IcoPath = _plugin._slickFlowIcon,
            Action = _ =>
            {
                int addedCount = 0;
                foreach (var alias in newAliases)
                {
                    if (!item.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
                    {
                        item.Aliases.Add(alias);
                        addedCount++;
                    }
                }

                if (addedCount > 0)
                    _plugin._itemRepo.UpdateItem(item);

                return true;
            }
        });

        return results;
    }
}
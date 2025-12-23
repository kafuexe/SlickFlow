using System.Collections.Generic;

namespace Flow.Launcher.Plugin.SlickFlow;

public class RemoveCommandHandler : ICommandHandler
{
    private readonly SlickFlow _plugin;

    public RemoveCommandHandler(SlickFlow plugin)
    {
        _plugin = plugin;
    }

    public List<Result> Handle(string[] args)
    {
        var results = new List<Result>();
        if (args.Length < 1)
        {
            results.Add(new Result
            {
                Title = "Usage: remove <alias>",
                Score = int.MaxValue - 1000,
                IcoPath = _plugin._slickFlowIcon
            });
            return results;
        }

        var alias = args[0];
        var item = _plugin._itemRepo.GetItemByAlias(alias);
        if (item == null)
        {
            results.Add(new Result
            {
                Title = $"No item found with alias '{alias}'",
                Score = int.MaxValue - 1000,
                IcoPath = _plugin._slickFlowIcon
            });
            return results;
        }

        if (item.Aliases.Count <= 1)
        {
            results.Add(new Result
            {
                Title = $"Item only has one alias. Use 'delete {alias}' to delete the item instead.",
                Score = int.MaxValue - 1000,
                IcoPath = _plugin._slickFlowIcon
            });
            return results;
        }

        results.Add(new Result
        {
            Title = $"Remove alias '{alias}' from item {item.Id}",
            Score = int.MaxValue - 1000,
            IcoPath = _plugin._slickFlowIcon,
            Action = _ =>
            {
                _plugin._itemRepo.RemoveAlias(item.Id, alias);
                return true;
            }
        });
        return results;
    }
}
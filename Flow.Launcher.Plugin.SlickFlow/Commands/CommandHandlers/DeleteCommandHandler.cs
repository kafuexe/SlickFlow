using System.Collections.Generic;

namespace Flow.Launcher.Plugin.SlickFlow;

public class DeleteCommandHandler : ICommandHandler
{
    private readonly SlickFlow _plugin;

    public DeleteCommandHandler(SlickFlow plugin)
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
                Title = "Usage: delete <alias-or-id>",
                Score = int.MaxValue - 1000,
                IcoPath = _plugin._slickFlowIcon
            });
            return results;
        }

        string target = args[0];
        Item? item = _plugin._itemRepo.GetItemById(target) ?? _plugin._itemRepo.GetItemByAlias(target);


        if (item == null)
        {
            results.Add(new Result { Title = $"No item found with '{target}'", IcoPath = _plugin._slickFlowIcon, Score = int.MaxValue - 1000 });
            return results;
        }

        // Confirm deletion
        results.Add(new Result
        {
            Title = $"Confirm delete of item {item.Id}?",
            Score = int.MaxValue - 1000,
            SubTitle = $"Aliases: {string.Join(", ", item.Aliases)}",
            IcoPath = _plugin._slickFlowIcon,
            Action = _ =>
            {
                _plugin._itemRepo.DeleteItem(item.Id);
                Console.WriteLine($"[Deleted] Item {item.Id} ({item.FileName})");
                return true;
            }
        });

        return results;
    }
}
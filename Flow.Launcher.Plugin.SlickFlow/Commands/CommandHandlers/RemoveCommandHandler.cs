using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;

public class RemoveCommandHandler : ICommandHandler
{
    private readonly IItemRepository _itemRepo;
    private readonly string _slickFlowIcon;

    public RemoveCommandHandler(IItemRepository itemRepo, string slickFlowIcon)
    {
        _itemRepo = itemRepo;
        _slickFlowIcon = slickFlowIcon;
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
                IcoPath = _slickFlowIcon
            });
            return results;
        }

        var alias = args[0];
        var item = _itemRepo.GetItemByAlias(alias);
        if (item == null)
        {
            results.Add(new Result
            {
                Title = $"No item found with alias '{alias}'",
                Score = int.MaxValue - 1000,
                IcoPath = _slickFlowIcon
            });
            return results;
        }

        if (item.Aliases.Count <= 1)
        {
            results.Add(new Result
            {
                Title = $"Item only has one alias. Use 'delete {alias}' to delete the item instead.",
                Score = int.MaxValue - 1000,
                IcoPath = _slickFlowIcon
            });
            return results;
        }

        results.Add(new Result
        {
            Title = $"Remove alias '{alias}' from item {item.Id}",
            Score = int.MaxValue - 1000,
            IcoPath = _slickFlowIcon,
            Action = _ =>
            {
                _itemRepo.RemoveAlias(item.Id, alias);
                return true;
            }
        });
        return results;
    }
}

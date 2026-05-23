using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;

public class DeleteCommandHandler : ICommandHandler
{
    private readonly IItemRepository _itemRepo;
    private readonly string _slickFlowIcon;

    public DeleteCommandHandler(IItemRepository itemRepo, string slickFlowIcon)
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
                Title = "Usage: delete <alias-or-id>",
                Score = int.MaxValue - 1000,
                IcoPath = _slickFlowIcon
            });
            return results;
        }

        string target = args[0];
        Item? item = _itemRepo.GetItemById(target) ?? _itemRepo.GetItemByAlias(target);


        if (item == null)
        {
            results.Add(new Result { Title = $"No item found with '{target}'", IcoPath = _slickFlowIcon, Score = int.MaxValue - 1000 });
            return results;
        }

        // Confirm deletion
        results.Add(new Result
        {
            Title = $"Confirm delete of item {item.Id}?",
            Score = int.MaxValue - 1000,
            SubTitle = $"Aliases: {string.Join(", ", item.Aliases)}",
            IcoPath = _slickFlowIcon,
            Action = _ =>
            {
                _itemRepo.DeleteItem(item.Id);
                Console.WriteLine($"[Deleted] Item {item.Id} ({item.FileName})");
                return true;
            }
        });

        return results;
    }
}

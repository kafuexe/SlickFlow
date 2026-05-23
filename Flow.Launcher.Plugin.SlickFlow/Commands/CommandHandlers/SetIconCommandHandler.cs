using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.Utils;
using Flow.Launcher.Plugin.SlickFlow.Utils.Icons;

namespace Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;

public class SetIconCommandHandler : ICommandHandler
{
    private readonly IItemRepository _itemRepo;
    private readonly IconHelper _iconHelper;
    private readonly string _slickFlowIcon;

    public SetIconCommandHandler(IItemRepository itemRepo, IconHelper iconHelper, string slickFlowIcon)
    {
        _itemRepo = itemRepo;
        _iconHelper = iconHelper;
        _slickFlowIcon = slickFlowIcon;
    }

    public List<Result> Handle(string[] args)
    {
        var results = new List<Result>();

        if (args.Length < 2)
        {
            results.Add(new Result
            {
                Title = "Usage: seticon <alias-or-id> <icon-path-or-url>",
                Score = int.MaxValue - 1000,
                IcoPath = _slickFlowIcon
            });
            return results;
        }

        string target = args[0];
        string iconSource = string.Join(' ', args.Skip(1));

        Item? item = _itemRepo.GetItemById(target) ?? _itemRepo.GetItemByAlias(target);

        if (item == null)
        {
            results.Add(new Result { Title = $"No item found with '{target}'", IcoPath = _slickFlowIcon, Score = int.MaxValue - 1000 });
            return results;
        }

        results.Add(new Result
        {
            Title = $"Set custom icon for item {item.Id}",
            SubTitle = $"Icon source: {iconSource}",
            Score = int.MaxValue - 1000,
            IcoPath = _slickFlowIcon,
            Action = _ =>
            {
                string newIconPath = _iconHelper.SetCustomIcon(iconSource, item.Id);
                if (!string.IsNullOrEmpty(newIconPath))
                {
                    item.IconPath = newIconPath;
                    _itemRepo.UpdateItem(item);
                    return true;
                }
                return false;
            }
        });

        return results;
    }
}

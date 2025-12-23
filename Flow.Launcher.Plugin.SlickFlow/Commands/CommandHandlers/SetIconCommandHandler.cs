using System.Collections.Generic;

namespace Flow.Launcher.Plugin.SlickFlow;

public class SetIconCommandHandler : ICommandHandler
{
    private readonly SlickFlow _plugin;

    public SetIconCommandHandler(SlickFlow plugin)
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
                Title = "Usage: seticon <alias-or-id> <icon-path-or-url>",
                Score = int.MaxValue - 1000,
                IcoPath = _plugin._slickFlowIcon
            });
            return results;
        }

        string target = args[0];
        string iconSource = string.Join(' ', args.Skip(1));

        Item? item = _plugin._itemRepo.GetItemById(target) ?? _plugin._itemRepo.GetItemByAlias(target);

        if (item == null)
        {
            results.Add(new Result { Title = $"No item found with '{target}'", IcoPath = _plugin._slickFlowIcon, Score = int.MaxValue - 1000 });
            return results;
        }

        results.Add(new Result
        {
            Title = $"Set custom icon for item {item.Id}",
            SubTitle = $"Icon source: {iconSource}",
            Score = int.MaxValue - 1000,
            IcoPath = _plugin._slickFlowIcon,
            Action = _ =>
            {
                string newIconPath = _plugin._iconHelper.SetCustomIcon(iconSource, item.Id);
                if (!string.IsNullOrEmpty(newIconPath))
                {
                    item.IconPath = newIconPath;
                    _plugin._itemRepo.UpdateItem(item);
                    return true;
                }
                return false;
            }
        });

        return results;
    }
}
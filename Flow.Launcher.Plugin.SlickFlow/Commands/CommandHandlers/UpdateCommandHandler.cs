using System;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.SlickFlow;

public class UpdateCommandHandler : ICommandHandler
{
    private readonly SlickFlow _plugin;

    public UpdateCommandHandler(SlickFlow plugin)
    {
        _plugin = plugin;
    }

    public List<Result> Handle(string[] args)
    {
        var results = new List<Result>();

        if (args.Length < 3)
        {
            results.Add(new Result
            {
                Score = int.MaxValue - 1000,
                Title = "Usage: update <alias-or-id> <property> <value> [property value] ...",
                IcoPath = _plugin._slickFlowIcon
            });
            return results;
        }

        string target = args[0];

        // Just fetch the item for preview, don't change it yet
        Item? item = _plugin._itemRepo.GetItemById(target) ?? _plugin._itemRepo.GetItemByAlias(target);

        if (item == null)
        {
            results.Add(new Result { Title = $"No item found with '{target}'",
                IcoPath = _plugin._slickFlowIcon, Score = int.MaxValue - 1000 });
            return results;
        }

        // Create a copy of the updates for previewing
        var updates = new Dictionary<string, string>();
        for (int i = 1; i < args.Length - 1; i += 2)
        {
            string prop = args[i].ToLowerInvariant();
            string val = args[i + 1];
            updates[prop] = val;
        }

        var invalidProps = updates.Keys.Where(k => !_plugin._itemValidator.IsValidProperty(k)).ToList();
        if (invalidProps.Any())
        {
            results.Add(new Result
            {
                Title = $"Invalid properties: {string.Join(", ", invalidProps)}",
                IcoPath = _plugin._slickFlowIcon,
                Score = int.MaxValue - 1000
            });
            return results;
        }

        // Show a result, actual update happens in Action
        results.Add(new Result
        {
            Title = $"Update item {item.Id}",
            Score = int.MaxValue - 1000,
            IcoPath = _plugin._slickFlowIcon,
            SubTitle = $"Properties to update: {string.Join(", ", updates.Select(kv => $"{kv.Key}={kv.Value}"))}",
            Action = _ =>
            {
                foreach (var kv in updates)
                {
                    string prop = kv.Key;
                    string val = kv.Value;

                    switch (prop)
                    {
                        case "args":
                        case "arguments":
                            item.Arguments = val;
                            break;

                        case "runas":
                            if (int.TryParse(val, out int ra))
                                item.RunAs = ra;
                            break;

                        case "startmode":
                            if (int.TryParse(val, out int sm))
                                item.StartMode = sm;
                            break;

                        case "subtitle":
                            item.SubTitle = val;
                            break;

                        case "workingdir":
                        case "workdir":
                            item.WorkingDir = val;
                            break;
                    }
                }

                _plugin._itemRepo.UpdateItem(item);
                return true;
            }
        });

        return results;
    }
}
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;

public class AddCommandHandler : ICommandHandler
{
    private readonly SlickFlow _plugin;

    public AddCommandHandler(SlickFlow plugin)
    {
        _plugin = plugin;
    }

    public List<Result> Handle(string[] args)
    {
        args = args.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
        var results = new List<Result>();

        if (args.Length < 2)
        {
            results.Add(new Result
            {
                Title = "Usage: add <alias1|alias2> <file-or-url> [args...] [runas]",
                Score = int.MaxValue - 1000,
                IcoPath = _plugin._slickFlowIcon
            });
            return results;
        }

        // Split aliases
        var aliases = args[0].Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        // File or URL (remove quotes only if they exist)
        var fileOrUrl = args[1].Trim();
        if ((fileOrUrl.StartsWith('"') && fileOrUrl.EndsWith('"')) ||
            (fileOrUrl.StartsWith('\'') && fileOrUrl.EndsWith('\'')))
        {
            fileOrUrl = fileOrUrl.Substring(1, fileOrUrl.Length - 2);
        }

        string fileArgs = string.Empty;
        int runAs = 0;

        if (args.Length > 2)
        {
            // Check if last argument is integer (runAs)
            if (int.TryParse(args[^1], out int ra))
            {
                runAs = ra;
                if (args.Length > 3)
                    fileArgs = string.Join(' ', args.Skip(2).Take(args.Length - 3));
            }
            else
            {
                fileArgs = string.Join(' ', args.Skip(2));
            }
        }

        // Prevent duplicate aliases
        var validationResults = _plugin._itemValidator.ValidateAliases(aliases);
        if (validationResults.Any())
        {
            return validationResults;
        }

        // Create Result with Action to add the item
        results.Add(new Result
        {
            Title = $"Add item: {string.Join(", ", aliases)}",
            SubTitle = $"File: {fileOrUrl} {fileArgs}".Trim(),
            Score = int.MaxValue - 1000,
            IcoPath = _plugin._slickFlowIcon,
            Action = _ =>
            {
                var item = new Item
                {
                    FileName = fileOrUrl,
                    Arguments = fileArgs,
                    RunAs = runAs,
                    Aliases = aliases
                };

                // Add the item to the repository
                _plugin._itemRepo.AddItem(item);

                return true;
            }
        });

        return results;
    }
}
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.items;

namespace Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;

public class AddCommandHandler : ICommandHandler
{
    private readonly IItemRepository _itemRepo;
    private readonly ItemValidator _itemValidator;
    private readonly string _slickFlowIcon;

    public AddCommandHandler(IItemRepository itemRepo, ItemValidator itemValidator, string slickFlowIcon)
    {
        _itemRepo = itemRepo;
        _itemValidator = itemValidator;
        _slickFlowIcon = slickFlowIcon;
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
                IcoPath = _slickFlowIcon
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
        var validationResults = _itemValidator.ValidateAliases(aliases);
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
            IcoPath = _slickFlowIcon,
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
                _itemRepo.AddItem(item);

                return true;
            }
        });

        return results;
    }
}

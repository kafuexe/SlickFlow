using Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;
using Flow.Launcher.Plugin.SlickFlow.items;
using Flow.Launcher.Plugin.SlickFlow.Utils;

namespace Flow.Launcher.Plugin.SlickFlow.Commands;

public class CommandProcessor
{
    private readonly Dictionary<string, ICommandHandler> _handlers;

    public CommandProcessor(IItemRepository itemRepo, ItemValidator itemValidator, IconHelper iconHelper, string slickFlowIcon)
    {
        _handlers = new Dictionary<string, ICommandHandler>(StringComparer.OrdinalIgnoreCase)
        {
            ["add"] = new AddCommandHandler(itemRepo, itemValidator, slickFlowIcon),
            ["alias"] = new AliasCommandHandler(itemRepo, itemValidator, slickFlowIcon),
            ["remove"] = new RemoveCommandHandler(itemRepo, slickFlowIcon),
            ["delete"] = new DeleteCommandHandler(itemRepo, slickFlowIcon),
            ["update"] = new UpdateCommandHandler(itemRepo, itemValidator, slickFlowIcon),
            ["seticon"] = new SetIconCommandHandler(itemRepo, iconHelper, slickFlowIcon)
        };
    }

    public List<Result> Process(string command, string[] args)
    {
        if (_handlers.TryGetValue(command, out var handler))
        {
            return handler.Handle(args);
        }
        return new List<Result>();
    }
}

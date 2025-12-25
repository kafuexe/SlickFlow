using Flow.Launcher.Plugin.SlickFlow.Commands.CommandHandlers;

namespace Flow.Launcher.Plugin.SlickFlow.Commands;

public class CommandProcessor
{
    private readonly Dictionary<string, ICommandHandler> _handlers;

    public CommandProcessor(SlickFlow plugin)
    {
        _handlers = new Dictionary<string, ICommandHandler>(StringComparer.OrdinalIgnoreCase)
        {
            ["add"] = new AddCommandHandler(plugin),
            ["alias"] = new AliasCommandHandler(plugin),
            ["remove"] = new RemoveCommandHandler(plugin),
            ["delete"] = new DeleteCommandHandler(plugin),
            ["update"] = new UpdateCommandHandler(plugin),
            ["seticon"] = new SetIconCommandHandler(plugin)
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
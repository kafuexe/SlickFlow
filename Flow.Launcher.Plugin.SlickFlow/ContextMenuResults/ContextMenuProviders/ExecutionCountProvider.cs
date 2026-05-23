using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Results;

public static class ExecutionCountProvider
{
    public static Result? Provide(Result selectedResult, Item item, IItemRepository itemRepo)
    {
        return new Result
        {
            Title = $"Executed {item.ExecCount} times",
            SubTitle = "Execution statistics",
            IcoPath = "Assets/Graph.png",
            Action = _ => false
        };
    }
}

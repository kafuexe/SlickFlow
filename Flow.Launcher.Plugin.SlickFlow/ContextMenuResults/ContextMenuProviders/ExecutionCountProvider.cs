using Flow.Launcher.Plugin.SlickFlow.Items;

namespace Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Results;

public static class ExecutionCountProvider
{
    public static Result? Provide(Result selectedResult, Item item)
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

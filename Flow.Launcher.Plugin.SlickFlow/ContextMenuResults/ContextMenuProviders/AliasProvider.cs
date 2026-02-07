using Flow.Launcher.Plugin.SlickFlow.Items;

namespace Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Results;

public static class AliasesProvider
{
    public static Result? Provide(Result selectedResult, Item item)
    {
        if (item.Aliases == null || item.Aliases.Count == 0)
            return null;

        return new Result
        {
            Title = "Aliases",
            SubTitle = string.Join(", ", item.Aliases),
            IcoPath = "Assets/Tag.png",
            Action = _ => false
        };
    }
}

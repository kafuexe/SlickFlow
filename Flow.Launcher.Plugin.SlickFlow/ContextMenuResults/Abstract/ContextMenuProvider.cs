using Flow.Launcher.Plugin.SlickFlow.Items;

namespace Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Abstract;

public delegate Result? ContextMenuProvider(
    Result selectedResult,
    Item item
);
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Abstract;

public delegate Result? ContextMenuProvider(
    Result selectedResult,
    Item item,
    IItemRepository itemRepo
);
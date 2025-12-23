using System.Collections.Generic;

namespace Flow.Launcher.Plugin.SlickFlow;

public interface IItemSearcher
{
    List<(string name, int score, Item item)> Search(string query, List<Item> items);
}
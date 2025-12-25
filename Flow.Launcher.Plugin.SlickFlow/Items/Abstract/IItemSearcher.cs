
namespace Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

public interface IItemSearcher
{
    List<(string name, int score, Item item)> Search(string query, List<Item> items);
}
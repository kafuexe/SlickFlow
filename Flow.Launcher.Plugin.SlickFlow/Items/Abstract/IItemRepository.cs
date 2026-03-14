namespace Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

public interface IItemRepository
{
    string AddItem(Item item);
    Item? GetItemById(string id);
    List<Item> GetAllItems();
    void UpdateItem(Item item);
    void DeleteItem(string id);
    void AddAlias(string itemId, string alias);
    void RemoveAlias(string itemId, string alias);
    Item? GetItemByAlias(string alias);
}

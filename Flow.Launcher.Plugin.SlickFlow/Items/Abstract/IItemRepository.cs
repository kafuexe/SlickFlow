namespace Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

public interface IItemRepository
{
    string AddItem(Item item);
    /// <summary>Adds several items in one batch (single save) and returns their assigned IDs in order.</summary>
    IReadOnlyList<string> AddItems(IEnumerable<Item> items);
    Item? GetItemById(string id);
    List<Item> GetAllItems();
    void UpdateItem(Item item);
    void DeleteItem(string id);
    void AddAlias(string itemId, string alias);
    void RemoveAlias(string itemId, string alias);
    Item? GetItemByAlias(string alias);
}

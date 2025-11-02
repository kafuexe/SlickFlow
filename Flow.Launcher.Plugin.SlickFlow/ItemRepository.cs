using System.Text.Json;

namespace Flow.Launcher.Plugin.SlickFlow.Stores;

public class ItemRepository 
{
    private readonly string _path;
    private readonly List<Item> _items = new();

    public ItemRepository(string path)
    {
        _path = path;
        Load();
    }
    public int AddItem(Item item)
    {
        if (item.Id == 0)
            item.Id = _items.Count > 0 ? _items.Max(i => i.Id) + 1 : 1;

        _items.Add(item);
        Save();
        return item.Id;
    }
    public Item? GetItemById(int id)
    {
        return _items.FirstOrDefault(i => i.Id == id);
    }
    public List<Item> GetAllItems()
    {
        return new List<Item>(_items);
    }
    public void UpdateItem(Item item)
    {
        var existing = _items.FirstOrDefault(i => i.Id == item.Id);
        if (existing == null)
            throw new InvalidOperationException($"Item with ID {item.Id} not found.");

        var index = _items.IndexOf(existing);
        _items[index] = item;
        Save();
    }
    public void DeleteItem(int id)
    {
        var existing = _items.FindAll(i => i.Id == id);
        if (existing.Count == 0)
            return;
            
        foreach (var item in existing)
        {
            if (!string.IsNullOrWhiteSpace(item.IconPath) && File.Exists(item.IconPath))
                File.Delete(item.IconPath);

            _items.Remove(item);
        }
        Save();
    }

    public void AddAlias(int itemId, string alias)
    {
        var item = GetItemById(itemId);
        if (item == null) throw new InvalidOperationException($"Item with ID {itemId} not found.");
        if (!item.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
        {
            item.Aliases.Add(alias);
            Save();
        }
    }
    public void RemoveAlias(int itemId, string alias)
    {
        var item = GetItemById(itemId);
        if (item == null) throw new InvalidOperationException($"Item with ID {itemId} not found.");
        if (item.RemoveAlias(alias)> 0)
            Save();
    }
    public Item? GetItemByAlias(string alias)
    {
        return _items.FirstOrDefault(i =>
            i.Aliases.Any(a => string.Equals(a, alias, StringComparison.OrdinalIgnoreCase)));
    }
    private void Load()
    {
        if (!File.Exists(_path))
        {
            _items.Clear();
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var loadedItems = JsonSerializer.Deserialize<List<Item>>(json);
            _items.Clear();

            if (loadedItems != null)
                _items.AddRange(loadedItems);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to load repository: {ex.Message}");
        }
    }
    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_items, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to save repository: {ex.Message}");
        }
    }
}

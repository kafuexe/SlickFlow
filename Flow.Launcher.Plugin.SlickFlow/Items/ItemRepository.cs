using System.Text.Json;

namespace Flow.Launcher.Plugin.SlickFlow.Items;

/// <summary>
/// Provides methods for managing a collection of <see cref="Item"/> objects, including loading, saving, and CRUD operations.
/// </summary>
public class ItemRepository 
{
    private readonly string _path;
    private readonly List<Item> _items = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemRepository"/> class and loads items from the specified path.
    /// </summary>
    /// <param name="path">The file path from which to load the items.</param>
    public ItemRepository(string path)
    {
        _path = path;
        Load();
    }
    /// <summary>
    /// Adds a new item to the repository and returns its assigned ID.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns>The ID assigned to the added item.</returns>
    public string AddItem(Item item)
    {
        var uuid = Guid.NewGuid().ToString();
        item.Id = uuid;

        _items.Add(item);
        Save();
        return item.Id;
    }

    /// <summary>
    /// Retrieves the item with the specified ID.
    /// </summary>
    /// <param name="id">The unique identifier of the item to retrieve.</param>
    /// <returns>The matching <see cref="Item"/> if found; otherwise, <c>null</c>.</returns>
    public Item? GetItemById(string id)
    {
        return _items.FirstOrDefault(i => i.Id == id);
    }

    /// <summary>
    /// Returns a new list containing all items in the collection.
    /// </summary>
    /// <returns>A list of all <see cref="Item"/> objects.</returns>
    public List<Item> GetAllItems()
    {
        return new List<Item>(_items);
    }

    /// <summary>
    /// Updates the existing item in the collection with the values of the specified item.
    /// </summary>
    /// <param name="item">The item containing updated values.</param>
    /// <exception cref="InvalidOperationException">Thrown when no item with the specified ID is found.</exception>
    public void UpdateItem(Item item)
    {
        var existing = _items.FirstOrDefault(i => i.Id == item.Id);
        if (existing == null)
            throw new InvalidOperationException($"Item with ID {item.Id} not found.");

        var index = _items.IndexOf(existing);
        _items[index] = item;
        Save();
    }

    /// <summary>
    /// Deletes the item with the specified ID and removes its associated icon file if it exists.
    /// </summary>
    /// <param name="id">The unique identifier of the item to delete.</param>
    public void DeleteItem(string id)
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
    
    /// <summary>
    /// Adds a new alias to the item with the specified ID if it does not already exist.
    /// </summary>
    /// <param name="itemId">The unique identifier of the item to which the alias will be added.</param>
    /// <param name="alias">The alias to add.</param>
    /// <exception cref="InvalidOperationException">Thrown when no item with the specified ID is found.</exception>
    public void AddAlias(string itemId, string alias)
    {
        var item = GetItemById(itemId);
        if (item == null) throw new InvalidOperationException($"Item with ID {itemId} not found.");
        if (!item.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
        {
            item.Aliases.Add(alias);
            Save();
        }
    }

    /// <summary>
    /// Removes the specified alias from the item with the given ID.
    /// </summary>
    /// <param name="itemId">The unique identifier of the item.</param>
    /// <param name="alias">The alias to remove from the item.</param>
    /// <exception cref="InvalidOperationException">Thrown when no item with the specified ID is found.</exception>
    public void RemoveAlias(string itemId, string alias)
    {
        var item = GetItemById(itemId);
        if (item == null) throw new InvalidOperationException($"Item with ID {itemId} not found.");
        if (item.RemoveAlias(alias) > 0)
            Save();
    }

    /// <summary>
    /// Returns the first item whose alias matches the specified alias, ignoring case.
    /// </summary>
    /// <param name="alias">The alias to look for.</param>
    /// <returns>The matching <see cref="Item"/> if found; otherwise, <c>null</c>.</returns>
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

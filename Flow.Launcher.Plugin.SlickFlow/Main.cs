using System.Reflection;
using Flow.Launcher.Plugin.SlickFlow.Stores;

namespace Flow.Launcher.Plugin.SlickFlow;

/// <summary>
/// Flow Launcher plugin for SlickFlow functionality
/// </summary>
public class SlickFlow : IPlugin
{
    #region Constants
    private PluginInitContext _context;
    private readonly string _dbDirectory = @"Settings\SlickFlow\SlickFlow.json";
    public static string AssemblyDirectory { get; } =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    private static string DataDirectory { get; } = Path.Combine(AssemblyDirectory, @"..\..\");
    private ItemRepository _itemRepo;
    private delegate List<Result> CommandHandler(string[] args);

    private Dictionary<string, CommandHandler> _commands;

    #endregion

    #region IPlugin Api

    public void Init(PluginInitContext context)
    {
        _context = context;
        _itemRepo = new ItemRepository(DataDirectory + _dbDirectory);
        _commands = new Dictionary<string, CommandHandler>(StringComparer.OrdinalIgnoreCase)
        {
            ["add"] = HandleAdd,
            ["alias"] = HandleAlias,
            ["remove"] = HandleRemove,
            ["delete"] = HandleDelete,
            ["update"] = HandleUpdate
        };
        _context.API.LogInfo("SlickFlow", "Plugin loaded successfully.");
    }

    /// <summary>
    /// Performs a search query and returns matching results
    /// </summary>
    /// <param name="query">The search query to process</param>
    /// <returns>A list of matching results</returns>
    public List<Result> Query(Query query)
    {
        var results = new List<Result>();
        var items = _itemRepo.GetAllItems();
        var parts = query.Search.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Handle commands
        if (parts.Length > 0 && _commands.TryGetValue(parts[0].ToLower(), out var handler))
        {
            var commandResults = handler(parts.Skip(1).ToArray());
            results.AddRange(commandResults);
        }

        results.AddRange(GetResults(query.Search, items));
        return results;

    }

    #endregion

    #region functions

    private List<Result> GetResults(string query, List<Item> items)
    {
        var results = new List<Result>();
        if (DoNotSearch(query, items))
            return results;

        var searchResults = Search(query, items);

        foreach (var (name, score, item) in searchResults)
        {
            results.Add(new Result()
            {
                Title = name,
                SubTitle = item.SubTitle,
                IcoPath = item.IconPath,
                Score = score,
                ContextData = this,
                Action = e =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            item.Execute(); // increments ExecCount
                            _itemRepo.UpdateItem(item); // persist updated ExecCount
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"[Error] Failed to execute or update item '{item.FileName}': {ex.Message}");
                        }
                    });

                    return true; // Flow Laun
                }
            });
        }

        return results;
    }

    private bool DoNotSearch(string query, List<Item> items)
    {
        if (string.IsNullOrEmpty(query))
            return true;

        if (string.IsNullOrWhiteSpace(query))
            return true;

        if (items == null || items.Count == 0)
            return true;
        return false;
    }

    private List<(string name, int score, Item item)> Search(string query, List<Item> items)
    {
        var results = new List<(string, int, Item)>();
        var queryLower = query.ToLower();

        foreach (var item in items)
        {
            foreach (var name in item.Aliases)
            {
                var nameLower = name.ToLower();
                int score = 0;

                if (nameLower == queryLower)
                {
                    score += 1000;
                }
                else if (nameLower.StartsWith(queryLower))
                {
                    score += 800;
                }
                else if (nameLower.Contains(queryLower))
                {
                    score += 600;
                }
                else if (queryLower.Contains(nameLower))
                {
                    score += 500;
                }

                // Bonus: shorter names get slight preference (faster typing)
                score += Math.Max(0, 100 - Math.Abs(nameLower.Length - queryLower.Length) * 5);

                // Optional: typo tolerance (Levenshtein distance)
                int distance = LevenshteinDistance(nameLower, queryLower);
                if (distance == 1)
                    score += 100;
                else if (distance == 2)
                    score += 50;

                if (score > 0)
                    results.Add((name, score, item));
            }
        }

        // Sort by descending score, then by shortest name (optional)
        return results
            .OrderByDescending(r => r.Item2)
            .ThenBy(r => r.Item1.Length)
            .ToList();
    }

    private int LevenshteinDistance(string a, string b)
    {
        int[,] dp = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost
                );
            }
        }

        return dp[a.Length, b.Length];
    }
    #endregion
    #region Handlers

    private List<Result> HandleAdd(string[] args)
    {
        var results = new List<Result>();

        if (args.Length < 2)
        {
            results.Add(new Result
            {
                Title = "Usage: add <alias1|alias2> <file> [args] [runas]",
                SubTitle = "Example: sf add note|notepad notepad.exe"
            });
            return results;
        }

        // Normalize and split aliases
        var aliases = args[0]
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim().ToLowerInvariant()) // case-insensitive
            .Distinct()
            .ToList();

        if (aliases.Count == 0)
        {
            results.Add(new Result { Title = "No valid aliases provided." });
            return results;
        }

        // Check for existing aliases
        var allItems = _itemRepo.GetAllItems();
        var existing = allItems
            .SelectMany(i => i.Aliases.Select(a => new { Item = i, Alias = a.ToLowerInvariant() }))
            .Where(x => aliases.Contains(x.Alias))
            .ToList();

        if (existing.Any())
        {
            var duplicates = string.Join(", ", existing.Select(x => x.Alias));
            results.Add(new Result
            {
                Title = $"Alias already exists: {duplicates}",
                SubTitle = "Remove it first or choose a different name."
            });
            return results;
        }

        // Create new item
        var file = args[1];
        var fileArgs = args.Length >= 3 ? args[2] : string.Empty;
        var runAs = args.Length >= 4 && int.TryParse(args[3], out int ra) ? ra : 0;

        var item = new Item
        {
            FileName = file,
            Arguments = fileArgs,
            RunAs = runAs,
            Aliases = aliases
        };

        // Save item
        var id = _itemRepo.AddItem(item);

        results.Add(new Result
        {
            Title = $"✅ Added item #{id}: {string.Join(", ", aliases)}",
            SubTitle = $"File: {file} {fileArgs}".Trim()
        });

        return results;
    }

    private List<Result> HandleRemove(string[] args)
    {
        var results = new List<Result>();
        if (args.Length < 1)
        {
            results.Add(new Result { Title = "Usage: sf remove <alias>" });
            return results;
        }

        var alias = args[0];
        var item = _itemRepo.GetItemByAlias(alias);
        if (item == null)
        {
            results.Add(new Result { Title = $"No item found with alias '{alias}'" });
            return results;
        }

        if (item.Aliases.Count <= 1)
        {
            results.Add(new Result
                { Title = $"Item only has one alias. Use 'sf delete {alias}' to delete the item instead." });
            return results;
        }

        _itemRepo.RemoveAlias(item.Id, alias);
        results.Add(new Result { Title = $"Removed alias '{alias}' from item {item.Id}" });
        return results;
    }
    private List<Result> HandleAlias(string[] args)
    {
        var results = new List<Result>();

        if (args.Length < 2)
        {
            results.Add(new Result { Title = "Usage: sf alias <existing-alias-or-id> <newAlias1|newAlias2>" });
            return results;
        }

        string target = args[0];
        Item? item = int.TryParse(target, out int id)
            ? _itemRepo.GetItemById(id)
            : _itemRepo.GetItemByAlias(target);

        if (item == null)
        {
            results.Add(new Result { Title = $"No item found with '{target}'" });
            return results;
        }

        var newAliases = args[1].Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();

        int addedCount = 0;
        foreach (var alias in newAliases)
        {
            if (!item.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
            {
                item.Aliases.Add(alias);
                addedCount++;
            }
        }

        if (addedCount > 0)
            _itemRepo.UpdateItem(item);

        results.Add(new Result
        {
            Title = addedCount > 0
                ? $"Added {addedCount} alias(es) to item {item.Id}"
                : "No new aliases added",
            SubTitle = $"Aliases: {string.Join(", ", item.Aliases)}"
        });

        return results;
    }
    private List<Result> HandleDelete(string[] args)
    {
        var results = new List<Result>();

        if (args.Length < 1)
        {
            results.Add(new Result { Title = "Usage: sf delete <alias-or-id>" });
            return results;
        }

        string target = args[0];
        Item? item = int.TryParse(target, out int id)
            ? _itemRepo.GetItemById(id)
            : _itemRepo.GetItemByAlias(target);

        if (item == null)
        {
            results.Add(new Result { Title = $"No item found with '{target}'" });
            return results;
        }

        // Confirm deletion
        results.Add(new Result
        {
            Title = $"Confirm delete of item {item.Id}?",
            SubTitle = $"Aliases: {string.Join(", ", item.Aliases)}",
            Action = e =>
            {
                _itemRepo.DeleteItem(item.Id);
                Console.WriteLine($"[Deleted] Item {item.Id} ({item.FileName})");
                return true;
            }
        });

        return results;
    }
    private List<Result> HandleUpdate(string[] args)
    {
        var results = new List<Result>();

        if (args.Length < 3)
        {
            results.Add(new Result
                { Title = "Usage: sf update <alias-or-id> <property> <value> [property value] ..." });
            return results;
        }

        string target = args[0];
        Item? item = int.TryParse(target, out int id)
            ? _itemRepo.GetItemById(id)
            : _itemRepo.GetItemByAlias(target);

        if (item == null)
        {
            results.Add(new Result { Title = $"No item found with '{target}'" });
            return results;
        }

        for (int i = 1; i < args.Length - 1; i += 2)
        {
            string prop = args[i].ToLowerInvariant();
            string val = args[i + 1];

            switch (prop)
            {
                case "args":
                case "arguments":
                    item.Arguments = val;
                    break;

                case "runas":
                    if (int.TryParse(val, out int ra))
                        item.RunAs = ra;
                    break;

                case "startmode":
                    if (int.TryParse(val, out int sm))
                        item.StartMode = sm;
                    break;

                case "subtitle":
                    item.SubTitle = val;
                    break;

                case "icon":
                case "iconpath":
                    item.IconPath = val;
                    break;

                case "workingdir":
                case "workdir":
                    item.WorkingDir = val;
                    break;

                default:
                    results.Add(new Result { Title = $"Unknown property '{prop}'" });
                    break;
            }
        }

        _itemRepo.UpdateItem(item);

        results.Add(new Result
        {
            Title = $"Updated item {item.Id}",
            SubTitle = $"File: {item.FileName} | Args: {item.Arguments}"
        });

        return results;
    }
    #endregion
}

using System.Reflection;
using Flow.Launcher.Plugin.SlickFlow.Stores;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.SlickFlow;

/// <summary>
/// Flow Launcher plugin for SlickFlow functionality
/// </summary>
public class SlickFlow : IPlugin
{
    #region Constants
    private PluginInitContext _context;
    private readonly string _dbDirectory = @"Settings\SlickFlow\SlickFlow.json";
    private readonly string _iconFolderDirectory = @"Settings\SlickFlow\icons\";
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
        var parts = SplitArgs(query.Search);

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
    public static string[] SplitArgs(string command)
    {
        var pattern = @"[\""].+?[\""]|[^ ]+";
        return Regex.Matches(command, pattern)
                .Cast<Match>()
                .Select(m => m.Value.Trim('"'))
                .ToArray();
    }
    private List<Result> GetResults(string query, List<Item> items)
    {
        var results = new List<Result>();
        if (DoNotSearch(query, items))
            return results;

        var searchResults = Search(query, items);

        foreach (var (name, score, item) in searchResults)
        {
            string iconPath;
            if (!item.AliasIcons.TryGetValue(name, out iconPath) || string.IsNullOrEmpty(iconPath))
            {
                iconPath = IconHelper.SaveIcon(item.FileName, item.Id, name, DataDirectory + _iconFolderDirectory);
                // Store the returned path (default or actual) in AliasIcons
                item.AliasIcons[name] = iconPath;
                _itemRepo.UpdateItem(item);
            }
            

            results.Add(new Result()
            {
                Title = name,
                SubTitle = item.SubTitle,
                IcoPath = iconPath,
                Score = score,
                ContextData = this,
                Action = e =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            item.Execute(); 
                            _itemRepo.UpdateItem(item); 
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"[Error] Failed to execute or update item '{item.FileName}': {ex.Message}");
                        }
                    });

                    return true; 
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
                    score = 1000;
                else if (nameLower.StartsWith(queryLower))
                    score = 800;
                else if (nameLower.Contains(queryLower))
                    score = 400; 
                else if (queryLower.Contains(nameLower))
                    score = 200; 

                // Bonus: slight preference for shorter names
                score += Math.Max(0, 50 - Math.Abs(nameLower.Length - queryLower.Length) * 2);
                int distance = LevenshteinDistance(nameLower, queryLower);
                if (distance == 1)
                    score += 50; // small boost for 1 character difference
                else if (distance == 2)
                    score += 5; // smaller boost for 2 character difference
                
                if (score > 0)
                    results.Add((name, score, item));
            }
        }

        return results
            .OrderByDescending(r => r.Item2) // score
            .ThenBy(r => r.Item1.Length) // shorter names first
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
        args = args.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
        var results = new List<Result>();

        if (args.Length < 2)
        {
            results.Add(new Result
            {
                Title = "Usage: add <alias1|alias2> <file-or-url> [args...] [runas]",
                Score = int.MaxValue - 1000
            });
            return results;
        }

        // Split aliases
        var aliases = args[0].Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        // File or URL (remove quotes only if they exist)
        var fileOrUrl = args[1].Trim();
        if ((fileOrUrl.StartsWith('"') && fileOrUrl.EndsWith('"')) ||
            (fileOrUrl.StartsWith('\'') && fileOrUrl.EndsWith('\'')))
        {
            fileOrUrl = fileOrUrl.Substring(1, fileOrUrl.Length - 2);
        }

        string fileArgs = string.Empty;
        int runAs = 0;

        if (args.Length > 2)
        {
            // Check if last argument is integer (runAs)
            if (int.TryParse(args[^1], out int ra))
            {
                runAs = ra;
                if (args.Length > 3)
                    fileArgs = string.Join(' ', args.Skip(2).Take(args.Length - 3));
            }
            else
            {
                fileArgs = string.Join(' ', args.Skip(2));
            }
        }

        // Prevent duplicate aliases
        var allItems = _itemRepo.GetAllItems();
        var existing = allItems.SelectMany(i => i.Aliases.Select(a => a.ToLowerInvariant()))
            .Intersect(aliases)
            .ToList();

        if (existing.Any())
        {
            results.Add(new Result
            {
                Title = $"Alias already exists: {string.Join(", ", existing)}"
            });
            return results;
        }

        // Create Result with Action to add the item
        results.Add(new Result
        {
            Title = $"Add item: {string.Join(", ", aliases)}",
            SubTitle = $"File: {fileOrUrl} {fileArgs}".Trim(),
            Score = int.MaxValue - 1000,
            Action = _ =>
            {
                var item = new Item
                {
                    FileName = fileOrUrl,
                    Arguments = fileArgs,
                    RunAs = runAs,
                    Aliases = aliases
                };

                // Add the item to the repository
                var id = _itemRepo.AddItem(item);

                // Save icons for each alias
                foreach (var alias in aliases)
                {
                    string iconPath = IconHelper.SaveIcon(fileOrUrl, id, alias, DataDirectory + _iconFolderDirectory);
                    if (!string.IsNullOrEmpty(iconPath))
                        item.AliasIcons[alias] = iconPath;
                }

                // Update the item with icon paths
                _itemRepo.UpdateItem(item);
                return true;
            }
        });

        return results;
    }


    private List<Result> HandleRemove(string[] args)
    {
        var results = new List<Result>();
        if (args.Length < 1)
        {
            results.Add(new Result
            {
                Title = "Usage: remove <alias>",
                Score = int.MaxValue - 1000,
            });
            return results;
        }

        var alias = args[0];
        var item = _itemRepo.GetItemByAlias(alias);
        if (item == null)
        {
            results.Add(new Result
            {
                Title = $"No item found with alias '{alias}'",
                Score = int.MaxValue - 1000,
            });
            return results;
        }

        if (item.Aliases.Count <= 1)
        {
            results.Add(new Result
            {
                Title = $"Item only has one alias. Use 'delete {alias}' to delete the item instead.",
                Score = int.MaxValue - 1000,
            });
            return results;
        }

        _itemRepo.RemoveAlias(item.Id, alias);
        results.Add(new Result
        {
            Title = $"Removed alias '{alias}' from item {item.Id}",
            Score = int.MaxValue - 1000,
        });
        return results;
    }

    private List<Result> HandleAlias(string[] args)
    {
        var results = new List<Result>();

        if (args.Length < 2)
        {
            results.Add(new Result
            {
                Title = "Usage: alias <existing-alias-or-id> <newAlias1|newAlias2>",
                Score = int.MaxValue - 1000,
            });
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

        if (!newAliases.Any())
        {
            results.Add(new Result { Title = "No valid new aliases provided" });
            return results;
        }

        results.Add(new Result
        {
            Title = $"Add {newAliases.Count} alias(es) to item {item.Id}",
            SubTitle = $"Existing aliases: {string.Join(", ", item.Aliases)}",
            Score = int.MaxValue - 1000,
            Action = _ =>
            {
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

                return true;
            }
        });

        return results;
    }

    private List<Result> HandleDelete(string[] args)
    {
        var results = new List<Result>();

        if (args.Length < 1)
        {
            results.Add(new Result
            {
                Title = "Usage: delete <alias-or-id>",
                Score = int.MaxValue - 1000
            });
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
            Score = int.MaxValue - 1000,
            SubTitle = $"Aliases: {string.Join(", ", item.Aliases)}",
            Action = _ =>
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
            {
                Score = int.MaxValue - 1000,
                Title = "Usage: update <alias-or-id> <property> <value> [property value] ..."
            });
            return results;
        }

        string target = args[0];

        // Just fetch the item for preview, don't change it yet
        Item? item = int.TryParse(target, out int id)
            ? _itemRepo.GetItemById(id)
            : _itemRepo.GetItemByAlias(target);

        if (item == null)
        {
            results.Add(new Result { Title = $"No item found with '{target}'" });
            return results;
        }

        // Create a copy of the updates for previewing
        var updates = new Dictionary<string, string>();
        for (int i = 1; i < args.Length - 1; i += 2)
        {
            string prop = args[i].ToLowerInvariant();
            string val = args[i + 1];
            updates[prop] = val;
        }

        // Show a result, actual update happens in Action
        results.Add(new Result
        {
            Title = $"Update item {item.Id}",
            Score = int.MaxValue - 1000,
            SubTitle = $"Properties to update: {string.Join(", ", updates.Select(kv => $"{kv.Key}={kv.Value}"))}",
            Action = _ =>
            {
                foreach (var kv in updates)
                {
                    string prop = kv.Key;
                    string val = kv.Value;

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

                        case "workingdir":
                        case "workdir":
                            item.WorkingDir = val;
                            break;
                    }
                }

                _itemRepo.UpdateItem(item);
                return true;
            }
        });

        return results;
    }

    #endregion
}

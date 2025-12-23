using System.Reflection;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Utils;

namespace Flow.Launcher.Plugin.SlickFlow;

/// <summary>
/// Flow Launcher plugin for SlickRun-like functionality
/// </summary>
public class SlickFlow : IPlugin
{
    #region Constants
    private delegate List<Result> CommandHandler(string[] args);
    internal PluginInitContext _context;
    internal ItemRepository _itemRepo;
    private Dictionary<string, CommandHandler> _commands;
    private static string AssemblyDirectory { get; } =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
    private static string DataDirectory { get; } = Path.Combine(AssemblyDirectory, @"..\..\");
    private readonly string _dbDirectory = @"Settings\SlickFlow\SlickFlow.json";
    internal IconHelper _iconHelper = new IconHelper(DataDirectory + @"Settings\SlickFlow\icons\");
    internal readonly string _slickFlowIcon = Path.Combine(AssemblyDirectory, "icon.ico");
    internal CommandProcessor _commandProcessor;
    internal ItemSearcher _itemSearcher;
    internal ItemValidator _itemValidator;
    #endregion

    #region IPlugin Api

    /// <summary>
    /// Initializes the plugin with the provided initialization context.
    /// </summary>
    /// <param name="context">The plugin initialization context.</param>
    public void Init(PluginInitContext context)
    {
        _context = context;
        _itemRepo = new ItemRepository(DataDirectory + _dbDirectory);
        _commandProcessor = new CommandProcessor(this);
        _itemSearcher = new ItemSearcher();
        _itemValidator = new ItemValidator(this);
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
        var parts = CommandParser.SplitArgs(query.Search);

        // Handle commands
        if (parts.Length > 0)
        {
            var commandResults = _commandProcessor.Process(parts[0].ToLower(), parts.Skip(1).ToArray());
            results.AddRange(commandResults);
        }

        results.AddRange(GetResults(query.Search, items));
        return results;

    }

    #endregion

    private List<Result> GetResults(string query, List<Item> items)
    {
        var results = new List<Result>();
        if (DoNotSearch(query, items))
            return results;

        var searchResults = _itemSearcher.Search(query, items);

        foreach (var (name, score, item) in searchResults)
        {
            var iconPath = item.IconPath;
            // Update icon asynchronously
            Task.Run(async () =>
            {
                try
                {
                    var newIconPath = await _iconHelper.SaveIconAsync(item.FileName, item.Id);
                    if (newIconPath != item.IconPath)
                    {
                        item.IconPath = newIconPath;
                        _itemRepo.UpdateItem(item);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Failed to update icon for item '{item.FileName}': {ex.Message}");
                }
            });


            results.Add(new Result()
            {
                Title = name,
                SubTitle = item.SubTitle,
                IcoPath = iconPath,
                Score = score,
                ContextData = this,
                Action = _ =>
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

        if (items.Count == 0)
            return true;
        return false;
    }



}
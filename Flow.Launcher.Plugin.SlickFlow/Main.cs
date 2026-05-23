using System.IO;
using System.Reflection;
using System.Windows.Controls;
using Flow.Launcher.Plugin.SlickFlow.Commands;
using Flow.Launcher.Plugin.SlickFlow.ContextMenuResults;
using Flow.Launcher.Plugin.SlickFlow.items;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.Items.Parameters;
using Flow.Launcher.Plugin.SlickFlow.Settings;
using Flow.Launcher.Plugin.SlickFlow.Utils;
using Flow.Launcher.Plugin.SlickFlow.Utils.Icons;
using Flow.Launcher.Plugin.SlickFlow.ViewModels.Settings;

namespace Flow.Launcher.Plugin.SlickFlow;

/// <summary>
/// Flow Launcher plugin for SlickRun-like functionality
/// </summary>
public class SlickFlow : IPlugin, IContextMenu , ISettingProvider
{
    #region Constants
    internal PluginInitContext _context = null!;
    internal ItemRepository _itemRepo = null!;
    public static string AssemblyDirectory { get; } =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

    internal IconHelper _iconHelper = null!;
    internal readonly string _slickFlowIcon = Path.Combine(AssemblyDirectory, "icon.ico");
    internal CommandProcessor _commandProcessor = null!;
    internal ItemSearcher _itemSearcher = null!;
    internal ItemValidator _itemValidator = null!;
    public Settings.Settings Settings { get; set; } = new();

    #endregion

    #region IPlugin Api

    /// <summary>
    /// Initializes the plugin with the provided initialization context.
    /// </summary>
    /// <param name="context">The plugin initialization context.</param>
    public void Init(PluginInitContext context)
    {
        _context = context;
        Settings = SettingsManager.Load();
        _iconHelper = new IconHelper(Settings.IconDirPath);
        _itemRepo = new ItemRepository(Settings.DbFilePath);
        _itemValidator = new ItemValidator(_itemRepo, _slickFlowIcon);
        _commandProcessor = new CommandProcessor(_itemRepo, _itemValidator, _iconHelper, _slickFlowIcon);
        _itemSearcher = new ItemSearcher();
        _context.API.LogInfo("SlickFlow", "Plugin loaded successfully.");
        
    }

    /// <summary>
    /// Performs a search query and returns matching results
    /// </summary>
    /// <param name="query">The search query to process</param>
    /// <returns>A list of matching results</returns>
    public List<Result> Query(Query query)
    {
        // Prompt-mode short-circuit: if the user is filling placeholders for an
        // item, the bar holds a special "<alias> | k=v | ... | name: input" pattern.
        // Bypass commands and normal search when we recognize it.
        var promptState = PromptModeParser.TryParse(query.Search);
        if (promptState != null)
        {
            var promptResults = new PromptModeHandler(_itemRepo, _context.API).BuildResults(promptState);
            if (promptResults.Count > 0)
                return promptResults;
        }

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

    /// <summary>
    /// on context menu of item - loads the correct results
    /// </summary>
    /// <param name="selectedResult"></param>
    /// <returns></returns>
    public List<Result> LoadContextMenus(Result selectedResult)
    {
        var item = _itemRepo.GetItemByAlias(selectedResult.Title);
        if (item is null)
            return new List<Result>();

        var builder = new ContextMenuBuilder();
        return builder.Build(selectedResult, item, _itemRepo);
    }


    /// <summary>
    ///  slickFlows settings
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
        /// <summary>
        /// Creates the settings panel UI for the plugin.
        /// </summary>
        /// <returns>A WPF UserControl for settings.</returns>
        public System.Windows.Controls.Control CreateSettingPanel()
    {
            return new SettingsView(_itemRepo);
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
                    var iconResult = await _iconHelper.TrySaveIconAsync(item.FileName, item.Id);
                    if (iconResult.SavedPath != item.IconPath)
                    {
                        item.IconPath = iconResult.SavedPath;
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
                    if (TryEnterPromptMode(name, item))
                        return false;

                    Task.Run(() =>
                    {
                        try
                        {
                            item.Execute(itemRepo: _itemRepo);
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

    private bool TryEnterPromptMode(string alias, Item item)
    {
        // Only items with placeholders (directly or via meta-chain leaves) trigger
        // sequential prompts. The schema collection is cycle-safe and throws on
        // unresolved aliases - either way, fall back to direct execution.
        if (!item.IsParameterized && !item.IsMetaItem)
            return false;

        IReadOnlyList<Placeholder> schema;
        try
        {
            schema = PlaceholderSchema.From(item, _itemRepo);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (schema.Count == 0)
            return false;

        var first = schema[0];
        var newQuery = PromptModeParser.Format(
            alias,
            filled: Array.Empty<(string, string)>(),
            nextName: first.Name,
            nextInitial: first.Default ?? "");
        _context.API.ChangeQuery(newQuery, requery: true);
        return true;
    }
}
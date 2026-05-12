using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SlickFlow.Items.Abstract;

namespace Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

/// <summary>
/// Drives the sequential-prompt UX. Given a parsed <see cref="PromptModeState"/>,
/// builds the guided <see cref="Result"/> the user sees in Flow Launcher while
/// filling in placeholders. Pressing Enter advances to the next prompt via
/// <c>IPublicAPI.ChangeQuery</c>, or executes the item when the last placeholder
/// is filled.
/// </summary>
public class PromptModeHandler
{
    private readonly IItemRepository _repo;
    private readonly IPublicAPI _api;

    public PromptModeHandler(IItemRepository repo, IPublicAPI api)
    {
        _repo = repo;
        _api = api;
    }

    public List<Result> BuildResults(PromptModeState state)
    {
        var item = _repo.GetItemByAlias(state.Alias);
        if (item == null)
            return new List<Result>();

        IReadOnlyList<Placeholder> schema;
        try
        {
            schema = PlaceholderSchema.From(item, _repo);
        }
        catch (InvalidOperationException)
        {
            return new List<Result>();
        }

        if (schema.Count == 0)
            return new List<Result>();

        var currentIndex = IndexOfName(schema, state.CurrentName);
        if (currentIndex < 0)
            return new List<Result>();

        var values = new Dictionary<string, string>();
        foreach (var (n, v) in state.Filled)
            values[n] = v;
        values[state.CurrentName] = state.CurrentInput;

        var title = item.IsMetaItem
            ? $"{state.Alias} (chain)"
            : PlaceholderParser.Substitute(item.FileName, values);

        var ph = schema[currentIndex];
        var hint = string.IsNullOrEmpty(ph.Hint) ? "" : $" ({ph.Hint})";
        var isLast = currentIndex == schema.Count - 1;
        var subtitle = isLast
            ? $"Set {ph.Name}{hint} — Press Enter to launch"
            : $"Set {ph.Name}{hint} — Press Enter for {schema[currentIndex + 1].Name}";

        return new List<Result>
        {
            new Result
            {
                // Score must dominate any other plugin's result (web search, etc.) so
                // the user lands on the prompt result on Enter. Without this, an empty
                // Score (0) lets unrelated plugins outrank the active prompt.
                Score = int.MaxValue,
                Title = title,
                SubTitle = subtitle,
                IcoPath = item.IconPath,
                Action = _ => AdvanceOrExecute(item, state, schema, values, currentIndex)
            }
        };
    }

    private bool AdvanceOrExecute(
        Item item,
        PromptModeState state,
        IReadOnlyList<Placeholder> schema,
        Dictionary<string, string> values,
        int currentIndex)
    {
        if (currentIndex >= schema.Count - 1)
        {
            try
            {
                item.Execute(itemRepo: _repo, values: values);
                _repo.UpdateItem(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to execute '{item.FileName}': {ex.Message}");
            }
            return true;
        }

        var newFilled = new List<(string, string)>(state.Filled)
        {
            (state.CurrentName, state.CurrentInput)
        };
        var next = schema[currentIndex + 1];
        var newQuery = PromptModeParser.Format(
            state.Alias,
            newFilled,
            next.Name,
            next.Default ?? "");
        _api.ChangeQuery(newQuery, requery: true);
        return false;
    }

    private static int IndexOfName(IReadOnlyList<Placeholder> schema, string name)
    {
        for (int i = 0; i < schema.Count; i++)
            if (schema[i].Name == name)
                return i;
        return -1;
    }
}

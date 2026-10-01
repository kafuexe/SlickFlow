namespace Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

/// <summary>
/// The outside-world inputs that built-in placeholder tokens (<c>$clip</c>,
/// <c>$date</c>, <c>$time</c>, <c>$env:NAME</c>) resolve against. Abstracted so
/// token resolution can be unit-tested without touching the real clipboard,
/// clock or process environment.
/// </summary>
public interface ITokenSources
{
    DateTime Now { get; }

    /// <summary>Current clipboard text, or an empty string when there is none.</summary>
    string GetClipboardText();

    /// <summary>Value of an environment variable, or <c>null</c> when unset.</summary>
    string? GetEnvironmentVariable(string name);
}

/// <summary>
/// Wraps another <see cref="ITokenSources"/> so a single launch sees one
/// consistent snapshot: the clock is frozen and the clipboard is read at most
/// once (lazily, only if a <c>$clip</c> token is actually used).
/// </summary>
public sealed class CachingTokenSources : ITokenSources
{
    private readonly ITokenSources _inner;
    private readonly Lazy<DateTime> _now;
    private readonly Lazy<string> _clipboard;

    public CachingTokenSources(ITokenSources inner)
    {
        _inner = inner;
        _now = new Lazy<DateTime>(() => inner.Now);
        _clipboard = new Lazy<string>(inner.GetClipboardText);
    }

    public DateTime Now => _now.Value;

    public string GetClipboardText() => _clipboard.Value;

    public string? GetEnvironmentVariable(string name) => _inner.GetEnvironmentVariable(name);
}

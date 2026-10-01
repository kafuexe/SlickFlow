using Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

namespace SlickFlow.Tests.Unit.Items.Parameters;

/// <summary>
/// Deterministic <see cref="ITokenSources"/> for tests: never touches the real
/// clipboard, clock or process environment.
/// </summary>
internal sealed class FakeTokenSources : ITokenSources
{
    public DateTime Now { get; set; } = new(2024, 3, 7, 9, 5, 42);
    public string Clipboard { get; set; } = string.Empty;
    public Dictionary<string, string> Env { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int ClipboardReads { get; private set; }

    public string GetClipboardText()
    {
        ClipboardReads++;
        return Clipboard;
    }

    public string? GetEnvironmentVariable(string name) =>
        Env.TryGetValue(name, out var v) ? v : null;
}

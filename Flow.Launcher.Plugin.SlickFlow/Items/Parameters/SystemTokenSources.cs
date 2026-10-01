using System.Runtime.InteropServices;
using Clipboard = System.Windows.Clipboard;

namespace Flow.Launcher.Plugin.SlickFlow.Items.Parameters;

/// <summary>
/// Real <see cref="ITokenSources"/>: system clock, process environment and the
/// Windows clipboard.
/// </summary>
public sealed class SystemTokenSources : ITokenSources
{
    public static readonly SystemTokenSources Instance = new();

    private const int ClipboardAttempts = 5;
    private static readonly TimeSpan ClipboardRetryDelay = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan ClipboardThreadTimeout = TimeSpan.FromSeconds(2);

    private SystemTokenSources() { }

    public DateTime Now => DateTime.Now;

    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    /// <summary>
    /// The clipboard is an OLE API that requires an STA thread, but items are
    /// executed from <c>Task.Run</c> (MTA thread-pool threads). When the caller
    /// is not STA, the read is marshalled onto a short-lived dedicated STA thread.
    /// Never throws: a locked clipboard is retried briefly, and any persistent
    /// failure (or a timeout) yields an empty string.
    /// </summary>
    public string GetClipboardText()
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            return ReadClipboardWithRetry();

        var text = string.Empty;
        var thread = new Thread(() => text = ReadClipboardWithRetry())
        {
            IsBackground = true,
            Name = "SlickFlow clipboard reader"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return thread.Join(ClipboardThreadTimeout) ? text : string.Empty;
    }

    private static string ReadClipboardWithRetry()
    {
        for (int attempt = 1; attempt <= ClipboardAttempts; attempt++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            }
            catch (ExternalException) when (attempt < ClipboardAttempts)
            {
                // CLIPBRD_E_CANT_OPEN: another process holds the clipboard open.
                Thread.Sleep(ClipboardRetryDelay);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to read clipboard: {ex.Message}");
                return string.Empty;
            }
        }
        return string.Empty;
    }
}

using System.Diagnostics;
using System.IO;

namespace Flow.Launcher.Plugin.SlickFlow.ViewModels.Settings;

/// <summary>
/// Dialogs used by <see cref="SettingsViewModel"/>, behind an interface so the view model stays testable.
/// </summary>
public interface ISettingsDialogService
{
    /// <summary>Asks the user for a SlickRun MagicWord file; returns null when cancelled.</summary>
    string? PickSlickRunFile(string? initialDirectory);

    /// <summary>Shows an informational (or error) message.</summary>
    void ShowMessage(string title, string message, bool isError = false);

    /// <summary>Opens a URL in the default browser.</summary>
    void OpenUrl(string url);
}

/// <summary>WinForms-backed implementation used at runtime.</summary>
public sealed class SettingsDialogService : ISettingsDialogService
{
    public string? PickSlickRunFile(string? initialDirectory)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Import from SlickRun",
            // SlickRun keeps its live database in SlickRun.srl and exports packs as .qrs; same format.
            Filter = "SlickRun MagicWords (*.qrs;*.srl)|*.qrs;*.srl|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            dlg.InitialDirectory = initialDirectory;

        return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
    }

    public void ShowMessage(string title, string message, bool isError = false)
    {
        MessageBox.Show(message, title, MessageBoxButtons.OK,
            isError ? MessageBoxIcon.Error : MessageBoxIcon.Information);
    }

    public void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}

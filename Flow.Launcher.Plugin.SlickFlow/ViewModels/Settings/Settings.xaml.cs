using System.Collections.ObjectModel;
using Flow.Launcher.Plugin.SlickFlow.Items;
using Flow.Launcher.Plugin.SlickFlow.ViewModels.Item;

namespace Flow.Launcher.Plugin.SlickFlow.ViewModels.Settings;

/// <summary>
/// Settings view code-behind.
/// </summary>
public partial class SettingsView 
{
    /// <summary>
    /// Initializes a new instance of the SettingsView class.
    /// </summary>
    public SettingsView(ItemRepository repo)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(repo);

    }
}

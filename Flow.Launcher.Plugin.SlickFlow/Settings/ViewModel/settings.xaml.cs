using System.Windows.Controls;

namespace Flow.Launcher.Plugin.SlickFlow.Settings.ViewModel;

/// <summary>
/// Settings view code-behind.
/// </summary>
public partial class SettingsView : System.Windows.Controls.UserControl
{
    /// <summary>
    /// Initializes a new instance of the SettingsView class.
    /// </summary>
    public SettingsView()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel();
    }
}

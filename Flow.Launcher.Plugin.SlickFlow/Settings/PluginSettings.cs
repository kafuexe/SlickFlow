using System.Collections.ObjectModel;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace Flow.Launcher.Plugin.SlickFlow.Settings;


public class Settings
{
  public string DbFilePath { get; set; } = string.Empty;
  public string IconDirPath { get; set; } = string.Empty;

}

static class SettingsLoader
{
  // Placeholder for future item-loading from a DB file in DbFilePath.
  public static void LoadItemsFileToSettings(Settings settings)
  {
    // Intentionally left blank for now. Items will be loaded from the DbFilePath later.
  }
}
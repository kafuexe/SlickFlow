using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Flow.Launcher.Plugin.SlickFlow.Settings;

public static class SettingsManager
{
    private const string SettingsFileName = "settings.json";

    private static string GetSettingsFilePath()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                    ?? AppContext.BaseDirectory;
        var defaultDir = Path.GetFullPath(
            Path.Combine(assemblyDir, "..", "..", "Settings", "SlickFlow"));
        return Path.Combine(defaultDir, SettingsFileName);
    }

    public static Settings Load()
    {
        var path = GetSettingsFilePath();

        try
        {
            if (!File.Exists(path))
                return CreateDefaultSettings();

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var settings = JsonSerializer.Deserialize<Settings>(json, options);

            if (settings == null)
                return CreateDefaultSettings();

            return settings;
        }
        catch
        {
            return CreateDefaultSettings();
        }
    }

    public static void Save(Settings settings)
    {
        var path = GetSettingsFilePath();
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(settings, options);
        File.WriteAllText(path, json);
    }



    private static Settings CreateDefaultSettings()
{
    var defaults = new Settings();

    try
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                          ?? AppContext.BaseDirectory;

        var defaultDir = Path.GetFullPath(
            Path.Combine(assemblyDir, "..", "..", "Settings", "SlickFlow"));

        Directory.CreateDirectory(defaultDir);

        var dbFile = Path.Combine(defaultDir, "SlickFlow.json");

        if (!File.Exists(dbFile))
            File.WriteAllText(dbFile, "{}");

        defaults.DbFilePath = dbFile;
    }
    catch
    {
        defaults.DbFilePath = string.Empty;
    }

    Save(defaults);
    return defaults;
}

}

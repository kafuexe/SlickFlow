using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Flow.Launcher.Plugin.SlickFlow.Settings
{
    public static class SettingsManager
    {
        private const string SettingsFileName = "settings.json";

        private static string GetSettingsFilePath(string? baseDirectory = null)
        {
            var dir = baseDirectory ?? GetDefaultDirectory();
            return Path.Combine(dir, SettingsFileName);
        }

        private static string GetDefaultDirectory()
        {
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                              ?? AppContext.BaseDirectory;
            return Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "Settings", "SlickFlow"));
        }

        public static Settings Load(string? baseDirectory = null)
        {
            var path = GetSettingsFilePath(baseDirectory);

            try
            {
                if (!File.Exists(path))
                    return CreateDefaultSettings(baseDirectory);

                var json = File.ReadAllText(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var settings = JsonSerializer.Deserialize<Settings>(json, options) ?? CreateDefaultSettings(baseDirectory);

                ValidateAndFillDefaults(settings, baseDirectory);
                return settings;
            }
            catch
            {
                return CreateDefaultSettings(baseDirectory);
            }
        }

        public static void Save(Settings settings, string? baseDirectory = null)
        {
            var path = GetSettingsFilePath(baseDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(settings, options));
        }

        private static Settings CreateDefaultSettings(string? baseDirectory = null)
        {
            var defaults = new Settings();
            InitializeDefaultPaths(defaults, baseDirectory);
            Save(defaults, baseDirectory);
            return defaults;
        }

        private static void InitializeDefaultPaths(Settings settings, string? baseDirectory = null)
        {
            var defaultDir = baseDirectory ?? GetDefaultDirectory();
            Directory.CreateDirectory(defaultDir);

            if (string.IsNullOrWhiteSpace(settings.DbFilePath))
                settings.DbFilePath = Path.Combine(defaultDir, "SlickFlow.json");

            if (!File.Exists(settings.DbFilePath))
                File.WriteAllText(settings.DbFilePath, "{}");

            if (string.IsNullOrWhiteSpace(settings.IconDirPath))
                settings.IconDirPath = Path.Combine(defaultDir, "icons");

            Directory.CreateDirectory(settings.IconDirPath);
        }

        private static void ValidateAndFillDefaults(Settings settings, string? baseDirectory = null)
        {
            InitializeDefaultPaths(settings, baseDirectory);
        }
    }
}

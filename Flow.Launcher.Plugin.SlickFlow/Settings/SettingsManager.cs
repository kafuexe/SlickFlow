using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Flow.Launcher.Plugin.SlickFlow.Settings
{
    public static class SettingsManager
    {
        private const string SettingsFileName = "settings.json";

        /// <summary>
        /// Returns the full path to settings.json based on assembly-relative default directory.
        /// </summary>
        private static string GetSettingsFilePath()
        {
            var defaultDir = GetDefaultDirectory();
            return Path.Combine(defaultDir, SettingsFileName);
        }

        /// <summary>
        /// Returns the default SlickFlow settings directory relative to the assembly.
        /// </summary>
        private static string GetDefaultDirectory()
        {
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                              ?? AppContext.BaseDirectory;
            return Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "Settings", "SlickFlow"));
        }

        /// <summary>
        /// Loads settings.json or creates defaults if missing/corrupted.
        /// </summary>
        public static Settings Load()
        {
            var path = GetSettingsFilePath();

            try
            {
                if (!File.Exists(path))
                    return CreateDefaultSettings();

                var json = File.ReadAllText(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var settings = JsonSerializer.Deserialize<Settings>(json, options) ?? CreateDefaultSettings();

                ValidateAndFillDefaults(settings);
                return settings;
            }
            catch
            {
                // Corrupted file or other IO issues
                return CreateDefaultSettings();
            }
        }

        /// <summary>
        /// Saves settings to settings.json.
        /// </summary>
        public static void Save(Settings settings)
        {
            var path = GetSettingsFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); // ensure folder exists
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(settings, options));
        }

        #region Default Initialization

        /// <summary>
        /// Creates default settings with default DB file and icon folder.
        /// </summary>
        private static Settings CreateDefaultSettings()
        {
            var defaults = new Settings();
            InitializeDefaultPaths(defaults);
            Save(defaults);
            return defaults;
        }

        /// <summary>
        /// Ensures DB file and icon folder exist, sets default paths if missing.
        /// </summary>
        private static void InitializeDefaultPaths(Settings settings)
        {
            var defaultDir = GetDefaultDirectory();
            Directory.CreateDirectory(defaultDir);

            // DB file
            if (string.IsNullOrWhiteSpace(settings.DbFilePath))
                settings.DbFilePath = Path.Combine(defaultDir, "SlickFlow.json");

            if (!File.Exists(settings.DbFilePath))
                File.WriteAllText(settings.DbFilePath, "{}");

            // Icon directory
            if (string.IsNullOrWhiteSpace(settings.IconDirPath))
                settings.IconDirPath = Path.Combine(defaultDir, "icons");

            Directory.CreateDirectory(settings.IconDirPath);
        }

        /// <summary>
        /// Validates settings and fills in only missing paths, preserving user-set values.
        /// </summary>
        private static void ValidateAndFillDefaults(Settings settings)
        {
            InitializeDefaultPaths(settings);
        }

        #endregion
    }
}

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
namespace Flow.Launcher.Plugin.SlickFlow
{
    public class Item
    {
        public int Id { get; set; }
        public string Arguments { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string SubTitle { get; set; } = string.Empty;
        public int RunAs { get; set; } = 0;
        public int StartMode { get; set; } = 0;
        public string WorkingDir { get; set; } = string.Empty;
        public int ExecCount { get; set; }
        public List<string> Aliases { get; set; } = new();
        public Dictionary<string, string> AliasIcons { get; set; } = new();

        public Item() { }
        public Item(int id, string fileName, IEnumerable<string>? aliases = null)
        {
            Id = id;
            FileName = fileName;
            if (aliases != null)
                Aliases = new List<string>(aliases);
        }
        public void AddAlias(string alias)
        {
            if (!Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
                Aliases.Add(alias);
        }
        public int RemoveAlias(string alias)
        {
            return Aliases.RemoveAll(a => string.Equals(a, alias, StringComparison.OrdinalIgnoreCase));
        }
        public bool MatchesQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return false;

            query = query.ToLowerInvariant();

            return FileName.ToLowerInvariant().Contains(query)
                || SubTitle.ToLowerInvariant().Contains(query)
                || Aliases.Any(a => a.ToLowerInvariant().Contains(query));
        }

        public void Execute()
        {
            try
            {
                

                if (!IsUrl(FileName) && !File.Exists(FileName))
                {
                    string sysPath = Path.Combine(Environment.SystemDirectory, FileName);
                    if (File.Exists(sysPath))
                    {
                        FileName = sysPath;
                    }
                }

                var psi = new ProcessStartInfo
                {
                    FileName = FileName,
                    Arguments = Arguments,
                    WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDir)
                        ? Environment.CurrentDirectory
                        : WorkingDir,
                    UseShellExecute = true // Required for URLs or file associations
                };

                // Run as admin if requested (only for files, not URLs)
                if (RunAs == 1 && !IsUrl(FileName))
                    psi.Verb = "runas";

                // Set window style
                psi.WindowStyle = StartMode switch
                {
                    1 => ProcessWindowStyle.Minimized,
                    2 => ProcessWindowStyle.Maximized,
                    _ => ProcessWindowStyle.Normal
                };

                Process.Start(psi);
                ExecCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to execute '{FileName}': {ex.Message}");
            }
        }


        private bool IsUrl(string fileName)
        {
            return Uri.TryCreate(fileName, UriKind.Absolute, out var uriResult) 
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);   
        }
        public override string ToString()
        {
            var aliases = Aliases.Count > 0 ? string.Join(", ", Aliases) : "none";
            return $"[#{Id}] {FileName} ({Arguments}) | Aliases=[{aliases}] | RunAs={RunAs}, StartMode={StartMode}, ExecCount={ExecCount}";
        }
    }
}

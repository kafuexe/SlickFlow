using Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Abstract;
using Flow.Launcher.Plugin.SlickFlow.ContextMenuResults.Results;
using Flow.Launcher.Plugin.SlickFlow.Items;

namespace Flow.Launcher.Plugin.SlickFlow.ContextMenuResults
{
    public class ContextMenuBuilder
    {
        private readonly List<ContextMenuProvider> _providers = new()
        {
            OpenPathProvider.Provide,
            RunAsAdministratorProvider.Provide,
            OpenIncognitoProvider.Provide,
            OpenInTerminalProvider.Provide,
            OpenInPowerShellProvider.Provide,
            ExecutionCountProvider.Provide,
            AliasesProvider.Provide
        };

        public List<Result> Build(Result selectedResult, Item item)
        {
            var results = new List<Result>();

            foreach (var provider in _providers)
            {
                var result = provider(selectedResult, item);
                if (result != null)
                    results.Add(result);
            }

            return results;
        }
    }
}

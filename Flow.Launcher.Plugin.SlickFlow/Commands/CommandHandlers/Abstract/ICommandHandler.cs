using System.Collections.Generic;

namespace Flow.Launcher.Plugin.SlickFlow;

public interface ICommandHandler
{
    List<Result> Handle(string[] args);
}
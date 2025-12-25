    namespace Flow.Launcher.Plugin.SlickFlow.Utils;

    internal static class ExceptionExtensions
    {
      /// <summary>
      /// Returns <c>true</c> for exceptions that should NOT be caught by generic catch blocks.
      /// This mirrors the behaviour of the BCL: OutOfMemory, StackOverflow, ThreadAbort, etc.
      /// </summary>
      public static bool IsCritical(this Exception ex) =>
        ex is OutOfMemoryException ||
        ex is StackOverflowException ||
        ex is ThreadAbortException ||
        ex is AccessViolationException ||
        ex is AppDomainUnloadedException;
    }
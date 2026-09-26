namespace GLOptimizer.Core.Models;

public enum GameRunStatus
{
    Unknown = 0,
    Stopped = 1,
    Running = 2
}

public enum GamePresenceStatus
{
    Unknown = 0,
    NotFound = 1,
    Installed = 2
}

public enum DiagnosticState
{
    Good = 0,
    Warning = 1,
    ActionRequired = 2
}

using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

public interface IStartupRegistration
{
    bool IsSupported { get; }

    OperationResult<string?> ReadCommand(string valueName);

    OperationResult SetCommand(string valueName, string command);

    OperationResult Remove(string valueName);
}

public static class StartupRegistrationRules
{
    public const string ValueName = "GL Optimizer";

    public static OperationResult Apply(IStartupRegistration registration, bool enabled, string command)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (!enabled)
        {
            return registration.Remove(ValueName);
        }

        if (!registration.IsSupported)
        {
            return OperationResult.Failure("Start with Windows is only available on Windows.");
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return OperationResult.Failure("The startup command is missing.");
        }

        return registration.SetCommand(ValueName, command);
    }
}

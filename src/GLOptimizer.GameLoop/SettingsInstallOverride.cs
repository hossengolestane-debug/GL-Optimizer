using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;

namespace GLOptimizer.GameLoop;

public interface IInstallOverrideSource
{
    string? OverridePath { get; }
}

public sealed class SettingsInstallOverride : IInstallOverrideSource
{
    private readonly ISettingsStore _settings;

    public SettingsInstallOverride(ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    public string? OverridePath => _settings.Current.GameLoopPathOverride;
}

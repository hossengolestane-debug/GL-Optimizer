namespace GLOptimizer.Core.Settings;

/// <summary>
/// Dark is the only theme that is applied. Light is stored so a later build can turn it on.
/// </summary>
public static class ThemePolicy
{
    public static AppTheme Applied(AppTheme requested) => AppTheme.Dark;

    public static bool IsImplemented(AppTheme theme) => theme == AppTheme.Dark;

    public const string LightNotImplemented = "Light theme is not implemented. Dark remains in use.";
}

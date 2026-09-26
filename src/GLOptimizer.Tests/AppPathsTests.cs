using GLOptimizer.Core.Paths;

namespace GLOptimizer.Tests;

public class AppPathsTests
{
    [Fact]
    public void Root_is_the_GLOptimizer_folder_under_local_app_data()
    {
        var root = AppPaths.GetRoot(Path.Combine("C:", "Users", "Ada", "AppData", "Local"));

        Assert.Equal(
            Path.Combine("C:", "Users", "Ada", "AppData", "Local", "GLOptimizer"),
            root);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Root_rejects_a_blank_base(string localAppData)
    {
        Assert.Throws<ArgumentException>(() => AppPaths.GetRoot(localAppData));
    }

    [Fact]
    public void Settings_and_logs_stay_inside_the_root()
    {
        var root = AppPaths.GetRoot(Path.Combine("home", "ada", ".local", "share"));

        Assert.Equal(Path.Combine(root, "settings.json"), AppPaths.GetSettingsFile(root));
        Assert.Equal(Path.Combine(root, "Logs"), AppPaths.GetLogsDirectory(root));
        Assert.Equal(
            Path.Combine(root, "Logs", "gloptimizer.log"),
            AppPaths.GetActiveLogFile(AppPaths.GetLogsDirectory(root)));
    }

    [Fact]
    public void Default_root_uses_the_process_local_app_data_folder()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
        {
            return;
        }

        Assert.Equal(Path.Combine(local, AppPaths.FolderName), AppPaths.GetDefaultRoot());
    }
}

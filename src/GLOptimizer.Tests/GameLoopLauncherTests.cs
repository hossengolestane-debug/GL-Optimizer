using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;
using GLOptimizer.GameLoop;

namespace GLOptimizer.Tests;

public class GameLoopLauncherTests
{
    [Fact]
    public async Task Start_launches_only_a_file_inside_the_install()
    {
        var root = Path.Combine(Path.GetTempPath(), "glopt-start-" + Guid.NewGuid().ToString("N"));
        var launcher = Path.Combine(root, "GameLoop.exe");
        var outside = Path.Combine(Path.GetTempPath(), "glopt-outside-" + Guid.NewGuid().ToString("N") + ".exe");
        Directory.CreateDirectory(root);
        File.WriteAllText(launcher, "launcher");
        File.WriteAllText(outside, "outside");
        var starter = new RecordingStarter();
        var launcherService = new GameLoopLauncher(starter);
        try
        {
            var started = await launcherService.StartAsync(new GameLoopInstallation
            {
                InstallPath = root,
                LauncherPath = launcher
            });
            var rejected = await launcherService.StartAsync(new GameLoopInstallation
            {
                InstallPath = root,
                LauncherPath = outside
            });
            var closed = await launcherService.CloseAsync(new GameLoopInstallation { InstallPath = root, LauncherPath = launcher });
            var restarted = await launcherService.RestartAsync(new GameLoopInstallation { InstallPath = root, LauncherPath = launcher });

            Assert.Equal(OperationStatus.Success, started.Status);
            Assert.Equal(OperationStatus.Failed, rejected.Status);
            Assert.Equal(OperationStatus.NotImplemented, closed.Status);
            Assert.Equal(OperationStatus.NotImplemented, restarted.Status);
            Assert.Equal(1, starter.Calls);
            Assert.Equal(Path.GetFullPath(launcher), starter.LastPath);
            Assert.Contains("not implemented", closed.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not implemented", restarted.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
            File.Delete(outside);
        }
    }

    private sealed class RecordingStarter : IProcessStarter
    {
        public int Calls { get; private set; }

        public string? LastPath { get; private set; }

        public void Start(string executablePath)
        {
            Calls++;
            LastPath = executablePath;
        }
    }
}

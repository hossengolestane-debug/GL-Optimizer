using GLOptimizer.Core.Detection;

namespace GLOptimizer.Tests;

public class InstallPathRulesTests
{
    [Fact]
    public void Relative_paths_and_uris_are_rejected()
    {
        Assert.Null(InstallPathRules.TryNormalize("GameLoop/GameLoop.exe"));
        Assert.Null(InstallPathRules.TryNormalize("file:///tmp/GameLoop"));
        Assert.Null(InstallPathRules.TryNormalize("   "));
        Assert.Null(InstallPathRules.TryNormalize(null));
    }

    [Fact]
    public void Filesystem_root_is_rejected()
    {
        var root = Path.GetPathRoot(Path.GetTempPath());
        Assert.False(string.IsNullOrEmpty(root));
        Assert.Null(InstallPathRules.TryNormalize(root));
    }

    [Fact]
    public void Icon_index_and_quotes_normalize_to_the_file()
    {
        var file = Path.Combine(Path.GetTempPath(), "glopt-icon-" + Guid.NewGuid().ToString("N"), "GameLoop.exe");
        var normalized = InstallPathRules.TryNormalize("\"" + file + ",0\"");

        Assert.Equal(Path.GetFullPath(file), normalized);
    }

    [Fact]
    public void Parent_escape_is_not_under_the_install()
    {
        var id = Guid.NewGuid().ToString("N");
        var parent = Path.Combine(Path.GetTempPath(), "glopt-parent-" + id);
        var root = Path.Combine(parent, "install");
        var outside = Path.Combine(parent, "outside", "file.txt");
        Directory.CreateDirectory(root);
        var escaped = Path.Combine(root, "..", "outside", "file.txt");

        Assert.True(InstallPathRules.IsUnderRoot(Path.Combine(root, "GameLoop.exe"), root));
        Assert.False(InstallPathRules.IsUnderRoot(escaped, root));
        Assert.False(InstallPathRules.IsUnderRoot(outside, root));
        Assert.False(InstallPathRules.IsUnderRoot("relative.exe", root));
    }
}

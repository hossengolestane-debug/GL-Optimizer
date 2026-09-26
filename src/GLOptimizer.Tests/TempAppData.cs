using GLOptimizer.Core.Paths;

namespace GLOptimizer.Tests;

internal sealed class TempAppData : IDisposable
{
    public TempAppData()
    {
        LocalAppData = Directory.CreateTempSubdirectory("gloptimizer-tests-").FullName;
    }

    public string LocalAppData { get; }

    public string Root => AppPaths.GetRoot(LocalAppData);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(LocalAppData))
            {
                Directory.Delete(LocalAppData, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}

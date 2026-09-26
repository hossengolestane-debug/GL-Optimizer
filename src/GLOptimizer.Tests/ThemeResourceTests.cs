using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace GLOptimizer.Tests;

public class ThemeResourceTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly Regex ResourceReference = new(@"\{(?:StaticResource|DynamicResource)\s+([^{}\s]+)\}", RegexOptions.Compiled);

    [Fact]
    public void Every_resource_reference_resolves_where_wpf_will_look()
    {
        var root = RepoRoot();
        var appDir = Path.Combine(root, "src", "GLOptimizer.App");
        var files = Directory.GetFiles(appDir, "*.xaml", SearchOption.AllDirectories);
        var byPath = files.ToDictionary(path => Path.GetFullPath(path), path => XDocument.Load(path, LoadOptions.SetLineInfo));
        var problems = new List<string>();

        foreach (var (path, document) in byPath)
        {
            var rootName = document.Root?.Name.LocalName;
            if (rootName == "ResourceDictionary")
            {
                var visible = VisibleKeys(path, byPath, new HashSet<string>(StringComparer.Ordinal));
                foreach (var reference in References(document))
                {
                    if (reference.Kind == "StaticResource" && !visible.Contains(reference.Key))
                    {
                        problems.Add(Relative(root, path) + ":" + reference.Line + " StaticResource '" + reference.Key + "' is not defined in this dictionary or a dictionary it merges.");
                    }
                }
            }
        }

        var application = Path.Combine(appDir, "App.xaml");
        var available = VisibleKeys(application, byPath, new HashSet<string>(StringComparer.Ordinal));
        foreach (var (path, document) in byPath)
        {
            var local = VisibleKeys(path, byPath, new HashSet<string>(StringComparer.Ordinal));
            foreach (var reference in References(document))
            {
                if (!available.Contains(reference.Key) && !local.Contains(reference.Key))
                {
                    problems.Add(Relative(root, path) + ":" + reference.Line + " " + reference.Kind + " '" + reference.Key + "' is not defined.");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Brush_converters_do_not_return_unset_value()
    {
        var converter = File.ReadAllText(Path.Combine(RepoRoot(), "src", "GLOptimizer.App", "Converters", "VisibilityConverters.cs"));
        Assert.DoesNotContain("UnsetValue", converter, StringComparison.Ordinal);
        Assert.DoesNotContain("Brushes", converter, StringComparison.Ordinal);
    }

    private static HashSet<string> VisibleKeys(string path, IReadOnlyDictionary<string, XDocument> documents, HashSet<string> stack)
    {
        var full = Path.GetFullPath(path);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (!stack.Add(full))
        {
            return keys;
        }

        if (!documents.TryGetValue(full, out var document) || document.Root is null)
        {
            stack.Remove(full);
            return keys;
        }

        foreach (var key in document.Descendants().Select(element => element.Attribute(Xaml + "Key")?.Value).OfType<string>())
        {
            keys.Add(key);
        }

        foreach (var source in document.Descendants().Where(element => element.Name.LocalName == "ResourceDictionary").Select(element => element.Attribute("Source")?.Value).OfType<string>())
        {
            var merged = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(full)!, source.Replace('/', Path.DirectorySeparatorChar)));
            keys.UnionWith(VisibleKeys(merged, documents, stack));
        }

        stack.Remove(full);
        return keys;
    }

    private static IEnumerable<ResourceRef> References(XDocument document)
    {
        foreach (var element in document.Descendants())
        {
            var line = element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
            foreach (var attribute in element.Attributes())
            {
                foreach (Match match in ResourceReference.Matches(attribute.Value))
                {
                    yield return new ResourceRef(match.Groups[1].Value, match.Value.Contains("DynamicResource", StringComparison.Ordinal) ? "DynamicResource" : "StaticResource", line);
                }
            }
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GLOptimizer.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find GLOptimizer.sln from " + AppContext.BaseDirectory);
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private sealed record ResourceRef(string Key, string Kind, int Line);
}

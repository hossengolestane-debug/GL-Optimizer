using System.Globalization;
using System.Text.Json;
using System.Xml;

namespace GLOptimizer.Core.Configuration;

public readonly record struct ConfigPair(string Key, string Value);

/// <summary>
/// Maps a small set of published GameLoop keys. Anything else is ignored.
/// A value outside the accepted range, or two different values for one setting, stays unset.
/// </summary>
public static class GameLoopSettingsParser
{
    private static readonly string[] RendererWords = ["OpenGL+", "OpenGL", "DirectX+", "DirectX", "Auto"];
    private static readonly string[] AntiAliasWords = ["Off", "Balanced", "Ultra"];

    public static bool TryReadPairs(string? text, string? extension, out IReadOnlyList<ConfigPair> pairs)
    {
        pairs = [];
        if (text is null || string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        var format = extension.Trim().TrimStart('.').ToLowerInvariant();
        if (format is not ("ini" or "cfg" or "txt" or "json" or "xml"))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return format is "ini" or "cfg" or "txt";
        }

        try
        {
            IReadOnlyList<ConfigPair> read = format switch
            {
                "ini" or "cfg" or "txt" => ReadIni(text),
                "json" => ReadJson(text),
                "xml" => ReadXml(text),
                _ => []
            };
            if (read.Count > 200)
            {
                return false;
            }

            pairs = read;
            return format is "ini" or "cfg" or "txt" or "json" or "xml";
        }
        catch (Exception ex) when (ex is JsonException or XmlException or InvalidOperationException)
        {
            return false;
        }
    }

    public static GameLoopSettings Parse(IReadOnlyList<ConfigPair> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var conflicted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in pairs)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || conflicted.Contains(pair.Key))
            {
                continue;
            }

            var value = pair.Value?.Trim() ?? string.Empty;
            if (value.Length == 0 || value.Length > 32 || LooksSensitive(value))
            {
                continue;
            }

            if (map.TryGetValue(pair.Key, out var existing)
                && !string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
            {
                conflicted.Add(pair.Key);
                map.Remove(pair.Key);
                continue;
            }

            map[pair.Key] = value;
        }

        var builder = new SettingsBuilder();
        builder.Set("Renderer", FirstWord(map, RendererWords, "Renderer", "RendererMode", "ScreenRenderingMode"));
        builder.Set("Resolution", Resolution(map));
        builder.Set("Dpi", IntegerText(map, 80, 960, "VMDPI", "DPI"));
        builder.Set("MemoryAllocation", Memory(map));
        builder.Set("CpuAllocation", IntegerText(map, 1, 64, "VMCpuCount", "CpuAllocation"));
        builder.Set("VSync", VSync(map));
        builder.Set("AntiAliasing", AntiAliasing(map));
        builder.Set("FpsTarget", Fps(map));
        return builder.Build();
    }

    public static GameLoopSettings Merge(IEnumerable<GameLoopSettings> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var builder = new SettingsBuilder();
        foreach (var source in sources)
        {
            builder.Set("Renderer", source.Renderer);
            builder.Set("Resolution", source.Resolution);
            builder.Set("Dpi", source.Dpi);
            builder.Set("MemoryAllocation", source.MemoryAllocation);
            builder.Set("CpuAllocation", source.CpuAllocation);
            builder.Set("VSync", source.VSync);
            builder.Set("AntiAliasing", source.AntiAliasing);
            builder.Set("FpsTarget", source.FpsTarget);
        }

        return builder.Build();
    }

    public static bool IsFpsKey(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            return false;
        }

        if (name.Equals("FPSLevel", StringComparison.OrdinalIgnoreCase)
            || name.Equals("FpsTarget", StringComparison.OrdinalIgnoreCase)
            || name.Equals("FPS", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!name.EndsWith("_FPSLevel", StringComparison.OrdinalIgnoreCase) || !name.Contains('.'))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_')
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsUnmappedRendererKey(string? name) =>
        name is not null && (
            name.Equals("ForceDirectX", StringComparison.OrdinalIgnoreCase)
            || name.Equals("EnableGLESv3", StringComparison.OrdinalIgnoreCase)
            || name.Equals("RendererEngine", StringComparison.OrdinalIgnoreCase));

    private static List<ConfigPair> ReadIni(string text)
    {
        var pairs = new List<ConfigPair>();
        foreach (var raw in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is ';' or '#' or '[')
            {
                continue;
            }

            var split = line.IndexOf('=');
            if (split <= 0)
            {
                continue;
            }

            var key = line[..split].Trim();
            var value = line[(split + 1)..].Trim().Trim('"');
            if (key.Length > 0 && key.Length <= 100)
            {
                pairs.Add(new ConfigPair(key, value));
            }
        }

        return pairs;
    }

    private static List<ConfigPair> ReadJson(string text)
    {
        var pairs = new List<ConfigPair>();
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 8 });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The config root was not an object.");
        }

        Walk(document.RootElement, pairs, 0);
        return pairs;
    }

    private static void Walk(JsonElement element, List<ConfigPair> pairs, int depth)
    {
        if (depth > 2 || element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Length is 0 or > 100)
            {
                continue;
            }

            switch (property.Value.ValueKind)
            {
                case JsonValueKind.String:
                    pairs.Add(new ConfigPair(property.Name, property.Value.GetString() ?? string.Empty));
                    break;
                case JsonValueKind.Number:
                    if (property.Value.TryGetInt64(out var number))
                    {
                        pairs.Add(new ConfigPair(property.Name, number.ToString(CultureInfo.InvariantCulture)));
                    }

                    break;
                case JsonValueKind.True:
                    pairs.Add(new ConfigPair(property.Name, "true"));
                    break;
                case JsonValueKind.False:
                    pairs.Add(new ConfigPair(property.Name, "false"));
                    break;
                case JsonValueKind.Object when depth < 2:
                    Walk(property.Value, pairs, depth + 1);
                    break;
            }
        }
    }

    private static List<ConfigPair> ReadXml(string text)
    {
        var pairs = new List<ConfigPair>();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            CloseInput = true,
            MaxCharactersInDocument = GameLoopConfigCatalog.MaxTextBytes
        };
        using var reader = XmlReader.Create(new StringReader(text), settings);
        var parents = new Stack<string>();
        var nodes = 0;
        while (reader.Read())
        {
            nodes++;
            if (nodes > 400)
            {
                throw new XmlException("The config has too many nodes.");
            }

            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    if (reader.LocalName.Length is > 0 and <= 100 && reader.HasAttributes)
                    {
                        while (reader.MoveToNextAttribute())
                        {
                            if (reader.LocalName.Length is > 0 and <= 100)
                            {
                                pairs.Add(new ConfigPair(reader.LocalName, reader.Value));
                            }
                        }

                        reader.MoveToElement();
                    }

                    if (!reader.IsEmptyElement && reader.LocalName.Length > 0)
                    {
                        parents.Push(reader.LocalName);
                    }

                    break;
                case XmlNodeType.Text when parents.Count > 0 && parents.Peek().Length <= 100:
                    pairs.Add(new ConfigPair(parents.Peek(), reader.Value.Trim()));
                    break;
                case XmlNodeType.EndElement when parents.Count > 0:
                    parents.Pop();
                    break;
            }
        }

        return pairs;
    }

    private static string? Resolution(Dictionary<string, string> map)
    {
        if (map.ContainsKey("VMResWidth") || map.ContainsKey("VMResHeight"))
        {
            var paired = PairedResolution(map);
            if (paired is null || !map.TryGetValue("Resolution", out _))
            {
                return paired;
            }

            var single = SingleResolution(map, "Resolution");
            return string.Equals(paired, single, StringComparison.Ordinal) ? paired : null;
        }

        return SingleResolution(map, "Resolution");
    }

    private static string? PairedResolution(Dictionary<string, string> map)
    {
        if (!TryInteger(map, "VMResWidth", 320, 8192, out var width)
            || !TryInteger(map, "VMResHeight", 320, 8192, out var height))
        {
            return null;
        }

        return width.ToString(CultureInfo.InvariantCulture) + "×" + height.ToString(CultureInfo.InvariantCulture);
    }

    private static string? SingleResolution(Dictionary<string, string> map, string key)
    {
        if (!map.TryGetValue(key, out var raw))
        {
            return null;
        }

        var parts = raw.Split(['x', 'X', '×', '*'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var height)
            || width is < 320 or > 8192
            || height is < 320 or > 8192)
        {
            return null;
        }

        return width.ToString(CultureInfo.InvariantCulture) + "×" + height.ToString(CultureInfo.InvariantCulture);
    }

    private static string? Memory(Dictionary<string, string> map)
    {
        var number = IntegerText(map, 128, 131072, "VMMemorySizeInMB", "MemoryAllocation");
        return number is null ? null : number + " MB";
    }

    private static string? IntegerText(Dictionary<string, string> map, int min, int max, params string[] keys)
    {
        string? chosen = null;
        foreach (var key in keys)
        {
            if (!TryInteger(map, key, min, max, out var number))
            {
                if (map.ContainsKey(key))
                {
                    return null;
                }

                continue;
            }

            var text = number.ToString(CultureInfo.InvariantCulture);
            if (chosen is not null && !string.Equals(chosen, text, StringComparison.Ordinal))
            {
                return null;
            }

            chosen = text;
        }

        return chosen;
    }

    private static bool TryInteger(Dictionary<string, string> map, string key, int min, int max, out int number)
    {
        number = 0;
        if (!map.TryGetValue(key, out var raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (trimmed.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^2].Trim();
        }
        else if (trimmed.EndsWith("DPI", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^3].Trim();
        }

        if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out number))
        {
            return false;
        }

        return number >= min && number <= max;
    }

    private static string? VSync(Dictionary<string, string> map)
    {
        string? chosen = null;
        foreach (var key in new[] { "VSyncEnabled", "VSync" })
        {
            if (!map.TryGetValue(key, out var raw))
            {
                continue;
            }

            var word = raw.Trim().ToLowerInvariant() switch
            {
                "1" or "true" or "yes" or "on" => "On",
                "0" or "false" or "no" or "off" => "Off",
                _ => null
            };
            if (word is null || (chosen is not null && !string.Equals(chosen, word, StringComparison.Ordinal)))
            {
                return null;
            }

            chosen = word;
        }

        return chosen;
    }

    private static string? AntiAliasing(Dictionary<string, string> map)
    {
        string? chosen = null;
        foreach (var key in new[] { "FxaaQuality", "AntiAliasing" })
        {
            if (!map.TryGetValue(key, out var raw))
            {
                continue;
            }

            string? word = null;
            foreach (var candidate in AntiAliasWords)
            {
                if (raw.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    word = candidate;
                    break;
                }
            }

            if (word is null && TryInteger(map, key, 0, 8, out var number))
            {
                word = number.ToString(CultureInfo.InvariantCulture);
            }

            if (word is null || (chosen is not null && !string.Equals(chosen, word, StringComparison.Ordinal)))
            {
                return null;
            }

            chosen = word;
        }

        return chosen;
    }

    private static string? Fps(Dictionary<string, string> map)
    {
        string? chosen = null;
        foreach (var pair in map)
        {
            if (!IsFpsKey(pair.Key))
            {
                continue;
            }

            if (!int.TryParse(pair.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                || number is < 15 or > 240)
            {
                return null;
            }

            var text = number.ToString(CultureInfo.InvariantCulture);
            if (chosen is not null && !string.Equals(chosen, text, StringComparison.Ordinal))
            {
                return null;
            }

            chosen = text;
        }

        return chosen;
    }

    private static string? FirstWord(Dictionary<string, string> map, string[] allowed, params string[] keys)
    {
        string? chosen = null;
        foreach (var key in keys)
        {
            if (!map.TryGetValue(key, out var raw))
            {
                continue;
            }

            string? word = null;
            foreach (var candidate in allowed)
            {
                if (raw.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    word = candidate;
                    break;
                }
            }

            if (word is null || (chosen is not null && !string.Equals(chosen, word, StringComparison.Ordinal)))
            {
                return null;
            }

            chosen = word;
        }

        return chosen;
    }

    private static bool LooksSensitive(string value)
    {
        if (value.Contains('@', StringComparison.Ordinal)
            || value.Contains('/')
            || value.Contains('\\'))
        {
            return true;
        }

        var spaces = 0;
        foreach (var character in value)
        {
            if (character == ' ')
            {
                spaces++;
            }
        }

        if (spaces > 1)
        {
            return true;
        }

        var hex = 0;
        foreach (var character in value)
        {
            if (Uri.IsHexDigit(character))
            {
                hex++;
            }
        }

        return value.Length >= 24 && hex == value.Length;
    }

    private sealed class SettingsBuilder
    {
        private readonly Dictionary<string, string?> _values = [];
        private readonly HashSet<string> _conflicts = [];

        public void Set(string field, string? value)
        {
            if (value is null || _conflicts.Contains(field))
            {
                return;
            }

            if (_values.TryGetValue(field, out var existing))
            {
                if (!string.Equals(existing, value, StringComparison.Ordinal))
                {
                    _conflicts.Add(field);
                    _values[field] = null;
                }

                return;
            }

            _values[field] = value;
        }

        public GameLoopSettings Build() => new()
        {
            Renderer = Read("Renderer"),
            Resolution = Read("Resolution"),
            Dpi = Read("Dpi"),
            MemoryAllocation = Read("MemoryAllocation"),
            CpuAllocation = Read("CpuAllocation"),
            VSync = Read("VSync"),
            AntiAliasing = Read("AntiAliasing"),
            FpsTarget = Read("FpsTarget")
        };

        private string? Read(string field) =>
            _conflicts.Contains(field) ? null : _values.GetValueOrDefault(field);
    }
}

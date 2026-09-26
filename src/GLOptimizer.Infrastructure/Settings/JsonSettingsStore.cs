using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Results;
using GLOptimizer.Core.Settings;

namespace GLOptimizer.Infrastructure.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _gate = new();
    private readonly string _path;
    private AppSettings _current = new();

    public JsonSettingsStore(AppDataLocations locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        _path = locations.SettingsFile;
    }

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current.Copy();
            }
        }
    }

    public OperationResult Load()
    {
        lock (_gate)
        {
            return LoadNoLock();
        }
    }

    public OperationResult Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            return SaveNoLock(settings);
        }
    }

    private OperationResult LoadNoLock()
    {
        try
        {
            if (!File.Exists(_path))
            {
                _current = new AppSettings();
                return SaveNoLock(_current);
            }

            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            if (loaded is null)
            {
                _current = new AppSettings();
                return OperationResult.Failure("Settings file was empty. Defaults are in use.");
            }

            AppSettingsRules.Normalize(loaded);
            _current = loaded;
            return OperationResult.Success();
        }
        catch (JsonException)
        {
            _current = new AppSettings();
            return OperationResult.Failure("Settings file was unreadable. Defaults are in use.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _current = new AppSettings();
            return OperationResult.Failure(UserFacingError.From(ex));
        }
    }

    private OperationResult SaveNoLock(AppSettings settings)
    {
        try
        {
            var copy = settings.Copy();
            AppSettingsRules.Normalize(copy);
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(copy, SerializerOptions);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temp, _path, overwrite: true);
            _current = copy;
            return OperationResult.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return OperationResult.Failure(UserFacingError.From(ex));
        }
    }
}

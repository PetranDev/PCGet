using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PCGet.Services;

/// <summary>
/// File-backed replacement for ApplicationData.LocalSettings when PCGet runs unpackaged.
/// Stored at %LOCALAPPDATA%\PCGet\settings.json. Shared by every AppSettingsService instance.
/// </summary>
internal sealed class JsonSettingsStore
{
    private static readonly Lazy<JsonSettingsStore> InstanceLazy = new(() => new JsonSettingsStore());

    private readonly object _sync = new();
    private readonly string _path;
    private readonly Dictionary<string, object> _values;

    public static JsonSettingsStore Instance => InstanceLazy.Value;

    private JsonSettingsStore()
    {
        _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCGet",
            "settings.json");

        _values = Load(_path);
    }

    public bool TryGetValue(string key, out object? value)
    {
        lock (_sync)
            return _values.TryGetValue(key, out value);
    }

    public void SetValue(string key, object value)
    {
        lock (_sync)
        {
            _values[key] = value;
            Save();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, _path, true);
        }
        catch
        {
            // Failing to persist a setting must never crash PCGet; the in-memory value still applies.
        }
    }

    private static Dictionary<string, object> Load(string path)
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);

        try
        {
            if (!File.Exists(path))
                return values;

            using var document = JsonDocument.Parse(File.ReadAllText(path));

            foreach (var property in document.RootElement.EnumerateObject())
            {
                object? value = property.Value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number when property.Value.TryGetInt32(out var intValue) => intValue,
                    _ => null
                };

                if (value is not null)
                    values[property.Name] = value;
            }
        }
        catch
        {
            // A corrupt settings file falls back to defaults.
        }

        return values;
    }
}

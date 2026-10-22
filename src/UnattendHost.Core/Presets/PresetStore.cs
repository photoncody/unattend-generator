using System.Text.Json;
using System.Text.RegularExpressions;

namespace UnattendHost.Core.Presets;

public sealed class PresetException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Reads presets (HostConfig JSON files) from a directory. The preset name is the file name
/// without ".json". Phase 1 is read-only; saving, seeding and secret placeholders come in phase 3.
/// </summary>
public sealed partial class PresetStore(string directory)
{
    public string Directory { get; } = directory;

    /// <summary>Lower-case letters, digits and hyphens, so names are safe in URLs and file paths.</summary>
    public static bool IsValidName(string name) => NamePattern().IsMatch(name);

    public IReadOnlyList<string> List()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }
        return [.. System.IO.Directory.EnumerateFiles(Directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(IsValidName)
            .Order(StringComparer.Ordinal)];
    }

    /// <exception cref="PresetException">The preset does not exist or is not valid JSON for HostConfig.</exception>
    public HostConfig Load(string name)
    {
        if (!IsValidName(name))
        {
            throw new PresetException($"'{name}' is not a valid preset name.");
        }

        string path = Path.Combine(Directory, name + ".json");
        if (!File.Exists(path))
        {
            throw new PresetException($"Preset '{name}' does not exist.");
        }

        try
        {
            return HostConfigJson.Deserialize(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new PresetException($"Preset '{name}' could not be read: {ex.Message}", ex);
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex NamePattern();
}

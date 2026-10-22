using Schneegans.Unattend;
using UnattendHost.Core;
using UnattendHost.Core.Presets;

namespace UnattendHost.Tests;

internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string Presets => Path.Combine(Root, "presets");

    public static string Baselines => Path.Combine(Root, "baselines");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "UnattendHost.slnx")))
            {
                return dir.FullName;
            }
        }
        throw new InvalidOperationException("Could not find the repository root (the folder with UnattendHost.slnx).");
    }
}

internal static class Shared
{
    // Creating the generator parses upstream's JSON resources, so share one instance.
    public static UnattendGenerator Generator { get; } = new();

    public static GenerationService Generation { get; } = new(Generator);

    public static PresetStore RepoPresets { get; } = new(RepoPaths.Presets);

    public static TheoryData<string> PresetNames => [.. RepoPresets.List()];
}

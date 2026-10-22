using UnattendHost.Core.Presets;

namespace UnattendHost.Tests;

public sealed class PresetStoreTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("unattendhost-tests-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Theory]
    [InlineData("win11-pro", true)]
    [InlineData("server2025-dc-desktop", true)]
    [InlineData("../etc/passwd", false)]
    [InlineData("a/b", false)]
    [InlineData("Win11", false)]
    [InlineData("-leading-hyphen", false)]
    [InlineData("", false)]
    public void IsValidName(string name, bool valid)
    {
        Assert.Equal(valid, PresetStore.IsValidName(name));
    }

    [Fact]
    public void Load_RejectsInvalidNames()
    {
        Assert.Throws<PresetException>(() => new PresetStore(directory).Load("../presets/win11-pro"));
    }

    [Fact]
    public void Load_ReportsMissingPreset()
    {
        var ex = Assert.Throws<PresetException>(() => new PresetStore(directory).Load("missing"));

        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public void Load_ReportsMalformedPreset()
    {
        File.WriteAllText(Path.Combine(directory, "broken.json"), """{ "targetOs": "Windows12" }""");

        var ex = Assert.Throws<PresetException>(() => new PresetStore(directory).Load("broken"));

        Assert.Contains("broken", ex.Message);
    }

    [Fact]
    public void List_ReturnsValidNamesInOrder_AndIgnoresOtherFiles()
    {
        File.WriteAllText(Path.Combine(directory, "b.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "a.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "Not Valid.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "notes.txt"), "");

        Assert.Equal(["a", "b"], new PresetStore(directory).List());
    }

    [Fact]
    public void List_IsEmptyWhenDirectoryIsMissing()
    {
        Assert.Empty(new PresetStore(Path.Combine(directory, "nope")).List());
    }
}

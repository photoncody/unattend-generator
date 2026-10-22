using UnattendHost.Core;
using UnattendHost.Web;

namespace UnattendHost.Tests;

public class GeneratorFormTests
{
    private static byte[] Generate(HostConfig config) => Shared.Generation.Generate(config).Xml;

    [Theory]
    [MemberData(nameof(Shared.PresetNames), MemberType = typeof(Shared))]
    public void Preset_SurvivesTheForm(string name)
    {
        // Loading a preset into the form and submitting it unchanged must not change the output.
        var config = Shared.RepoPresets.Load(name);

        var roundTripped = GeneratorForm.FromHostConfig(config).ToHostConfig();

        Assert.Equal(Generate(config), Generate(roundTripped));
    }

    [Fact]
    public void NewForm_ProducesTheDefaultConfiguration()
    {
        Assert.Equal(Generate(new HostConfig()), Generate(new GeneratorForm().ToHostConfig()));
    }

    [Fact]
    public void FieldsOfUnselectedModes_AreDropped()
    {
        var form = new GeneratorForm
        {
            ComputerNameMode = ComputerNameMode.Random,
            ComputerName = "LEFTOVER",
            SetupMode = SetupMode.Standard,
            EditionMode = EditionMode.Interactive,
            ProductKey = "LEFTO-VERXX-XXXXX-XXXXX-XXXXX",
            ImageName = "leftover",
            AccountMode = AccountMode.InteractiveLocal,
        };
        form.Users[0].Name = "leftover";

        var config = form.ToHostConfig();

        Assert.Null(config.ComputerName.Name);
        Assert.Null(config.Setup.Scripted);
        Assert.Null(config.Setup.Standard!.ProductKey);
        Assert.Empty(config.Accounts.Users);
        Assert.Empty(Shared.Generation.Validate(config));
    }

    [Fact]
    public void EmptyRows_AreDropped()
    {
        var form = new GeneratorForm { AccountMode = AccountMode.Unattended };
        form.Users[1].Name = "labadmin";

        var config = form.ToHostConfig();

        Assert.Equal(["labadmin"], config.Accounts.Users.Select(u => u.Name));
        Assert.Empty(config.Scripts.Items);
    }

    [Fact]
    public void Form_AlwaysHasRoomForAnotherRow()
    {
        var config = Shared.RepoPresets.Load("win11-pro-scripted");

        var form = GeneratorForm.FromHostConfig(config);

        Assert.True(form.Users.Count > config.Accounts.Users.Count);
        Assert.True(form.Scripts.Count > config.Scripts.Items.Count);
        Assert.Equal(GeneratorForm.LocaleSlots, form.Locales.Count);
    }
}

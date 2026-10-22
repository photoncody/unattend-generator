using UnattendHost.Core;

namespace UnattendHost.Tests;

public class ValidatorTests
{
    private static IReadOnlyList<string> Validate(HostConfig config) => Shared.Generation.Validate(config);

    private static readonly LanguageOptions EnglishUs = new()
    {
        Mode = LanguageMode.Unattended,
        ImageLanguage = "en-US",
        Locales = [new LocaleKeyboard { Locale = "en-US", Keyboard = "00000409" }],
        GeoLocation = "244",
    };

    [Fact]
    public void DefaultConfig_IsValid()
    {
        Assert.Empty(Validate(new HostConfig()));
    }

    [Theory]
    [MemberData(nameof(Shared.PresetNames), MemberType = typeof(Shared))]
    public void RepoPresets_AreValid(string name)
    {
        Assert.Empty(Validate(Shared.RepoPresets.Load(name)));
    }

    [Theory]
    [InlineData(TargetOs.Server2022)]
    [InlineData(TargetOs.Server2025)]
    public void ServerTarget_IsRejectedInPhase1(TargetOs target)
    {
        var errors = Validate(new HostConfig { TargetOs = target });

        Assert.Contains(errors, e => e.Contains("targetOs"));
    }

    [Fact]
    public void UnsupportedSchemaVersion_IsRejected()
    {
        Assert.Contains(Validate(new HostConfig { SchemaVersion = 2 }), e => e.Contains("schemaVersion"));
    }

    [Fact]
    public void UnattendedLanguage_WithKnownIds_IsValid()
    {
        Assert.Empty(Validate(new HostConfig { Language = EnglishUs }));
    }

    [Fact]
    public void UnknownIds_AreReportedPerField()
    {
        var config = new HostConfig
        {
            Language = EnglishUs with
            {
                ImageLanguage = "xx-XX",
                Locales = [new LocaleKeyboard { Locale = "en-US", Keyboard = "nope" }],
            },
            Bloatware = ["RemoveNothing"],
        };

        var errors = Validate(config);

        Assert.Contains(errors, e => e.StartsWith("language.imageLanguage 'xx-XX'"));
        Assert.Contains(errors, e => e.StartsWith("language.locales[0].keyboard 'nope'"));
        Assert.Contains(errors, e => e.StartsWith("bloatware[0] 'RemoveNothing'"));
    }

    [Fact]
    public void LookupsAreCaseInsensitive_LikeUpstream()
    {
        Assert.Empty(Validate(new HostConfig { Bloatware = ["removeteams"] }));
    }

    [Fact]
    public void TooManyLocales_IsRejected()
    {
        var pair = new LocaleKeyboard { Locale = "en-US", Keyboard = "00000409" };
        var config = new HostConfig { Language = EnglishUs with { Locales = [pair, pair, pair, pair] } };

        Assert.Contains(Validate(config), e => e.Contains("between 1 and 3"));
    }

    [Fact]
    public void LanguageFields_AreRejectedWhenLanguageIsInteractive()
    {
        var config = new HostConfig { Language = EnglishUs with { Mode = LanguageMode.Interactive } };

        Assert.Equal(3, Validate(config).Count);
    }

    [Fact]
    public void InactiveSetupSection_IsRejected()
    {
        var config = new HostConfig
        {
            Setup = new SetupOptions { Mode = SetupMode.Standard, Scripted = new ScriptedSetupOptions() },
        };

        Assert.Contains(Validate(config), e => e.Contains("setup.scripted"));
    }

    [Fact]
    public void ScriptedImageSelection_RequiresMatchingField()
    {
        var config = new HostConfig
        {
            Setup = new SetupOptions
            {
                Mode = SetupMode.Scripted,
                Scripted = new ScriptedSetupOptions { ImageMode = ImageSelectionMode.Index, ImageName = "Windows 11 Pro" },
            },
        };

        var errors = Validate(config);

        Assert.Contains(errors, e => e.StartsWith("setup.scripted.imageIndex"));
        Assert.Contains(errors, e => e.StartsWith("setup.scripted.imageName is only used"));
    }

    [Fact]
    public void CustomComputerName_RequiresName_AndOtherModesRejectIt()
    {
        Assert.Contains(Validate(new HostConfig { ComputerName = new() { Mode = ComputerNameMode.Custom } }), e => e.StartsWith("computerName.name is required"));
        Assert.Contains(Validate(new HostConfig { ComputerName = new() { Mode = ComputerNameMode.Random, Name = "PC1" } }), e => e.StartsWith("computerName.name is only used"));
    }

    [Fact]
    public void BuiltinAdministratorAutoLogon_RequiresPassword()
    {
        var config = new HostConfig
        {
            Accounts = new AccountOptions { Mode = AccountMode.Unattended, AutoLogon = AutoLogonMode.BuiltinAdministrator },
        };

        Assert.Contains(Validate(config), e => e.StartsWith("accounts.builtinAdministratorPassword is required"));
    }

    [Fact]
    public void UsersAreRejectedWhenAccountsAreInteractive()
    {
        var config = new HostConfig
        {
            Accounts = new AccountOptions { Mode = AccountMode.InteractiveLocal, Users = [new UserAccount { Name = "x" }] },
        };

        Assert.Contains(Validate(config), e => e.StartsWith("accounts.users"));
    }

    [Fact]
    public void DuplicateBloatware_IsRejected()
    {
        Assert.Contains(Validate(new HostConfig { Bloatware = ["RemoveTeams", "removeteams"] }), e => e.Contains("more than once"));
    }

    [Fact]
    public void EmptyScript_IsRejected()
    {
        var config = new HostConfig
        {
            Scripts = new ScriptOptions { Items = [new CustomScript { Phase = ScriptPhase.System, Type = ScriptType.Cmd, Content = "  " }] },
        };

        Assert.Contains(Validate(config), e => e.StartsWith("scripts.items[0].content"));
    }

    [Fact]
    public void AllErrorsAreReportedAtOnce()
    {
        var config = new HostConfig
        {
            TargetOs = TargetOs.Server2025,
            ComputerName = new() { Mode = ComputerNameMode.Custom },
            Bloatware = ["RemoveNothing"],
        };

        Assert.Equal(3, Validate(config).Count);
    }
}

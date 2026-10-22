using UnattendHost.Core;

namespace UnattendHost.Web;

/// <summary>
/// A flat, mutable copy of HostConfig that HTML form fields bind to.
///
/// The form always posts every field, including those of modes that are not selected.
/// ToHostConfig only copies the fields that belong to the selected modes, and drops empty
/// list rows, so hidden leftovers never end up in the configuration.
/// </summary>
public sealed class GeneratorForm
{
    public const int LocaleSlots = 3;
    private const int MinUserSlots = 3;
    private const int MinScriptSlots = 3;

    public GeneratorForm()
    {
        Load(new HostConfig());
    }

    public static GeneratorForm FromHostConfig(HostConfig config)
    {
        var form = new GeneratorForm();
        form.Load(config);
        return form;
    }

    public string? Description { get; set; }
    public TargetOs TargetOs { get; set; }

    public LanguageMode LanguageMode { get; set; }
    public string? ImageLanguage { get; set; }
    public List<LocaleSlot> Locales { get; set; } = [];
    public string? GeoLocation { get; set; }

    public SetupMode SetupMode { get; set; }
    public string? ActivationKey { get; set; }

    // Standard setup
    public EditionMode EditionMode { get; set; }
    public string? Edition { get; set; }
    public string? ProductKey { get; set; }
    public bool BypassRequirementsCheck { get; set; }

    // Scripted setup
    public ImageSelectionMode ImageMode { get; set; }
    public string? ImageEdition { get; set; }
    public int? ImageIndex { get; set; }
    public string? ImageName { get; set; }
    public PartitionMode PartitionMode { get; set; }
    public PartitionLayout PartitionLayout { get; set; }
    public bool RecoveryPartition { get; set; }
    public int SystemPartitionSizeMiB { get; set; }
    public int RecoveryPartitionSizeMiB { get; set; }
    public string? DiskpartScript { get; set; }
    public TargetDiskMode TargetDiskMode { get; set; }
    public int? TargetDiskIndex { get; set; }
    public int? TargetDiskMinSizeGiB { get; set; }
    public int? TargetDiskMaxSizeGiB { get; set; }
    public bool TargetDiskRequireNoPartitions { get; set; }
    public bool DisableDefender { get; set; }
    public bool Disable8Dot3Names { get; set; }
    public bool PauseBeforeFormatting { get; set; }
    public bool PauseBeforeReboot { get; set; }
    public bool CompactOs { get; set; }
    public bool SkipIntegrityCheck { get; set; }

    public ComputerNameMode ComputerNameMode { get; set; }
    public string? ComputerName { get; set; }

    public AccountMode AccountMode { get; set; }
    public List<UserSlot> Users { get; set; } = [];
    public AutoLogonMode AutoLogon { get; set; }
    public string? BuiltinAdministratorPassword { get; set; }
    public bool ObscurePasswords { get; set; }
    public bool KeepSensitiveFiles { get; set; }

    public bool BypassNetworkCheck { get; set; }
    public List<string> Bloatware { get; set; } = [];

    public List<ScriptSlot> Scripts { get; set; } = [];
    public bool RestartExplorer { get; set; }

    private void Load(HostConfig config)
    {
        Description = config.Description;
        TargetOs = config.TargetOs;

        LanguageMode = config.Language.Mode;
        ImageLanguage = config.Language.ImageLanguage;
        Locales = Pad(config.Language.Locales.Select(l => new LocaleSlot { Locale = l.Locale, Keyboard = l.Keyboard }), LocaleSlots, LocaleSlots);
        GeoLocation = config.Language.GeoLocation;

        SetupMode = config.Setup.Mode;
        ActivationKey = config.Setup.ActivationKey;

        var standard = config.Setup.Standard ?? new();
        EditionMode = standard.EditionMode;
        Edition = standard.Edition;
        ProductKey = standard.ProductKey;
        BypassRequirementsCheck = standard.BypassRequirementsCheck;

        var scripted = config.Setup.Scripted ?? new();
        ImageMode = scripted.ImageMode;
        ImageEdition = scripted.Edition;
        ImageIndex = scripted.ImageIndex;
        ImageName = scripted.ImageName;
        PartitionMode = scripted.Partitioning.Mode;
        PartitionLayout = scripted.Partitioning.Layout;
        RecoveryPartition = scripted.Partitioning.RecoveryPartition;
        SystemPartitionSizeMiB = scripted.Partitioning.SystemPartitionSizeMiB;
        RecoveryPartitionSizeMiB = scripted.Partitioning.RecoveryPartitionSizeMiB;
        DiskpartScript = scripted.Partitioning.DiskpartScript;
        TargetDiskMode = scripted.Partitioning.TargetDisk.Mode;
        TargetDiskIndex = scripted.Partitioning.TargetDisk.Index;
        TargetDiskMinSizeGiB = scripted.Partitioning.TargetDisk.MinSizeGiB;
        TargetDiskMaxSizeGiB = scripted.Partitioning.TargetDisk.MaxSizeGiB;
        TargetDiskRequireNoPartitions = scripted.Partitioning.TargetDisk.RequireNoPartitions;
        DisableDefender = scripted.DisableDefender;
        Disable8Dot3Names = scripted.Disable8Dot3Names;
        PauseBeforeFormatting = scripted.PauseBeforeFormatting;
        PauseBeforeReboot = scripted.PauseBeforeReboot;
        CompactOs = scripted.CompactOs;
        SkipIntegrityCheck = scripted.SkipIntegrityCheck;

        ComputerNameMode = config.ComputerName.Mode;
        ComputerName = config.ComputerName.Name;

        AccountMode = config.Accounts.Mode;
        Users = Pad(config.Accounts.Users.Select(u => new UserSlot { Name = u.Name, DisplayName = u.DisplayName, Password = u.Password, Group = u.Group }), MinUserSlots);
        AutoLogon = config.Accounts.AutoLogon;
        BuiltinAdministratorPassword = config.Accounts.BuiltinAdministratorPassword;
        ObscurePasswords = config.Accounts.ObscurePasswords;
        KeepSensitiveFiles = config.KeepSensitiveFiles;

        BypassNetworkCheck = config.BypassNetworkCheck;
        Bloatware = [.. config.Bloatware];

        Scripts = Pad(config.Scripts.Items.Select(s => new ScriptSlot { Phase = s.Phase, Type = s.Type, Content = s.Content }), MinScriptSlots);
        RestartExplorer = config.Scripts.RestartExplorer;
    }

    public HostConfig ToHostConfig()
    {
        return new HostConfig
        {
            Description = Description,
            TargetOs = TargetOs,
            Language = LanguageMode == LanguageMode.Unattended
                ? new LanguageOptions
                {
                    Mode = LanguageMode,
                    ImageLanguage = ImageLanguage,
                    Locales = [.. Locales
                        .Where(l => l.Locale != null || l.Keyboard != null)
                        .Select(l => new LocaleKeyboard { Locale = l.Locale ?? "", Keyboard = l.Keyboard ?? "" })],
                    GeoLocation = GeoLocation,
                }
                : new LanguageOptions { Mode = LanguageMode },
            Setup = new SetupOptions
            {
                Mode = SetupMode,
                Standard = SetupMode == SetupMode.Standard ? ToStandardSetup() : null,
                Scripted = SetupMode == SetupMode.Scripted ? ToScriptedSetup() : null,
                ActivationKey = ActivationKey,
            },
            ComputerName = new ComputerNameOptions
            {
                Mode = ComputerNameMode,
                Name = ComputerNameMode == ComputerNameMode.Custom ? ComputerName : null,
            },
            Accounts = AccountMode == AccountMode.Unattended
                ? new AccountOptions
                {
                    Mode = AccountMode,
                    Users = [.. Users
                        .Where(u => u.Name != null || u.DisplayName != null || u.Password != null)
                        .Select(u => new UserAccount { Name = u.Name ?? "", DisplayName = u.DisplayName ?? "", Password = u.Password ?? "", Group = u.Group })],
                    AutoLogon = AutoLogon,
                    BuiltinAdministratorPassword = AutoLogon == AutoLogonMode.BuiltinAdministrator ? BuiltinAdministratorPassword ?? "" : null,
                    ObscurePasswords = ObscurePasswords,
                }
                : new AccountOptions { Mode = AccountMode },
            KeepSensitiveFiles = KeepSensitiveFiles,
            BypassNetworkCheck = BypassNetworkCheck,
            Bloatware = [.. Bloatware],
            Scripts = new ScriptOptions
            {
                Items = [.. Scripts
                    .Where(s => !string.IsNullOrWhiteSpace(s.Content))
                    .Select(s => new CustomScript { Phase = s.Phase, Type = s.Type, Content = s.Content! })],
                RestartExplorer = RestartExplorer,
            },
        };
    }

    private StandardSetupOptions ToStandardSetup()
    {
        return new StandardSetupOptions
        {
            EditionMode = EditionMode,
            Edition = EditionMode == EditionMode.Edition ? Edition : null,
            ProductKey = EditionMode == EditionMode.ProductKey ? ProductKey : null,
            BypassRequirementsCheck = BypassRequirementsCheck,
        };
    }

    private ScriptedSetupOptions ToScriptedSetup()
    {
        return new ScriptedSetupOptions
        {
            ImageMode = ImageMode,
            Edition = ImageMode == ImageSelectionMode.Edition ? ImageEdition : null,
            ImageIndex = ImageMode == ImageSelectionMode.Index ? ImageIndex : null,
            ImageName = ImageMode == ImageSelectionMode.Name ? ImageName : null,
            Partitioning = new PartitionOptions
            {
                Mode = PartitionMode,
                Layout = PartitionLayout,
                RecoveryPartition = RecoveryPartition,
                SystemPartitionSizeMiB = SystemPartitionSizeMiB,
                RecoveryPartitionSizeMiB = RecoveryPartitionSizeMiB,
                TargetDisk = new TargetDiskOptions
                {
                    Mode = TargetDiskMode,
                    Index = TargetDiskIndex,
                    MinSizeGiB = TargetDiskMinSizeGiB,
                    MaxSizeGiB = TargetDiskMaxSizeGiB,
                    RequireNoPartitions = TargetDiskRequireNoPartitions,
                },
                DiskpartScript = PartitionMode == PartitionMode.CustomScript ? DiskpartScript : null,
            },
            DisableDefender = DisableDefender,
            Disable8Dot3Names = Disable8Dot3Names,
            PauseBeforeFormatting = PauseBeforeFormatting,
            PauseBeforeReboot = PauseBeforeReboot,
            CompactOs = CompactOs,
            SkipIntegrityCheck = SkipIntegrityCheck,
        };
    }

    /// <summary>Adds empty rows so the form always has room for one more entry.</summary>
    private static List<T> Pad<T>(IEnumerable<T> items, int minCount, int? maxCount = null) where T : new()
    {
        var list = items.ToList();
        int target = Math.Max(minCount, list.Count + 1);
        if (maxCount is int max)
        {
            target = Math.Min(target, max);
        }
        while (list.Count < target)
        {
            list.Add(new T());
        }
        return list;
    }
}

public sealed class LocaleSlot
{
    public string? Locale { get; set; }
    public string? Keyboard { get; set; }
}

public sealed class UserSlot
{
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public string? Password { get; set; }
    public AccountGroup Group { get; set; } = AccountGroup.Administrators;
}

public sealed class ScriptSlot
{
    public ScriptPhase Phase { get; set; } = ScriptPhase.System;
    public ScriptType Type { get; set; } = ScriptType.Ps1;
    public string? Content { get; set; }
}

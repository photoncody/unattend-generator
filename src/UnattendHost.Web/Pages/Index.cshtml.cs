using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UnattendHost.Core;
using UnattendHost.Core.Presets;

namespace UnattendHost.Web.Pages;

/// <summary>What the result view shows after a successful generation.</summary>
public sealed record ResultView(string ConfigJson, string XmlText, IReadOnlyList<string> Warnings);

/// <summary>
/// The generator page. It shows either the form or, after a successful POST, the result.
/// The result view carries the configuration as JSON in hidden fields, so "download" and
/// "back to editor" regenerate from exactly the same input; nothing is stored on the server.
/// </summary>
public sealed class IndexModel(GenerationService generation, UpstreamCatalog catalog, PresetStore presets) : PageModel
{
    private const long MaxPresetFileBytes = 1_000_000;

    [BindProperty]
    public GeneratorForm Form { get; set; } = new();

    public UpstreamCatalog Catalog => catalog;

    public IReadOnlyList<string> PresetNames { get; private set; } = [];

    public string? LoadedPreset { get; private set; }

    public List<string> Errors { get; } = [];

    public ResultView? Result { get; private set; }

    public void OnGet(string? preset)
    {
        if (preset != null)
        {
            try
            {
                if (TryLoad(presets.Load(preset)))
                {
                    LoadedPreset = preset;
                }
            }
            catch (PresetException ex)
            {
                Errors.Add(ex.Message);
            }
        }
        PresetNames = presets.List();
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? presetFile)
    {
        PresetNames = presets.List();
        if (presetFile == null || presetFile.Length == 0)
        {
            Errors.Add("Choose a preset file to load.");
        }
        else if (presetFile.Length > MaxPresetFileBytes)
        {
            Errors.Add($"The preset file is larger than {MaxPresetFileBytes / 1000} kB.");
        }
        else
        {
            using var reader = new StreamReader(presetFile.OpenReadStream(), Encoding.UTF8);
            if (TryParse(await reader.ReadToEndAsync(), out var config))
            {
                TryLoad(config);
            }
        }
        return Page();
    }

    public IActionResult OnPostGenerate()
    {
        PresetNames = presets.List();
        if (!ModelState.IsValid)
        {
            Errors.AddRange(ModelState
                .Where(entry => entry.Value != null)
                .SelectMany(entry => entry.Value!.Errors.Select(error => $"{entry.Key}: {error.ErrorMessage}")));
            return Page();
        }

        var config = Form.ToHostConfig();
        try
        {
            var result = generation.Generate(config);
            Result = new ResultView(
                ConfigJson: HostConfigJson.Serialize(config),
                XmlText: Encoding.ASCII.GetString(result.Xml),
                Warnings: result.Warnings);
        }
        catch (InvalidHostConfigException ex)
        {
            Errors.AddRange(ex.Errors);
        }
        return Page();
    }

    public IActionResult OnPostEdit(string? configJson)
    {
        PresetNames = presets.List();
        if (TryParse(configJson, out var config))
        {
            Form = GeneratorForm.FromHostConfig(config);
        }
        return Page();
    }

    public IActionResult OnPostDownload(string? configJson)
    {
        PresetNames = presets.List();
        if (TryParse(configJson, out var config))
        {
            try
            {
                var result = generation.Generate(config);
                return File(result.Xml, "application/xml", "autounattend.xml");
            }
            catch (InvalidHostConfigException ex)
            {
                Errors.AddRange(ex.Errors);
            }
        }
        return Page();
    }

    public IActionResult OnPostDownloadPreset(string? configJson)
    {
        PresetNames = presets.List();
        if (TryParse(configJson, out var config))
        {
            return File(Encoding.UTF8.GetBytes(HostConfigJson.Serialize(config)), "application/json", "preset.json");
        }
        return Page();
    }

    private bool TryParse(string? json, out HostConfig config)
    {
        try
        {
            config = HostConfigJson.Deserialize(json ?? "");
            return true;
        }
        catch (JsonException ex)
        {
            Errors.Add($"The preset could not be read: {ex.Message}");
            config = new HostConfig();
            return false;
        }
    }

    /// <summary>
    /// Fills the form from a preset, but only if the preset is valid. An invalid preset is
    /// reported and not loaded, because the form cannot represent some invalid values (such as
    /// an unknown bloatware ID) and would silently drop them.
    /// </summary>
    private bool TryLoad(HostConfig config)
    {
        var errors = generation.Validate(config);
        if (errors.Count > 0)
        {
            Errors.AddRange(errors);
            return false;
        }
        Form = GeneratorForm.FromHostConfig(config);
        return true;
    }
}

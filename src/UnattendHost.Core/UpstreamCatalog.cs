using Schneegans.Unattend;

namespace UnattendHost.Core;

public sealed record CatalogItem(string Id, string DisplayName)
{
    /// <summary>Display name plus ID, so the UI shows the exact value that ends up in the preset.</summary>
    public string Label => $"{DisplayName} ({Id})";
}

/// <summary>
/// The lists of choices the UI offers, taken from upstream's own data so they stay in sync
/// when upstream is bumped. Nothing here is hardcoded.
/// </summary>
public sealed class UpstreamCatalog(UnattendGenerator generator)
{
    public IReadOnlyList<CatalogItem> ImageLanguages { get; } =
        Sorted(generator.ImageLanguages.Values.Select(x => new CatalogItem(x.Id, x.DisplayName)));

    public IReadOnlyList<CatalogItem> UserLocales { get; } =
        Sorted(generator.UserLocales.Values.Select(x => new CatalogItem(x.Id, x.DisplayName)));

    public IReadOnlyList<CatalogItem> KeyboardIdentifiers { get; } =
        Sorted(generator.KeyboardIdentifiers.Values.Select(x => new CatalogItem(x.Id, x.DisplayName)));

    public IReadOnlyList<CatalogItem> GeoLocations { get; } =
        Sorted(generator.GeoLocations.Values.Select(x => new CatalogItem(x.Id, x.DisplayName)));

    /// <summary>Editions upstream marks as visible.</summary>
    public IReadOnlyList<CatalogItem> WindowsEditions { get; } =
        Sorted(generator.WindowsEditions.Values.Where(x => x.Visible).Select(x => new CatalogItem(x.Id, x.DisplayName)));

    public IReadOnlyList<CatalogItem> Bloatwares { get; } =
        Sorted(generator.Bloatwares.Values.Select(x => new CatalogItem(x.Id, x.DisplayName)));

    private static List<CatalogItem> Sorted(IEnumerable<CatalogItem> items)
    {
        return [.. items.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id, StringComparer.Ordinal)];
    }
}

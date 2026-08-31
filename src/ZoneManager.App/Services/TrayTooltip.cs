namespace ZoneManager.App.Services;

/// <summary>
/// Baut den Tooltip des Infobereichssymbols. Windows kürzt Tooltips über 63 Zeichen hart,
/// deshalb wird der Text hier kontrolliert gekürzt.
/// </summary>
public static class TrayTooltip
{
    public const int MaximumLength = 63;

    public static string Build(string productName, int monitorCount, bool elevationRestricted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        var text = $"{productName} · {monitorCount} Monitore";
        if (!elevationRestricted)
        {
            return Shorten(text);
        }

        var full = $"{text} · {ElevationNotice.TraySuffix}";
        return full.Length <= MaximumLength ? full : Shorten($"{text} · eingeschränkt");
    }

    private static string Shorten(string text) =>
        text.Length <= MaximumLength ? text : text[..(MaximumLength - 1)] + "…";
}

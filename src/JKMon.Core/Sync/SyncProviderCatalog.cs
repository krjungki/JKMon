namespace JKMon.Core.Sync;

/// <summary>Known provider ids and the order their circles appear in unless the user reorders them.</summary>
public static class SyncProviderCatalog
{
    public const string OneDrive = "onedrive";
    public const string Syncthing = "syncthing";
    public const string GlobalSecureAccess = "gsa";

    public static IReadOnlyList<string> DefaultOrder { get; } = [OneDrive, Syncthing, GlobalSecureAccess];

    public static string DisplayName(string providerId) => providerId switch
    {
        OneDrive => "OneDrive",
        Syncthing => "Syncthing",
        GlobalSecureAccess => "Global Secure Access",
        _ => providerId
    };

    /// <summary>
    /// Keeps the requested order, drops ids this build does not know and appends any provider the stored order
    /// predates, so adding a provider never leaves it invisible.
    /// </summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? order)
    {
        var result = new List<string>(DefaultOrder.Count);

        foreach (var id in order ?? [])
        {
            var trimmed = id?.Trim();
            if (trimmed is { Length: > 0 } && DefaultOrder.Contains(trimmed) && !result.Contains(trimmed))
            {
                result.Add(trimmed);
            }
        }

        foreach (var id in DefaultOrder)
        {
            if (!result.Contains(id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>Providers the order does not mention sort last while keeping their relative order.</summary>
    public static int RankOf(IReadOnlyList<string> order, string providerId)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (string.Equals(order[i], providerId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}

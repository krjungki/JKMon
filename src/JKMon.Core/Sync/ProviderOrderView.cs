namespace JKMon.Core.Sync;

/// <summary>
/// The stored provider order always covers every provider this build knows, but a machine without Syncthing or
/// Global Secure Access should not be asked to arrange circles it will never see. This restricts the order to the
/// providers that are actually present while leaving the absent ones untouched in the stored list, so their
/// position survives until the client is installed.
/// </summary>
public static class ProviderOrderView
{
    public static IReadOnlyList<string> Visible(IReadOnlyList<string> order, IEnumerable<string> present)
    {
        var known = new HashSet<string>(present, StringComparer.Ordinal);
        return order.Where(known.Contains).ToList();
    }

    /// <summary>
    /// Swaps two entries of the full order addressed by their position among the visible ones. Swapping rather than
    /// removing and reinserting is what keeps the absent providers pinned to their stored slots.
    /// </summary>
    public static IReadOnlyList<string> Move(
        IReadOnlyList<string> order, IEnumerable<string> present, int fromVisible, int toVisible)
    {
        var known = new HashSet<string>(present, StringComparer.Ordinal);
        var slots = new List<int>();
        for (var i = 0; i < order.Count; i++)
        {
            if (known.Contains(order[i]))
            {
                slots.Add(i);
            }
        }

        if (fromVisible < 0 || toVisible < 0 || fromVisible >= slots.Count || toVisible >= slots.Count)
        {
            return order;
        }

        var result = order.ToList();
        (result[slots[fromVisible]], result[slots[toVisible]]) = (result[slots[toVisible]], result[slots[fromVisible]]);
        return result;
    }
}

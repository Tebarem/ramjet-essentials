namespace Mc.Scripting.Essentials.Core;

/// <summary>Finds a standing height near a target for <c>teleport-safety</c>: the cell under the feet is solid, the feet and head cells are free.
/// The column is fixed by the caller; only the height moves. Pure: the world check is a delegate.</summary>
public static class SafeSpot
{
    /// <summary>How far above and below the target the search goes, in blocks.</summary>
    public const int SearchRadius = 24;

    /// <summary>The nearest standing height to <paramref name="startY"/>, searching up and down alternately within the bounds.</summary>
    /// <param name="startY">The height the target asks for.</param>
    /// <param name="minY">The lowest height that may be used (inclusive).</param>
    /// <param name="maxY">The highest height that may be used (inclusive).</param>
    /// <param name="standable">True when a player can stand with the feet at that height.</param>
    /// <returns>The height, or null when none is near.</returns>
    public static int? FindY(int startY, int minY, int maxY, Func<int, bool> standable)
    {
        if (startY >= minY && startY <= maxY && standable(startY)) return startY;
        for (int offset = 1; offset <= SearchRadius; offset++)
        {
            int down = startY - offset, up = startY + offset;
            if (down >= minY && standable(down)) return down;
            if (up <= maxY && standable(up)) return up;
        }
        return null;
    }
}

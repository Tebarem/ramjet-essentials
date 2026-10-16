namespace Mc.Scripting.Essentials.Core;

/// <summary>Name rules shared by homes, warps, kits and nicks.</summary>
public static class Names
{
    /// <summary>Longest home, warp or kit name.</summary>
    public const int MaxNameLength = 32;

    /// <summary>Longest nick (display name), in characters.</summary>
    public const int MaxNickLength = 16;

    /// <summary>A home, warp or kit name: ASCII letters, digits, underscore and hyphen, 1 to 32 characters.</summary>
    public static bool IsValid(string? name) => name is { Length: > 0 and <= MaxNameLength } && name.All(IsNameChar);

    /// <summary>A nick: the name rules with a shorter limit. Colour codes and spaces are not accepted.</summary>
    public static bool IsValidNick(string? nick) => nick is { Length: > 0 and <= MaxNickLength } && nick.All(IsNameChar);

    /// <summary>The key a name is stored under: names are case-insensitive.</summary>
    public static string Key(string name) => name.ToLowerInvariant();

    private static bool IsNameChar(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-';
}

namespace Mc.Scripting.Essentials.Core;

/// <summary>A place in a world: the world key the engine names it by, the position and the view direction. Plain data, so the core and its tests
/// need no engine types.</summary>
public sealed record StoredLocation(string World, double X, double Y, double Z, float Yaw, float Pitch);

/// <summary>A position without a world, for distance checks on the tick thread (no allocation).</summary>
public readonly record struct Pos(double X, double Y, double Z);

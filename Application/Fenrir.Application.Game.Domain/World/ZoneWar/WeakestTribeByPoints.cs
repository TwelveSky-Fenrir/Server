using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Legacy <c>ReturnSmallTribe</c> (<c>Server/Header/function.h:3145-3165</c>): the tribe holding the
///     <b>strictly lowest tribe-point total</b>, ties resolving to the lowest tribe index. This is the
///     underdog-comeback selector that gates <see cref="TribeSymbolSpawner" />'s x2-life "catch-up" buff: a
///     tribe's own home symbol is spawned at double life only while that tribe is the weakest by points and still
///     holds its own symbol on its own home map (A4-symbol behavior contract, Edge cases: "x2-life catch-up
///     buff", read at <c>S10_MySummon.cpp:1871,2090-2096</c>, applied at <c>:833-836</c>).
///     <para>
///         The legacy "-1 / no weakest tribe" result is gated on the commented-out <c>USE_NEW_CLAN_POINT</c>
///         macro (<c>Server/Header/Protocol/DEFINE.h:92</c>), dead in the shipped ReleaseEU33 build, so in
///         production the weakest tribe is always a real id 0-3 -- this resolver mirrors that by always returning
///         a concrete tribe id.
///     </para>
///     <para>
///         This is the points-based selector the contract specifies. It is deliberately separate from
///         <see cref="TribeSymbolSpawner" />'s current in-file connected-population heuristic, which is a
///         placeholder pending this resolver -- see this task's wiring manifest for the exact swap.
///     </para>
/// </summary>
public static class WeakestTribeByPoints
{
    /// <summary>
    ///     Resolves the weakest tribe from a per-tribe point vector indexed by tribe id. Must contain exactly
    ///     <see cref="WorldStateService.TribeCount" /> entries.
    /// </summary>
    public static byte Resolve(IReadOnlyList<int> tribePoints)
    {
        ArgumentNullException.ThrowIfNull(tribePoints);
        if (tribePoints.Count != WorldStateService.TribeCount)
            throw new ArgumentException(
                $"Expected exactly {WorldStateService.TribeCount} tribe point totals.", nameof(tribePoints));

        byte weakest = 0;
        for (byte i = 1; i < WorldStateService.TribeCount; i++)
            // Strict '<' is what resolves an exact tie in favour of the lowest tribe index, matching legacy.
            if (tribePoints[i] < tribePoints[weakest])
                weakest = i;

        return weakest;
    }

    /// <summary>
    ///     Convenience overload reading each tribe's current cached <see cref="TribeRvrState.Points" /> straight
    ///     from <paramref name="worldState" /> in tribe-id order.
    /// </summary>
    public static byte Resolve(WorldStateService worldState)
    {
        ArgumentNullException.ThrowIfNull(worldState);

        var points = new int[WorldStateService.TribeCount];
        for (byte i = 0; i < WorldStateService.TribeCount; i++)
            points[i] = worldState.GetTribe(i).Points;

        return Resolve(points);
    }
}

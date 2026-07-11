using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The concrete per-owner-state destination table for <see cref="TribeSymbolCatalog" />, transcribed from
///     <c>MySummon::SummonTribeSymbol</c>'s hardcoded per-owner switch (<c>Server/ts25zone/S10_MySummon.cpp:1873-2034</c>)
///     via the A4-symbol behavior contract (Side effects section) -- NOT re-derived from <c>Server/</c> here. This
///     is the data that turns <see cref="TribeSymbolSpawner" />'s already-present despawn-if-not-mine /
///     spawn-if-mine migration loop from a documented no-op (against <see cref="TribeSymbolCatalog.Empty" />) into
///     the real inter-zone symbol relocation: every zone reads all five symbols' current owner state, despawns
///     locally any symbol whose owner-derived destination map is no longer its own, and spawns at the exact
///     tabulated coordinate any symbol whose destination map IS its own.
///     <para>
///         Coordinates are copied verbatim -- <c>MySummon::SummonTribeSymbol</c> spawns via the location-monster
///         mode that copies the coordinate exactly (<c>S10_MySummon.cpp:753-762</c>), never the random-radius
///         path -- so no scatter is applied here either.
///     </para>
///     <para>
///         Owner-state key convention matches <see cref="TribeSymbolSpawner.ResolveOwnerState" />: for symbol
///         indices 0-3 the key is the owning tribe id (0-3); for symbol index 4 (the neutral "monster symbol")
///         the key is <see cref="TribeSymbolCatalog.NeutralUnclaimedOwnerState" /> for the unclaimed/neutral
///         state (legacy owner -1) and the capturing tribe id (0-3) otherwise.
///     </para>
///     <para>
///         <b>Captured-relocation reach (see this cluster's report / open questions):</b> all four tribe-symbol
///         captor placements (owner keys 1/2/3 for a slot whose own tribe is not the owner) ARE populated here, so
///         the catalog is fully ready to relocate a captured tribe symbol to its captor's territory. Today only
///         the neutral monster symbol's captor is actually resolvable at runtime
///         (<c>WorldStateService.World.MonsterSymbol</c> carries the captor id) -- so only symbol 4's captured
///         relocation is live end-to-end. Tribe symbols 0-3 record ownership as a bool
///         (<c>TribeRvrState.HasSymbol</c>) with no captor identity, so a lost tribe symbol still resolves to
///         "unresolvable this pass" upstream and never reaches these captor rows until that schema records a
///         captor. The data is intentionally complete so no further transcription is needed when it does.
///     </para>
/// </summary>
public static class TribeSymbolPlacementCatalog
{
    /// <summary>
    ///     Fully-populated catalog, built once. Intended to replace the <see cref="TribeSymbolCatalog.Empty" />
    ///     DI registration (see this task's wiring manifest).
    /// </summary>
    public static readonly TribeSymbolCatalog Default = Build();

    /// <summary>Builds a fresh populated catalog from the tabulated coordinates.</summary>
    public static TribeSymbolCatalog Build()
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ImmutableDictionary<byte, TribeSymbolPlacement>>();

        // Tribe symbol 0 (special-type 11), by owner tribe 0/1/2/3.
        builder[0] = Owners(
            (0, new TribeSymbolPlacement(2, -1810f, -1f, 3155f)),
            (1, new TribeSymbolPlacement(8, 4410f, 28f, 4666f)),
            (2, new TribeSymbolPlacement(13, -7610f, 0f, 5763f)),
            (3, new TribeSymbolPlacement(142, -2505f, 0f, 7201f)));

        // Tribe symbol 1 (special-type 12), by owner tribe 0/1/2/3.
        builder[1] = Owners(
            (0, new TribeSymbolPlacement(3, -6760f, 0f, 1187f)),
            (1, new TribeSymbolPlacement(7, -831f, 10f, -3392f)),
            (2, new TribeSymbolPlacement(13, -6684f, 0f, 5319f)),
            (3, new TribeSymbolPlacement(142, -2063f, 1f, 6846f)));

        // Tribe symbol 2 (special-type 13), by owner tribe 0/1/2/3.
        builder[2] = Owners(
            (0, new TribeSymbolPlacement(3, -7780f, 0f, 400f)),
            (1, new TribeSymbolPlacement(8, 5493f, 38f, 4174f)),
            (2, new TribeSymbolPlacement(12, -4045f, 0f, 1648f)),
            (3, new TribeSymbolPlacement(142, -2948f, 8f, 6105f)));

        // Tribe symbol 3 (special-type 28), by owner tribe 0/1/2/3.
        builder[3] = Owners(
            (0, new TribeSymbolPlacement(3, -6864f, 0f, 2761f)),
            (1, new TribeSymbolPlacement(8, 5545f, 41f, 6452f)),
            (2, new TribeSymbolPlacement(13, -5397f, 0f, 5819f)),
            (3, new TribeSymbolPlacement(141, -1132f, 0f, 3486f)));

        // Neutral "monster symbol" (special-type 14): unclaimed home (owner -1 -> the sentinel key) plus each
        // capturing tribe 0/1/2/3.
        builder[4] = Owners(
            (TribeSymbolCatalog.NeutralUnclaimedOwnerState, new TribeSymbolPlacement(74, -2f, 0f, 2626f)),
            (0, new TribeSymbolPlacement(4, 7839f, 461f, 6520f)),
            (1, new TribeSymbolPlacement(9, -2438f, -590f, 6697f)),
            (2, new TribeSymbolPlacement(14, 7174f, 336f, 6191f)),
            (3, new TribeSymbolPlacement(143, -38f, 0f, 4432f)));

        return new TribeSymbolCatalog(builder.ToImmutable());
    }

    private static ImmutableDictionary<byte, TribeSymbolPlacement> Owners(
        params (byte OwnerState, TribeSymbolPlacement Placement)[] entries)
    {
        var byOwner = ImmutableDictionary.CreateBuilder<byte, TribeSymbolPlacement>();
        foreach (var (ownerState, placement) in entries)
            byOwner[ownerState] = placement;
        return byOwner.ToImmutable();
    }
}

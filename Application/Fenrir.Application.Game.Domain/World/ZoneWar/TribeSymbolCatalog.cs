using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>One symbol's designated map and spawn coordinate while a particular owner state holds it.</summary>
public readonly record struct TribeSymbolPlacement(short MapId, float X, float Y, float Z);

/// <summary>
///     <c>MySummon::SummonTribeSymbol</c>'s per-owner-state coordinate table
///     (<c>Server/ts25zone/S10_MySummon.cpp:1873-2034</c>), keyed by symbol index (0-3 = one tribe's own slot,
///     4 = the neutral "monster symbol") then by owner state. <see cref="Empty" /> makes every symbol
///     unresolvable (no placement configured for any state), a safe no-op.
///     <para>
///         For symbol indices 0-3, <see cref="WorldStateService" /> today only ever resolves owner state ==
///         that same tribe id -- a symbol a tribe has *lost* has no recorded captor identity
///         (<see cref="WorldStateService.ResolveTribeSymbol" />'s own remarks: <c>TribeRvrState.HasSymbol</c>
///         is a bool, not an owner id), so only that one key is ever meaningfully populated for those 4
///         symbols today; see <see cref="TribeSymbolSpawner" />'s own remarks for the full consequence. For
///         symbol index 4 (neutral), <see cref="NeutralUnclaimedOwnerState" /> stands in for
///         <c>WorldRvrState.MonsterSymbol == null</c> ("unclaimed"), and 0-3 stand in for whichever tribe has
///         captured it.
///     </para>
///     <para>
///         The concrete per-state coordinates are hardcoded in the legacy switch and are NOT reproduced here
///         -- populating a real catalog is the same follow-up data-gathering task noted on
///         <see cref="GuardPostCatalog" />.
///     </para>
/// </summary>
public sealed class TribeSymbolCatalog(
    ImmutableDictionary<byte, ImmutableDictionary<byte, TribeSymbolPlacement>> placementsBySymbol)
{
    /// <summary>Sentinel owner-state key for the neutral symbol's "nobody has captured it yet" state.</summary>
    public const byte NeutralUnclaimedOwnerState = 255;

    public static readonly TribeSymbolCatalog Empty =
        new(ImmutableDictionary<byte, ImmutableDictionary<byte, TribeSymbolPlacement>>.Empty);

    public bool TryGetPlacement(byte symbolIndex, byte ownerState, out TribeSymbolPlacement placement)
    {
        placement = default;
        return placementsBySymbol.TryGetValue(symbolIndex, out var byOwnerState) &&
               byOwnerState.TryGetValue(ownerState, out placement);
    }
}

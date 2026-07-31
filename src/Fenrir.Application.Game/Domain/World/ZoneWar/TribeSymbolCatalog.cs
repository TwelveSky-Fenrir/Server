using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public readonly record struct TribeSymbolPlacement(short MapId, float X, float Y, float Z);

public sealed class TribeSymbolCatalog(
    ImmutableDictionary<byte, ImmutableDictionary<byte, TribeSymbolPlacement>> placementsBySymbol)
{
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

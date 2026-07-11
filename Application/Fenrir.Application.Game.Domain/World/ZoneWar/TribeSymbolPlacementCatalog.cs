using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class TribeSymbolPlacementCatalog
{
    public static readonly TribeSymbolCatalog Default = Build();

    public static TribeSymbolCatalog Build()
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ImmutableDictionary<byte, TribeSymbolPlacement>>();

        builder[0] = Owners(
            (0, new TribeSymbolPlacement(2, -1810f, -1f, 3155f)),
            (1, new TribeSymbolPlacement(8, 4410f, 28f, 4666f)),
            (2, new TribeSymbolPlacement(13, -7610f, 0f, 5763f)),
            (3, new TribeSymbolPlacement(142, -2505f, 0f, 7201f)));

        builder[1] = Owners(
            (0, new TribeSymbolPlacement(3, -6760f, 0f, 1187f)),
            (1, new TribeSymbolPlacement(7, -831f, 10f, -3392f)),
            (2, new TribeSymbolPlacement(13, -6684f, 0f, 5319f)),
            (3, new TribeSymbolPlacement(142, -2063f, 1f, 6846f)));

        builder[2] = Owners(
            (0, new TribeSymbolPlacement(3, -7780f, 0f, 400f)),
            (1, new TribeSymbolPlacement(8, 5493f, 38f, 4174f)),
            (2, new TribeSymbolPlacement(12, -4045f, 0f, 1648f)),
            (3, new TribeSymbolPlacement(142, -2948f, 8f, 6105f)));

        builder[3] = Owners(
            (0, new TribeSymbolPlacement(3, -6864f, 0f, 2761f)),
            (1, new TribeSymbolPlacement(8, 5545f, 41f, 6452f)),
            (2, new TribeSymbolPlacement(13, -5397f, 0f, 5819f)),
            (3, new TribeSymbolPlacement(141, -1132f, 0f, 3486f)));

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

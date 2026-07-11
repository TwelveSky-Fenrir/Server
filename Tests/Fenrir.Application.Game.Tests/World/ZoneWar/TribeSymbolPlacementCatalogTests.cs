using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Pins every per-owner-state coordinate in <see cref="TribeSymbolPlacementCatalog" /> to the exact value the
///     A4-symbol behavior contract transcribes from <c>MySummon::SummonTribeSymbol</c>
///     (<c>S10_MySummon.cpp:1873-2034</c>). Pure data assertions -- no zone/tick needed.
/// </summary>
public class TribeSymbolPlacementCatalogTests
{
    private static readonly TribeSymbolCatalog Catalog = TribeSymbolPlacementCatalog.Default;

    private static TribeSymbolPlacement Get(byte symbolIndex, byte ownerState)
    {
        Assert.True(Catalog.TryGetPlacement(symbolIndex, ownerState, out var placement),
            $"catalog is missing symbol {symbolIndex} owner {ownerState}");
        return placement;
    }

    [Fact]
    public void Symbol0_ByOwner_MatchesContractTable()
    {
        Assert.Equal(new TribeSymbolPlacement(2, -1810f, -1f, 3155f), Get(0, 0));
        Assert.Equal(new TribeSymbolPlacement(8, 4410f, 28f, 4666f), Get(0, 1));
        Assert.Equal(new TribeSymbolPlacement(13, -7610f, 0f, 5763f), Get(0, 2));
        Assert.Equal(new TribeSymbolPlacement(142, -2505f, 0f, 7201f), Get(0, 3));
    }

    [Fact]
    public void Symbol1_ByOwner_MatchesContractTable()
    {
        Assert.Equal(new TribeSymbolPlacement(3, -6760f, 0f, 1187f), Get(1, 0));
        Assert.Equal(new TribeSymbolPlacement(7, -831f, 10f, -3392f), Get(1, 1));
        Assert.Equal(new TribeSymbolPlacement(13, -6684f, 0f, 5319f), Get(1, 2));
        Assert.Equal(new TribeSymbolPlacement(142, -2063f, 1f, 6846f), Get(1, 3));
    }

    [Fact]
    public void Symbol2_ByOwner_MatchesContractTable()
    {
        Assert.Equal(new TribeSymbolPlacement(3, -7780f, 0f, 400f), Get(2, 0));
        Assert.Equal(new TribeSymbolPlacement(8, 5493f, 38f, 4174f), Get(2, 1));
        Assert.Equal(new TribeSymbolPlacement(12, -4045f, 0f, 1648f), Get(2, 2));
        Assert.Equal(new TribeSymbolPlacement(142, -2948f, 8f, 6105f), Get(2, 3));
    }

    [Fact]
    public void Symbol3_ByOwner_MatchesContractTable()
    {
        Assert.Equal(new TribeSymbolPlacement(3, -6864f, 0f, 2761f), Get(3, 0));
        Assert.Equal(new TribeSymbolPlacement(8, 5545f, 41f, 6452f), Get(3, 1));
        Assert.Equal(new TribeSymbolPlacement(13, -5397f, 0f, 5819f), Get(3, 2));
        Assert.Equal(new TribeSymbolPlacement(141, -1132f, 0f, 3486f), Get(3, 3));
    }

    [Fact]
    public void MonsterSymbol_NeutralAndCaptors_MatchContractTable()
    {
        // Owner -1 (neutral / unclaimed) -> the sentinel key -> neutral home zone 74.
        Assert.Equal(new TribeSymbolPlacement(74, -2f, 0f, 2626f),
            Get(4, TribeSymbolCatalog.NeutralUnclaimedOwnerState));
        Assert.Equal(new TribeSymbolPlacement(4, 7839f, 461f, 6520f), Get(4, 0));
        Assert.Equal(new TribeSymbolPlacement(9, -2438f, -590f, 6697f), Get(4, 1));
        Assert.Equal(new TribeSymbolPlacement(14, 7174f, 336f, 6191f), Get(4, 2));
        Assert.Equal(new TribeSymbolPlacement(143, -38f, 0f, 4432f), Get(4, 3));
    }

    [Fact]
    public void EveryTribeSymbolHasAllFourCaptorRows_ReadyForCapturedRelocation()
    {
        // The captor placements (owner != slot tribe) are populated for all four tribe symbols even though
        // upstream owner resolution can only reach them for the neutral symbol today -- the data is complete so
        // no re-transcription is needed once a captor identity is recorded for tribe symbols 0-3.
        for (byte symbolIndex = 0; symbolIndex < 4; symbolIndex++)
        for (byte owner = 0; owner < 4; owner++)
            Assert.True(Catalog.TryGetPlacement(symbolIndex, owner, out _),
                $"tribe symbol {symbolIndex} is missing captor row {owner}");
    }

    [Fact]
    public void UnconfiguredOwnerState_ReturnsFalse_NoGuessing()
    {
        // A tribe symbol has no key 255 (the neutral sentinel is only meaningful for symbol 4), and no owner 5.
        Assert.False(Catalog.TryGetPlacement(0, TribeSymbolCatalog.NeutralUnclaimedOwnerState, out _));
        Assert.False(Catalog.TryGetPlacement(2, 5, out _));
        Assert.False(Catalog.TryGetPlacement(9, 0, out _)); // no such symbol index
    }
}

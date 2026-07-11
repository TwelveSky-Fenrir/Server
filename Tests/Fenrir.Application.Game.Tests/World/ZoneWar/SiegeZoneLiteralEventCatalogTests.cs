using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class SiegeZoneLiteralEventCatalogTests
{
    [Theory]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone267NoOpEventCode, 402)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone267WritesState1EventCode, 403)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone267WritesState2EventCode, 404)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone267WritesState3EventCode, 405)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone267WritesState5EventCodeA, 406)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone267WritesState5EventCodeB, 407)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone267WritesState4EventCode, 408)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone267WritesState5EventCodeC, 409)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone267ResetEventCode, 410)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241ChallengeStartedEventCode, 411)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241FailureEventCodeA, 412)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241FailureEventCodeB, 413)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241SuccessEventCode, 414)]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241ReturnTownEventCode, 415)]
    [InlineData(SiegeZoneLiteralEventCatalog.WarHasStartedEventCode, 416)]
    [InlineData(SiegeZoneLiteralEventCatalog.InstinctDefenseFormationInUseEventCode, 418)]
    [InlineData(SiegeZoneLiteralEventCatalog.ReturnToOriginalFactionEventCode, 419)]
    [InlineData(SiegeZoneLiteralEventCatalog.RemainToFolBeginEventCode, 420)]
    [InlineData(SiegeZoneLiteralEventCatalog.FolBeganEventCode, 421)]
    [InlineData(SiegeZoneLiteralEventCatalog.FolSuccessEventCode, 422)]
    [InlineData(SiegeZoneLiteralEventCatalog.UnlabeledEventCode423, 423)]
    [InlineData(SiegeZoneLiteralEventCatalog.FolSuccessDuplicateLabelEventCode, 424)]
    [InlineData(SiegeZoneLiteralEventCatalog.RemainToFolAnnihilationEventCode, 425)]
    [InlineData(SiegeZoneLiteralEventCatalog.FolAnnihilationBeganEventCode, 426)]
    [InlineData(SiegeZoneLiteralEventCatalog.FolAnnihilationSucceedEventCode, 427)]
    [InlineData(SiegeZoneLiteralEventCatalog.FolAnnihilationFailedEventCode, 428)]
    [InlineData(SiegeZoneLiteralEventCatalog.AllFactionsAllianceRevokedEventCode, 500)]
    public void NamedConstant_MatchesTheContractsLiteralValue(int namedConstant, int expected)
    {
        Assert.Equal(expected, namedConstant);
    }

    [Fact]
    public void Zone267Bounds_MatchTheIngestorsOwnRangeConstants()
    {
        Assert.Equal(ZoneCenterBroadcastIngestor.Zone267RangeStart, SiegeZoneLiteralEventCatalog.Zone267NoOpEventCode);
        Assert.Equal(ZoneCenterBroadcastIngestor.Zone267RangeEnd, SiegeZoneLiteralEventCatalog.Zone267ResetEventCode);
    }

    [Fact]
    public void Zone241Bounds_MatchTheIngestorsOwnRangeConstants()
    {
        Assert.Equal(ZoneCenterBroadcastIngestor.Zone241RangeStart,
            SiegeZoneLiteralEventCatalog.Zone241ChallengeStartedEventCode);
        Assert.Equal(ZoneCenterBroadcastIngestor.Zone241RangeEnd,
            SiegeZoneLiteralEventCatalog.Zone241ReturnTownEventCode);
    }

    [Fact]
    public void PureRelayEventCodes_ContainsExactlyTheThirteenNamedCodes_AndNeverTheOthers()
    {
        var expected = new[]
        {
            416, 418, 419, 420, 421, 422, 423, 424, 425, 426, 427, 428, 500
        };

        Assert.Equal(expected.Length, SiegeZoneLiteralEventCatalog.PureRelayEventCodes.Count);
        foreach (var code in expected)
            Assert.Contains(code, (IEnumerable<int>)SiegeZoneLiteralEventCatalog.PureRelayEventCodes);

        Assert.DoesNotContain(417, (IEnumerable<int>)SiegeZoneLiteralEventCatalog.PureRelayEventCodes);
        Assert.DoesNotContain(402, (IEnumerable<int>)SiegeZoneLiteralEventCatalog.PureRelayEventCodes);
        Assert.DoesNotContain(411, (IEnumerable<int>)SiegeZoneLiteralEventCatalog.PureRelayEventCodes);
    }

    [Fact]
    public void KnownGapEventCodes_ContainsOnly417()
    {
        Assert.Contains(417, (IEnumerable<int>)SiegeZoneLiteralEventCatalog.KnownGapEventCodes);
        Assert.Single(SiegeZoneLiteralEventCatalog.KnownGapEventCodes);
    }

    [Fact]
    public void PureRelayEventCodes_And_KnownGapEventCodes_AreDisjoint()
    {
        foreach (var code in SiegeZoneLiteralEventCatalog.PureRelayEventCodes)
            Assert.DoesNotContain(code, (IEnumerable<int>)SiegeZoneLiteralEventCatalog.KnownGapEventCodes);
    }

    [Theory]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241ChallengeStartedEventCode,
        "Den of Rebirth Challenge 0 - 11 // 0 // Name")]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241FailureEventCodeA, "Den of Rebirth Failure 0 - 11 // 0 // Name")]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241FailureEventCodeB, "Den of Rebirth Failure 0 - 11 // 0 // Name")]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241SuccessEventCode, "Den of Rebirth Success 0 - 11 // 0 // Name")]
    [InlineData(SiegeZoneLiteralEventCatalog.Zone241ReturnTownEventCode, "Den of Rebirth Return Town 0 - 11 //")]
    [InlineData(SiegeZoneLiteralEventCatalog.WarHasStartedEventCode, "The War has started")]
    [InlineData(SiegeZoneLiteralEventCatalog.InstinctDefenseFormationInUseEventCode,
        "Instinct defense formation in use")]
    [InlineData(SiegeZoneLiteralEventCatalog.ReturnToOriginalFactionEventCode, "return to original faction")]
    [InlineData(SiegeZoneLiteralEventCatalog.RemainToFolBeginEventCode, "remain to FoL begin")]
    [InlineData(SiegeZoneLiteralEventCatalog.FolBeganEventCode, "FoL began")]
    [InlineData(SiegeZoneLiteralEventCatalog.FolSuccessEventCode, "FoL success")]
    [InlineData(SiegeZoneLiteralEventCatalog.FolSuccessDuplicateLabelEventCode, "FoL success")]
    [InlineData(SiegeZoneLiteralEventCatalog.RemainToFolAnnihilationEventCode, "remain to FoL annhi")]
    [InlineData(SiegeZoneLiteralEventCatalog.FolAnnihilationBeganEventCode, "FoL annhi began")]
    [InlineData(SiegeZoneLiteralEventCatalog.FolAnnihilationSucceedEventCode, "FoL annhi succeed")]
    [InlineData(SiegeZoneLiteralEventCatalog.FolAnnihilationFailedEventCode, "FoL annhi falied")]
    [InlineData(SiegeZoneLiteralEventCatalog.AllFactionsAllianceRevokedEventCode, "all factions alliance revoked")]
    public void TryGetLegacyLabel_ReturnsTheVerbatimLegacyText(int eventCode, string expectedLabel)
    {
        Assert.True(SiegeZoneLiteralEventCatalog.TryGetLegacyLabel(eventCode, out var label));
        Assert.Equal(expectedLabel, label);
    }

    [Fact]
    public void TryGetLegacyLabel_422And424_ShareTheIdenticalDuplicatedText()
    {
        SiegeZoneLiteralEventCatalog.TryGetLegacyLabel(SiegeZoneLiteralEventCatalog.FolSuccessEventCode,
            out var label422);
        SiegeZoneLiteralEventCatalog.TryGetLegacyLabel(SiegeZoneLiteralEventCatalog.FolSuccessDuplicateLabelEventCode,
            out var label424);

        Assert.Equal(label422, label424);
    }

    [Fact]
    public void TryGetLegacyLabel_412And413_ShareTheIdenticalDuplicatedText()
    {
        SiegeZoneLiteralEventCatalog.TryGetLegacyLabel(SiegeZoneLiteralEventCatalog.Zone241FailureEventCodeA,
            out var label412);
        SiegeZoneLiteralEventCatalog.TryGetLegacyLabel(SiegeZoneLiteralEventCatalog.Zone241FailureEventCodeB,
            out var label413);

        Assert.Equal(label412, label413);
    }

    [Theory]
    [InlineData(417)]
    [InlineData(423)]
    [InlineData(64)]
    [InlineData(402)]
    [InlineData(999999)]
    public void TryGetLegacyLabel_ReturnsFalse_ForCodesWithNoRecoverableLabel(int eventCode)
    {
        Assert.False(SiegeZoneLiteralEventCatalog.TryGetLegacyLabel(eventCode, out var label));
        Assert.Equal(string.Empty, label);
    }
}

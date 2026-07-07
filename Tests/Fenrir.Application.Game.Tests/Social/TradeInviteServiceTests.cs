using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Social;

/// <summary>
///     Covers <see cref="TradeInviteService.Invite" />'s response-code-order: the asker's own busy state
///     must be checked before the target avatar is resolved by name
///     (Server/ts25zone/S04_MyWork02.cpp:8259-8277,8459-8471,9088-9101,9311-9324 pre-check family).
/// </summary>
public class TradeInviteServiceTests
{
    private static (TradeInviteService Service, ZoneRegistry Zones, TradeRegistry Trades) CreateService(short mapId)
    {
        var trades = new TradeRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([mapId]);
        return (new TradeInviteService(trades), zones, trades);
    }

    private static PlayerRuntimeState Enter(ZoneRegistry zones, short mapId, int characterId, string name,
        byte tribe = 1)
    {
        zones.TryGet(mapId, out var zone);
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone!.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, mapId, name, tribe: tribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return state!;
    }

    [Fact]
    public void Invite_AskerBusy_AndTargetNameDoesNotExist_ReturnsAskerBusy_NotTargetNotFound()
    {
        var (service, zones, trades) = CreateService(1);
        var asker = Enter(zones, 1, 1, "Asker");
        Enter(zones, 1, 2, "PendingTarget");

        Assert.Equal(TradeAskOutcome.Sent, trades.TryAsk(1, 2)); // still pending, never answered

        var result = service.Invite(zones[1], asker, "NoSuchAvatar");

        Assert.Equal(TradeInviteResultKind.AskerBusy, result.Kind);
    }

    [Fact]
    public void Invite_AskerNotBusy_TargetNameDoesNotExist_ReturnsTargetNotFound()
    {
        var (service, zones, _) = CreateService(1);
        var asker = Enter(zones, 1, 1, "Asker");

        var result = service.Invite(zones[1], asker, "NoSuchAvatar");

        Assert.Equal(TradeInviteResultKind.TargetNotFound, result.Kind);
    }

    /// <summary>
    ///     Covers the trade-ask displayed-level combined-level extension: the outward Level value must be
    ///     <see cref="PlayerRuntimeState.CombinedLevel" /> (aLevel1+aLevel2), sent verbatim with no offset.
    /// </summary>
    [Fact]
    public void Invite_Success_SendsAskersCombinedLevel_NotOrdinaryLevelAlone()
    {
        var (service, zones, _) = CreateService(1);
        var asker = Enter(zones, 1, 1, "Asker");
        asker.Level = 30;
        asker.Level2 = 7;
        Enter(zones, 1, 2, "Target");

        var result = service.Invite(zones[1], asker, "Target");

        Assert.Equal(TradeInviteResultKind.Sent, result.Kind);
        Assert.Equal(37, result.AskerLevel);
    }
}

using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Social;

/// <summary>
///     Covers <see cref="DuelService" />'s CZ_DUEL_ASK_SEND/CZ_DUEL_START_SEND behavior most relevant to
///     Active-duel resolution: the requester's-own-already-dueling desync outcome
///     (<see cref="DuelAskResultKind.ChallengerAlreadyDueling" />), and that the 180-legacy-tick countdown
///     seeded at Start is the single <see cref="DuelRegistry.DurationTicks" /> source of truth, not a
///     separately duplicated literal.
/// </summary>
public class DuelServiceTests
{
    private static (DuelService Service, ZoneRegistry Zones, DuelRegistry Duels) CreateService(short mapId)
    {
        var duels = new DuelRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([mapId]);
        return (new DuelService(zones, duels), zones, duels);
    }

    private static PlayerRuntimeState Enter(ZoneRegistry zones, short mapId, int characterId, string name, byte tribe)
    {
        var (_, state) = EnterWithPipe(zones, mapId, characterId, name, tribe);
        return state;
    }

    private static (FakeDuplexPipe Pipe, PlayerRuntimeState State) EnterWithPipe(ZoneRegistry zones, short mapId,
        int characterId, string name, byte tribe)
    {
        zones.TryGet(mapId, out var zone);
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        zone!.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, mapId, name, tribe: tribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return (pipe, state!);
    }

    [Fact]
    public void Ask_ChallengerAlreadyActivelyDueling_ReturnsChallengerAlreadyDueling()
    {
        var (service, zones, duels) = CreateService(1);
        var challenger = Enter(zones, 1, 10, "Challenger", tribe: 1);
        Enter(zones, 1, 20, "PriorOpponent", tribe: 1);
        Enter(zones, 1, 30, "NewTarget", tribe: 1);

        // 10 is already Active-dueling 20 (seeded directly, bypassing the ask/accept/start round trip).
        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 20, false));
        Assert.True(duels.TryAnswer(20, true, out _));
        Assert.True(duels.TryStart(10, out _));

        var result = service.Ask(zones[1], challenger, "NewTarget", sort: 0);

        Assert.Equal(DuelAskResultKind.ChallengerAlreadyDueling, result);
    }

    [Fact]
    public void Ask_OrdinaryBusyChallenger_StillNegotiating_ReturnsChallengerBusy()
    {
        var (service, zones, duels) = CreateService(1);
        var challenger = Enter(zones, 1, 10, "Challenger", tribe: 1);
        Enter(zones, 1, 20, "PendingTarget", tribe: 1);
        Enter(zones, 1, 30, "NewTarget", tribe: 1);

        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 20, false)); // still pending, never answered

        var result = service.Ask(zones[1], challenger, "NewTarget", sort: 0);

        Assert.Equal(DuelAskResultKind.ChallengerBusy, result);
    }

    [Fact]
    public void Start_SendsRemainTimeEqualToDurationTicks_NotASeparateLiteral()
    {
        var (service, zones, duels) = CreateService(1);
        var (pipeA, _) = EnterWithPipe(zones, 1, 10, "A", tribe: 1);
        EnterWithPipe(zones, 1, 20, "B", tribe: 1);
        duels.TryAsk(10, 20, false);
        duels.TryAnswer(20, true, out _);
        ZoneTestKit.DrainOutbound(pipeA);

        service.Start(10);

        Assert.True(duels.TryGetActiveDuel(10, out var duel));
        Assert.Equal(DuelRegistry.DurationTicks, duel!.RemainingTicks);

        var expected = new byte[FrameWriter.FrameSizeOf<DuelStartResponse>()];
        FrameWriter.WriteFrame(new DuelStartResponse
        {
            DuelState = [1, duel.UniqueNumber, 1], RemainTime = DuelRegistry.DurationTicks, EatDrugState = 0
        }, expected);
        var actual = ZoneTestKit.DrainOutbound(pipeA);
        Assert.Equal(expected, actual);
    }
}

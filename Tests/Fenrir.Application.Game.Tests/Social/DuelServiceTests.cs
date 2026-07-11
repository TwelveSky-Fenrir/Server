using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        return CreateService(mapId, new FakeCharacterShardLocationRepository(), new FakeSocialCrossShardRelayQueue());
    }

    private static (DuelService Service, ZoneRegistry Zones, DuelRegistry Duels) CreateService(short mapId,
        ICharacterShardLocationRepository directory, FakeSocialCrossShardRelayQueue relay)
    {
        var duels = new DuelRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([mapId]);
        return (new DuelService(zones, duels, new TradeRegistry(), new FriendRegistry(), new PartyRegistry(),
                new MentorRegistry(), new GuildInviteRegistry(), directory, relay,
                Options.Create(new GameServerOptions { ShardId = 1 }), NullLogger<DuelService>.Instance),
            zones, duels);
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
    public async Task Ask_ChallengerAlreadyActivelyDueling_ReturnsChallengerAlreadyDueling()
    {
        var (service, zones, duels) = CreateService(1);
        var challenger = Enter(zones, 1, 10, "Challenger", 1);
        Enter(zones, 1, 20, "PriorOpponent", 1);
        Enter(zones, 1, 30, "NewTarget", 1);

        // 10 is already Active-dueling 20 (seeded directly, bypassing the ask/accept/start round trip).
        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 20, false));
        Assert.True(duels.TryAnswer(20, true, out _));
        Assert.True(duels.TryStart(10, out _));

        var result = await service.AskAsync(zones[1], challenger, "NewTarget", 0, CancellationToken.None);

        Assert.Equal(DuelAskResultKind.ChallengerAlreadyDueling, result);
    }

    [Fact]
    public async Task Ask_OrdinaryBusyChallenger_StillNegotiating_ReturnsChallengerBusy()
    {
        var (service, zones, duels) = CreateService(1);
        var challenger = Enter(zones, 1, 10, "Challenger", 1);
        Enter(zones, 1, 20, "PendingTarget", 1);
        Enter(zones, 1, 30, "NewTarget", 1);

        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 20, false)); // still pending, never answered

        var result = await service.AskAsync(zones[1], challenger, "NewTarget", 0, CancellationToken.None);

        Assert.Equal(DuelAskResultKind.ChallengerBusy, result);
    }

    /// <summary>
    ///     Response-code-order regression: the challenger's own busy/pose state must be checked before the
    ///     target avatar is resolved by name, so a busy challenger naming a nonexistent avatar still gets
    ///     the busy reply, not "target not found" (Server/ts25zone/S04_MyWork02.cpp:8259-8277).
    /// </summary>
    [Fact]
    public async Task Ask_ChallengerBusy_AndTargetNameDoesNotExist_ReturnsChallengerBusy_NotTargetNotFound()
    {
        var (service, zones, duels) = CreateService(1);
        var challenger = Enter(zones, 1, 10, "Challenger", 1);
        Enter(zones, 1, 20, "PendingTarget", 1);

        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 20, false)); // still pending, never answered

        var result = await service.AskAsync(zones[1], challenger, "NoSuchAvatar", 0, CancellationToken.None);

        Assert.Equal(DuelAskResultKind.ChallengerBusy, result);
    }

    /// <summary>Same response-code-order regression as above, for the desynced-Active-duel outcome.</summary>
    [Fact]
    public async Task Ask_ChallengerAlreadyActivelyDueling_AndTargetNameDoesNotExist_ReturnsChallengerAlreadyDueling()
    {
        var (service, zones, duels) = CreateService(1);
        var challenger = Enter(zones, 1, 10, "Challenger", 1);
        Enter(zones, 1, 20, "PriorOpponent", 1);

        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(10, 20, false));
        Assert.True(duels.TryAnswer(20, true, out _));
        Assert.True(duels.TryStart(10, out _));

        var result = await service.AskAsync(zones[1], challenger, "NoSuchAvatar", 0, CancellationToken.None);

        Assert.Equal(DuelAskResultKind.ChallengerAlreadyDueling, result);
    }

    /// <summary>WS1.4 ASK-PUBLISH-ONLY: a same-shard miss that resolves cross-shard publishes an Ask.</summary>
    [Fact]
    public async Task Ask_SameShardMiss_ResolvesCrossShard_PublishesAskAndReturnsSentCrossShard()
    {
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(2, 9, 77, "RemoteTarget", 1, DateTime.UtcNow));
        var relay = new FakeSocialCrossShardRelayQueue();
        var (service, zones, duels) = CreateService(1, directory, relay);
        var challenger = Enter(zones, 1, 10, "Challenger", 1);

        var result = await service.AskAsync(zones[1], challenger, "RemoteTarget", 0, CancellationToken.None);

        Assert.Equal(DuelAskResultKind.SentCrossShard, result);
        Assert.Single(relay.Enqueued);
        Assert.Equal(SocialCrossShardRelayKind.Duel, relay.Enqueued[0].Kind);
        Assert.True(duels.IsNegotiating(10));
    }

    [Fact]
    public void Start_SendsRemainTimeEqualToDurationTicks_NotASeparateLiteral()
    {
        var (service, zones, duels) = CreateService(1);
        var (pipeA, _) = EnterWithPipe(zones, 1, 10, "A", 1);
        EnterWithPipe(zones, 1, 20, "B", 1);
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

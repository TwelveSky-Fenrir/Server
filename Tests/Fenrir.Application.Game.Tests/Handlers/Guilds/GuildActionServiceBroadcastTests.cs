using System.Buffers.Binary;
using Fenrir.Application.Game.Abstractions.Guilds;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Guilds;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers.Guilds;

// C08(b): GUILD_WORK notice/AGM/title/buff must mirror the refreshed GUILD_INFO onto every other currently
// connected member -- across zones, not just the acting character (who gets its own copy via Result) or the
// specific target (who separately gets a GuildMembershipZoneCommand mirror of their own PlayerRuntimeState).
public class GuildActionServiceBroadcastTests
{
    private const int GuildId = 10;
    private const int MasterId = 1;
    private const int MemberId = 2;
    private const int OutsiderId = 3;

    [Fact]
    public async Task Notice_Success_BroadcastsGuildInfo_ToOnlineMemberInAnotherZone_ButNotToAnOutsider()
    {
        var (zones, guilds) = CreateWorld();
        var (masterSession, masterPipe, masterState) = EnterZone(zones, 1, MasterId, "Odin", GuildId, 2);
        var (memberSession, memberPipe, _) = EnterZone(zones, 2, MemberId, "Thor", GuildId, 0);
        var (_, outsiderPipe, _) = EnterZone(zones, 2, OutsiderId, "Loki", null, 0);

        guilds.Seed(SeedGuild(memberCount: 2));

        var service = CreateService(zones, guilds);

        var data = new byte[500];
        new GuildWorkNoticePayload { Notices = ["Raid at 8pm", "", "", ""] }.Write(data);

        var result = await service.UpdateGuildNoticeAsync(new GuildActionRequest { Sort = 5, Data = data },
            masterState, CancellationToken.None);
        Respond(masterSession, result);

        AssertGuildActionResponse(ZoneTestKit.DrainOutbound(masterPipe), 5);
        AssertGuildActionResponse(ZoneTestKit.DrainOutbound(memberPipe), 5);
        Assert.Empty(ZoneTestKit.DrainOutbound(outsiderPipe));
        Assert.Null(memberSession.DisconnectReason);
    }

    [Fact]
    public async Task Agm_Promote_BroadcastsGuildInfo_ToOnlineThirdMember_NotJustActorAndTarget()
    {
        var (zones, guilds) = CreateWorld();
        var (masterSession, masterPipe, masterState) = EnterZone(zones, 1, MasterId, "Odin", GuildId, 2);
        EnterZone(zones, 1, MemberId, "Thor", GuildId, 0);
        var (_, bystanderPipe, _) = EnterZone(zones, 2, OutsiderId, "Freya", GuildId, 0);

        guilds.SeedRoster(GuildId,
            new GuildRosterRowDto(GuildId, "Aesir", MasterId, "Odin", 2, "", DateTime.UtcNow),
            new GuildRosterRowDto(GuildId, "Aesir", MemberId, "Thor", 0, "", DateTime.UtcNow),
            new GuildRosterRowDto(GuildId, "Aesir", OutsiderId, "Freya", 0, "", DateTime.UtcNow));
        guilds.Seed(SeedGuild(memberCount: 3));

        var service = CreateService(zones, guilds);

        var data = new byte[500];
        new GuildWorkAgmPayload { AvatarName = "Thor", GuildRole = 1 }.Write(data);

        var result = await service.SetAgmRoleAsync(new GuildActionRequest { Sort = 9, Data = data }, masterState,
            CancellationToken.None);
        Respond(masterSession, result);

        AssertGuildActionResponse(ZoneTestKit.DrainOutbound(masterPipe), 9);
        AssertGuildActionResponse(ZoneTestKit.DrainOutbound(bystanderPipe), 9);
    }

    [Fact]
    public async Task Title_Success_BroadcastsGuildInfo_ToOnlineMemberInAnotherZone()
    {
        var (zones, guilds) = CreateWorld();
        var (masterSession, masterPipe, masterState) = EnterZone(zones, 1, MasterId, "Odin", GuildId, 2);
        var (_, memberPipe, _) = EnterZone(zones, 2, MemberId, "Thor", GuildId, 0);

        guilds.SeedRoster(GuildId,
            new GuildRosterRowDto(GuildId, "Aesir", MasterId, "Odin", 2, "", DateTime.UtcNow),
            new GuildRosterRowDto(GuildId, "Aesir", MemberId, "Thor", 0, "", DateTime.UtcNow));
        guilds.Seed(SeedGuild(memberCount: 2));

        var service = CreateService(zones, guilds);

        var data = new byte[500];
        new GuildWorkTitlePayload { AvatarName = "Thor", CallName = "Duke" }.Write(data);

        var result = await service.SetMemberTitleAsync(new GuildActionRequest { Sort = 10, Data = data }, masterState,
            CancellationToken.None);
        Respond(masterSession, result);

        AssertGuildActionResponse(ZoneTestKit.DrainOutbound(masterPipe), 10);
        AssertGuildActionResponse(ZoneTestKit.DrainOutbound(memberPipe), 10);
    }

    [Fact]
    public async Task Buff_Activation_StampsFreshCheckpoint_AndBroadcastsToOnlineMember()
    {
        var (zones, guilds) = CreateWorld();
        var (masterSession, masterPipe, masterState) = EnterZone(zones, 1, MasterId, "Odin", GuildId, 2);
        var (_, memberPipe, _) = EnterZone(zones, 2, MemberId, "Thor", GuildId, 0);

        guilds.Seed(SeedGuild(memberCount: 2, buffTime: 60, buffTimeForDiff: 0));

        var service = CreateService(zones, guilds);

        var data = new byte[500];
        new GuildWorkBuffPayload { GuildBuffType = 2 }.Write(data);

        var before = DateTime.UtcNow.Ticks;
        var result = await service.SetGuildBuffAsync(new GuildActionRequest { Sort = 14, Data = data }, masterState,
            CancellationToken.None);
        Respond(masterSession, result);

        Assert.NotNull(guilds.LastSetBuff);
        var (setGuildId, buffType, buffState, buffTime, buffTimeForDiff) = guilds.LastSetBuff!.Value;
        Assert.Equal(GuildId, setGuildId);
        Assert.Equal(2, buffType);
        Assert.Equal(1, buffState);
        Assert.Equal(60, buffTime);
        Assert.True(buffTimeForDiff >= before, "Activation must stamp a fresh checkpoint, not carry the stale 0.");

        AssertGuildActionResponse(ZoneTestKit.DrainOutbound(masterPipe), 14);
        AssertGuildActionResponse(ZoneTestKit.DrainOutbound(memberPipe), 14);
    }

    private static GuildActionService CreateService(ZoneRegistry zones, FakeGuildRepository guilds)
    {
        return new GuildActionService(zones, guilds, new FakeCharacterRepository(), new GuildInviteRegistry(),
            NullLogger<GuildActionService>.Instance);
    }

    /// <summary>Mirrors <c>GuildActionHandler.Respond</c> -- the actor's own reply is handler-owned plumbing.</summary>
    private static void Respond(ZoneClientSession session, GuildActionResult result)
    {
        if (result.Abort)
        {
            session.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new GuildActionResponse { Result = result.Result, Sort = result.Sort, GuildInfo = result.GuildInfo });
    }

    private static GuildSummaryDto SeedGuild(int memberCount, int buffTime = 0, long buffTimeForDiff = 0)
    {
        return new GuildSummaryDto(GuildId, "Aesir", Grade: 1, MasterCharacterId: MasterId, Points: 0,
            BuffType: 0, BuffState: 0, BuffTime: buffTime, BuffTimeForDiff: buffTimeForDiff, Logo: 0,
            CreatedAtUtc: DateTime.UtcNow, MemberCount: memberCount);
    }

    private static (ZoneRegistry Zones, FakeGuildRepository Guilds) CreateWorld()
    {
        var options = ZoneTestKit.Options();
        var registry = new ZoneRegistry(Options.Create(options), new MovementRules(Options.Create(options)),
            new DirtyTracker<int>(), NullLogger<Zone>.Instance, ZoneTestKit.EmptyWorldData(), []);
        registry.Initialize([1, 2]);

        return (registry, new FakeGuildRepository());
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) EnterZone(
        ZoneRegistry zones, short mapId, int characterId, string name, int? guildId, byte guildRoleDb)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        // Spread far apart (AoiCellSize defaults to 75) so two characters sharing a zone never see each other's
        // own spawn as an AOI broadcast -- that noise would otherwise land in a pipe already drained below,
        // right before this test's actual assertions run.
        var zone = zones[mapId];
        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, mapId, name: name, posX: characterId * 10_000f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;
        zone.TryGetPlayer(characterId, out var state);
        state!.GuildId = guildId;
        state.GuildRoleDb = guildRoleDb;
        state.GuildName = guildId is null ? "" : "Aesir";

        return (session, pipe, state);
    }

    private static void AssertGuildActionResponse(byte[] frame, int expectedSort)
    {
        Assert.Equal(FrameWriter.FrameSizeOf<GuildActionResponse>(), frame.Length);
        var payload = frame.AsSpan(1);
        Assert.Equal(expectedSort, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
    }
}

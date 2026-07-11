using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Guilds;

public class GuildBuffExpiryZoneCommandTests
{
    private static readonly int StateFlagFrame = FrameWriter.FrameSizeOf<AvatarStateFlagResponse>();

    [Fact]
    public void ApplyGuildBuffExpiryCommand_MatchingMember_FlipsBothFields_AndSendsNotification()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, pipe, state) = Enter(zone, 10, 5);
        state.GuildBuffActive = true;
        state.GuildBuffActiveMirror = true;

        zone.PostGuildBuffExpiryCommand(new GuildBuffExpiryZoneCommand(5, 0));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.False(player!.GuildBuffActive);
        Assert.False(player.GuildBuffActiveMirror);

        Assert.Equal(StateFlagFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void ApplyGuildBuffExpiryCommand_NonMatchingGuild_LeavesFieldsUntouched_NoNotification()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, pipe, state) = Enter(zone, 10, 5);
        state.GuildBuffActive = true;
        state.GuildBuffActiveMirror = true;

        zone.PostGuildBuffExpiryCommand(new GuildBuffExpiryZoneCommand(6, 0));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.True(player!.GuildBuffActive);
        Assert.True(player.GuildBuffActiveMirror);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void ApplyGuildBuffExpiryCommand_MidZoneTransfer_StillFlipsFields_ButSendsNoNotification()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, pipe, state) = Enter(zone, 10, 5);
        state.GuildBuffActive = true;
        state.GuildBuffActiveMirror = true;
        state.IsMovingZone = true;

        zone.PostGuildBuffExpiryCommand(new GuildBuffExpiryZoneCommand(5, 0));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.False(player!.GuildBuffActive);
        Assert.False(player.GuildBuffActiveMirror);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void ApplyGuildBuffExpiryCommand_MultipleMatchingMembers_EachIndependentlyNotified()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, pipeA, stateA) = Enter(zone, 10, 5);
        var (_, pipeB, stateB) = Enter(zone, 20, 5);
        stateA.GuildBuffActive = true;
        stateB.GuildBuffActive = true;
        ZoneTestKit.DrainOutbound(pipeA);
        ZoneTestKit.DrainOutbound(pipeB);

        zone.PostGuildBuffExpiryCommand(new GuildBuffExpiryZoneCommand(5, 0));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var playerA));
        Assert.True(zone.TryGetPlayer(20, out var playerB));
        Assert.False(playerA!.GuildBuffActive);
        Assert.False(playerB!.GuildBuffActive);
        Assert.Equal(StateFlagFrame, ZoneTestKit.DrainOutbound(pipeA).Length);
        Assert.Equal(StateFlagFrame, ZoneTestKit.DrainOutbound(pipeB).Length);
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Enter(Zone zone,
        int characterId, int guildId)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, zone.MapId, posX: characterId * 10_000f)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;
        zone.TryGetPlayer(characterId, out var state);
        state!.GuildId = guildId;

        return (session, pipe, state);
    }
}

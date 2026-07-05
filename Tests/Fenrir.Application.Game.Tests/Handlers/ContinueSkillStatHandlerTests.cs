using Fenrir.Application.Game.Handlers;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Framing;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>Drives the real <see cref="ContinueSkillStatHandler" /> (opcode 94) over a real <see cref="Zone" />.</summary>
public class ContinueSkillStatHandlerTests
{
    private static readonly int RegisterFrame = FrameWriter.FrameSizeOf<AutoBuffRegisterResponse>();

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        int characterId)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, zone.MapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;

        zone.TryGetPlayer(characterId, out var state);
        return (session, pipe, state!);
    }

    private static int[] Flat(params (int SkillId, int Grade)[] slots)
    {
        var flat = new int[16];
        for (var i = 0; i < slots.Length; i++)
        {
            flat[i * 2] = slots[i].SkillId;
            flat[i * 2 + 1] = slots[i].Grade;
        }

        return flat;
    }

    [Fact]
    public void Register_ClampsToLearnedGrade_MirrorsToAutoBuffSkillAndReplies()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        state.LearnedSkills = state.LearnedSkills.Add(0, new LearnedSkill(100, 3));
        var handler = new ContinueSkillStatHandler();

        handler.Handle(new ContinueSkillStatRequest { Skill = Flat((100, 10), (200, 5)) }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal((100, 3), player!.AutoBuffSkill[0]);
        Assert.Equal((200, -1), player.AutoBuffSkill[1]);

        Assert.Equal(RegisterFrame, ZoneTestKit.DrainOutbound(pipe).Length);
    }

    [Fact]
    public void Register_RequestedGradeAtOrBelowLearned_KeptAsRequested()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _, state) = Setup(zone, 10);
        state.LearnedSkills = state.LearnedSkills.Add(0, new LearnedSkill(100, 5));
        var handler = new ContinueSkillStatHandler();

        handler.Handle(new ContinueSkillStatRequest { Skill = Flat((100, 2)) }, session);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal((100, 2), player!.AutoBuffSkill[0]);
    }
}

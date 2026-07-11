using System.Buffers.Binary;
using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneLevelMilestoneBonusTests
{
    private const int CharacterId = 10;

    private static int StateFlagFrame => FrameWriter.FrameSizeOf<AvatarStateFlagResponse>();

    private static int StatUpdateFrame => FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>();

    private static FrozenDictionary<short, LevelRowDto> TestLevels()
    {
        var dict = new Dictionary<short, LevelRowDto>
        {
            [1] = new(1, 0, 99, 0, 0, 0, 0, 0, 0, 100, 100),
            [10] = new(10, 900, 999, 0, 0, 0, 0, 0, 0, 200, 150),
            [11] = new(11, 1000, 1099, 0, 0, 0, 0, 0, 0, 220, 160),
            [44] = new(44, 2000, 2099, 0, 0, 0, 0, 0, 0, 400, 250),
            [45] = new(45, 2100, 2199, 5, 0, 0, 0, 0, 0, 500, 300),
            [65] = new(65, 2200, 2299, 0, 0, 0, 0, 0, 0, 700, 450)
        };
        return dict.ToFrozenDictionary();
    }

    private static (Zone Zone, FakeDuplexPipe Pipe, int CharacterId) SetUpKillerAt(short level, long experience)
    {
        var zone = ZoneTestKit.CreateZone(1, worldData: ZoneTestKit.EmptyWorldData(levelsByLevel: TestLevels()));

        var (session, pipe) = ZoneTestKit.CreateSession(CharacterId);
        var enterData = new PlayerEnterData(
            session, "Killer", 1, 0, 2, 3, level,
            1, 0f, 0f, 0f, 0f,
            1, 1, 1, 1, 1,
            Experience: experience);
        zone.Post(ZoneCommand.Enter(CharacterId, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        return (zone, pipe, CharacterId);
    }

        private static (int Sort, int Value01, int Value02, int Value03) ReadStateFlagFrame(byte[] buffer, int index)
    {
        var offset = index * StateFlagFrame + 1;
        var body = buffer.AsSpan(offset);
        return (
            BinaryPrimitives.ReadInt32LittleEndian(body[8..]),
            BinaryPrimitives.ReadInt32LittleEndian(body[12..]),
            BinaryPrimitives.ReadInt32LittleEndian(body[16..]),
            BinaryPrimitives.ReadInt32LittleEndian(body[20..]));
    }

    [Fact]
    public void CrossingAnArmableMilestone_ArmsBonusItemFields_AndBroadcastsSort107()
    {
        var (zone, pipe, killerId) = SetUpKillerAt(44, 2050);

        zone.GrantMonsterKillExperience(killerId, 44, 300);

        zone.TryGetPlayer(killerId, out var killer);
        Assert.NotNull(killer);
        Assert.Equal(45, killer!.Level);
        Assert.Equal(45, killer.BonusItemLevel);
        Assert.True(killer.BonusItemValue);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        var bonusFrame = ReadStateFlagFrame(frame, 2);
        Assert.Equal(107, bonusFrame.Sort);
        Assert.Equal(1, bonusFrame.Value01);
        Assert.Equal(45, bonusFrame.Value02);
        Assert.Equal(0, bonusFrame.Value03);
    }

    [Fact]
    public void LevelUpThatDoesNotCrossAMilestone_LeavesBonusItemFieldsUntouched_AndSendsNoSort107()
    {
        var (zone, pipe, killerId) = SetUpKillerAt(10, 990);

        zone.GrantMonsterKillExperience(killerId, 10, 90);

        zone.TryGetPlayer(killerId, out var killer);
        Assert.NotNull(killer);
        Assert.Equal(11, killer!.Level);
        Assert.Equal(0, killer.BonusItemLevel);
        Assert.False(killer.BonusItemValue);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        Assert.Equal(2 * StateFlagFrame + StatUpdateFrame, frame.Length);
    }

    [Fact]
    public void SecondMilestoneCrossing_OverwritesAnAlreadyArmedButUnclaimedMilestone()
    {
        var (zone, pipe, killerId) = SetUpKillerAt(44, 2050);

        zone.GrantMonsterKillExperience(killerId, 44, 300);
        zone.TryGetPlayer(killerId, out var afterFirst);
        Assert.Equal(45, afterFirst!.BonusItemLevel);
        ZoneTestKit.DrainOutbound(pipe);

        zone.GrantMonsterKillExperience(killerId, 45, 300);

        zone.TryGetPlayer(killerId, out var afterSecond);
        Assert.Equal(65, afterSecond!.Level);
        Assert.Equal(65, afterSecond.BonusItemLevel);
        Assert.True(afterSecond.BonusItemValue);
    }
}

using System.Buffers.Binary;
using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the level-milestone bonus-item ARM step <see cref="Zone.GrantMonsterKillExperience" /> now applies
///     inside its own level-up cascade (<c>Zone.ApplyCharacterExperienceGain</c>, Sort=107 on
///     <see cref="AvatarStateFlagResponse" />) -- the wiring gap workstream C19 originally left open:
///     <see cref="LevelMilestoneBonus" /> existed with no production caller. Pure resolution-table
///     coverage (which levels are armable, the highest-wins-in-one-jump rule) already lives in
///     <c>LevelMilestoneBonusTests</c>; this covers only the live wiring into <see cref="PlayerRuntimeState" />
///     and the AOI broadcast.
/// </summary>
public class ZoneLevelMilestoneBonusTests
{
    private const int CharacterId = 10;

    private static int StateFlagFrame => FrameWriter.FrameSizeOf<AvatarStateFlagResponse>();

    // ApplyCharacterExperienceGain always sends this once at the very end, whether or not a level crossed
    // (the Exp1 total) -- accounted for below so the "no Sort=107" assertion checks total frame count exactly
    // rather than merely a lower bound.
    private static int StatUpdateFrame => FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>();

    // [1] is ReturnLevel's own below-floor row. [10]/[11] give a small, non-milestone level-up (parity check
    // with ZoneLevelUpTests' own scenario). [44]/[45]/[65] are spaced with deliberately unpopulated levels in
    // between (46-64) -- ReturnLevel's linear scan simply skips missing rows, so a single kill's XP gain can
    // land directly in a later row's own band without every intervening level needing a row.
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
        ZoneTestKit.DrainOutbound(pipe); // the Enter flow's own join broadcast, not under test

        return (zone, pipe, CharacterId);
    }

    /// <summary>Reads one AvatarStateFlagResponse frame's Sort/Value01/Value02/Value03 at a given frame index.</summary>
    private static (int Sort, int Value01, int Value02, int Value03) ReadStateFlagFrame(byte[] buffer, int index)
    {
        var offset = index * StateFlagFrame + 1; // skip every preceding frame's own header, plus this one's
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

        // Same-level kill (favorable/unfavorable gap 0): rawGain = 300 * 1.0 = 300, /3 (level 44 < 113) = 100.
        // 2050 + 100 = 2150, inside level 45's own [2100,2199] band -> crosses exactly one milestone (45).
        zone.GrantMonsterKillExperience(killerId, 44, 300);

        zone.TryGetPlayer(killerId, out var killer);
        Assert.NotNull(killer);
        Assert.Equal(45, killer!.Level);
        Assert.Equal(45, killer.BonusItemLevel);
        Assert.True(killer.BonusItemValue);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        // Two duplicate Sort=1 (level-up) sends, then one Sort=107 (bonus-item arm) send -- all three are the
        // AOI-broadcast-to-self-plus-neighbors idiom, no queued command/tick needed to observe them.
        var bonusFrame = ReadStateFlagFrame(frame, 2);
        Assert.Equal(107, bonusFrame.Sort);
        Assert.Equal(1, bonusFrame.Value01); // armed flag
        Assert.Equal(45, bonusFrame.Value02); // armed milestone level
        Assert.Equal(0, bonusFrame.Value03);
    }

    [Fact]
    public void LevelUpThatDoesNotCrossAMilestone_LeavesBonusItemFieldsUntouched_AndSendsNoSort107()
    {
        var (zone, pipe, killerId) = SetUpKillerAt(10, 990);

        // Mirrors ZoneLevelUpTests' own scenario: raw 90 * 1.0 = 90, /3 = 30 -> 990+30=1020 (level 11). No
        // armable milestone lies strictly between 10 and 11.
        zone.GrantMonsterKillExperience(killerId, 10, 90);

        zone.TryGetPlayer(killerId, out var killer);
        Assert.NotNull(killer);
        Assert.Equal(11, killer!.Level);
        Assert.Equal(0, killer.BonusItemLevel);
        Assert.False(killer.BonusItemValue);

        var frame = ZoneTestKit.DrainOutbound(pipe);
        // The two duplicate Sort=1 level-up sends plus the unconditional trailing Exp1 AvatarStatUpdateResponse
        // -- no third (Sort=107) AvatarStateFlagResponse frame in between.
        Assert.Equal(2 * StateFlagFrame + StatUpdateFrame, frame.Length);
    }

    [Fact]
    public void SecondMilestoneCrossing_OverwritesAnAlreadyArmedButUnclaimedMilestone()
    {
        var (zone, pipe, killerId) = SetUpKillerAt(44, 2050);

        zone.GrantMonsterKillExperience(killerId, 44, 300); // arms 45, as above
        zone.TryGetPlayer(killerId, out var afterFirst);
        Assert.Equal(45, afterFirst!.BonusItemLevel);
        ZoneTestKit.DrainOutbound(pipe);

        // Second kill, still at (now) level 45: same math lands at 2250, inside level 65's [2200,2299] band --
        // crosses milestone 65 without the player ever claiming the still-armed 45 in between. The single
        // BonusItemLevel field is unconditionally overwritten -- "most recent milestone wins," matching
        // LevelMilestoneBonus's own documented contract.
        zone.GrantMonsterKillExperience(killerId, 45, 300);

        zone.TryGetPlayer(killerId, out var afterSecond);
        Assert.Equal(65, afterSecond!.Level);
        Assert.Equal(65, afterSecond.BonusItemLevel);
        Assert.True(afterSecond.BonusItemValue);
    }
}

using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Progression;

public class HighLevelExperienceOutcomeApplierTests
{
    private static (Zone Zone, int CharacterId) SetUpCharacter(long experience = 0, short level2 = 0,
        int exp2 = 0, int statPoints = 0, int skillPoints = 0)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        var enterData = new PlayerEnterData(
            session, "Ascended", 1, 0, 2, 3, 145,
            1, 0f, 0f, 0f, 0f,
            1, 1, 1, 1, 1,
            Experience: experience, Level2: level2, Exp2: exp2, StatPoints: statPoints);
        zone.Post(ZoneCommand.Enter(1, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.TryGetPlayer(1, out var state);
        state!.SkillPoints = skillPoints;
        return (zone, 1);
    }

    [Fact]
    public void Apply_None_MutatesNothing()
    {
        var (zone, id) = SetUpCharacter(555, 3, 777, 10, 20);
        zone.TryGetPlayer(id, out var target);

        HighLevelExperienceOutcomeApplier.Apply(target!, HighLevelExperienceOutcome.None);

        Assert.Equal(555, target!.Experience);
        Assert.Equal(3, target.Level2);
        Assert.Equal(777, target.Exp2);
        Assert.Equal(10, target.StatPoints);
        Assert.Equal(20, target.SkillPoints);
    }

    [Fact]
    public void Apply_MainPoolFill_SetsExperienceAndAddsStatPoints()
    {
        var (zone, id) = SetUpCharacter(statPoints: 5);
        zone.TryGetPlayer(id, out var target);
        var outcome = HighLevelExperienceOutcome.MainPoolFill(1_950_000_000, 7);

        HighLevelExperienceOutcomeApplier.Apply(target!, outcome);

        Assert.Equal(1_950_000_000, target!.Experience);
        Assert.Equal(12, target.StatPoints);
    }

    [Fact]
    public void Apply_RebirthTierLevelUp_SetsLevel2Exp2AndAddsSkillPoints()
    {
        var (zone, id) = SetUpCharacter(level2: 3, exp2: 999_999_999, skillPoints: 40);
        zone.TryGetPlayer(id, out var target);
        var outcome = HighLevelExperienceOutcome.LevelUp(4, 100, 0);

        HighLevelExperienceOutcomeApplier.Apply(target!, outcome);

        Assert.Equal(4, target!.Level2);
        Assert.Equal(0, target.Exp2);
        Assert.Equal(140, target.SkillPoints);
    }

    [Fact]
    public void Apply_RebirthTierLevelUp_ZeroToOne_AddsZone101TimeBonus()
    {
        var (zone, id) = SetUpCharacter(level2: 0, exp2: 0);
        zone.TryGetPlayer(id, out var target);
        target!.Zone101Time = 500;
        var outcome = HighLevelExperienceOutcome.LevelUp(1, 100,
            HighLevelExperienceResolver.Zone101TimeGrantOnFirstRebirthTier);

        HighLevelExperienceOutcomeApplier.Apply(target, outcome);

        Assert.Equal(500 + HighLevelExperienceResolver.Zone101TimeGrantOnFirstRebirthTier, target.Zone101Time);
    }

    [Fact]
    public void Apply_RebirthTierLevelUp_MidTier_NoZone101TimeMutation()
    {
        var (zone, id) = SetUpCharacter(level2: 1, exp2: 0);
        zone.TryGetPlayer(id, out var target);
        target!.Zone101Time = 500;
        var outcome = HighLevelExperienceOutcome.LevelUp(2, 100, 0);

        HighLevelExperienceOutcomeApplier.Apply(target, outcome);

        Assert.Equal(500, target.Zone101Time);
    }

    [Fact]
    public void Apply_RebirthTierAccrual_SetsExp2AndAddsStatPoints()
    {
        var (zone, id) = SetUpCharacter(level2: 2, exp2: 100_000_000, statPoints: 15);
        zone.TryGetPlayer(id, out var target);
        var outcome = HighLevelExperienceOutcome.Accrual(200_000_000, 10);

        HighLevelExperienceOutcomeApplier.Apply(target!, outcome);

        Assert.Equal(200_000_000, target!.Exp2);
        Assert.Equal(25, target.StatPoints);

        Assert.Equal(2, target.Level2);
    }
}

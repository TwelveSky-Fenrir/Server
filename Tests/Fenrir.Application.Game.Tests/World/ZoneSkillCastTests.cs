using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <c>Zone.ApplySkillCast</c> end-to-end (report 12 §4.2: <c>AVATAR_ACTION_SEND</c> Sort=30) via the
///     SAME unified action wire <c>AvatarActionHandler</c> forwards for every action, including movement.
/// </summary>
public class ZoneSkillCastTests
{
    private static SkillDefinition HolyShieldSkill(byte maxUpgradePoint, short manaUse, byte shieldPercent,
        short runTime)
    {
        var row = new SkillRowDto(82, "Holy Shield", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var grade0 = new SkillGradeRowDto(82, 0, manaUse, 0, 0, 0, 0, 0, 0, 0, 0, runTime, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            shieldPercent, 0, 0, 0, 0, 0);
        var grade1 = new SkillGradeRowDto(82, 1, manaUse, 0, 0, 0, 0, 0, 0, 0, 0, runTime, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            shieldPercent, 0, 0, 0, 0, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static SkillDefinition HealLifeSkill(byte maxUpgradePoint, short manaUse, byte healAmount)
    {
        var row = new SkillRowDto(106, "Heal", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var grade0 = new SkillGradeRowDto(106, 0, manaUse, healAmount, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0);
        var grade1 = new SkillGradeRowDto(106, 1, manaUse, healAmount, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static ActionInfo SkillCastAction(int skillNumber, int gradeNum1, int targetCharacterId = 0)
    {
        return new ActionInfo
        {
            Type = 0, Sort = 30, Frame = 0,
            Location = [100, 0, 100], TargetLocation = [100, 0, 100],
            Front = 0, TargetFront = 0,
            PetLocation = new float[3], PetTargetLocation = new float[3], PetFront = 0, PetSort = 0,
            TargetObjectSort = 0, TargetObjectIndex = targetCharacterId,
            TargetObjectUniqueNumber = unchecked((int)(uint)targetCharacterId),
            SkillNumber = skillNumber, SkillGradeNum1 = gradeNum1, SkillGradeNum2 = 0, SkillValue = 0
        };
    }

    [Fact]
    public void HolyShieldCast_ConsumesManaAndWritesBuffSlot9()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 30, 20, 40)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var manaBefore = state!.Mana; // 300 (EnterData default), MaxLife=840

        zone.Post(ZoneCommand.Move(10, SkillCastAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Equal(168, state.Buffs.Buff[9 * 2]); // 20% of MaxLife(840) = 168
        Assert.Equal(40, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void InsufficientMana_CastFails_NoBuffWritten()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 9999, 20, 40)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var manaBefore = state!.Mana;

        zone.Post(ZoneCommand.Move(10, SkillCastAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
    }

    [Fact]
    public void SecondCastWithinSameLegacyTick_IsRejectedByTheAntiFloodGate()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 10, 20, 40)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var manaBefore = state!.Mana;

        zone.Post(ZoneCommand.Move(10, SkillCastAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // first cast succeeds: -10 mana

        zone.Post(ZoneCommand.Move(10, SkillCastAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // still well within the same 500ms legacy tick window

        Assert.Equal(manaBefore - 10, state.Mana); // only ONE cast's worth of mana consumed
    }

    [Fact]
    public void TargetedHeal_RestoresTargetLifeClampedToMax()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [106] = HealLifeSkill(10, 5, 100)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (healerSession, _) = ZoneTestKit.CreateSession(1);
        var (targetSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(healerSession, 1, "Healer")));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(targetSession, 1, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(20, out var target));
        target!.Life = 700; // MaxLife=840 -> 140 of headroom, less than the 100 flat heal

        zone.Post(ZoneCommand.Move(10, SkillCastAction(106, 10, 20)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(800, target.Life);
    }

    [Fact]
    public void TargetedHeal_ClampsToMaxLife()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [106] = HealLifeSkill(10, 5, 200)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (healerSession, _) = ZoneTestKit.CreateSession(1);
        var (targetSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(healerSession, 1, "Healer")));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(targetSession, 1, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(20, out var target));
        target!.Life = 800; // MaxLife=840 -> only 40 of headroom, less than the 200 flat heal

        zone.Post(ZoneCommand.Move(10, SkillCastAction(106, 10, 20)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(840, target.Life);
    }
}

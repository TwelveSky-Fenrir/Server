using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Simulation;

public class AutoHuntTickSystemTests
{
    private static (Zone Zone, PlayerRuntimeState State) SetUp(
        FrozenDictionary<int, SkillDefinition>? skillsById = null,
        FrozenDictionary<int, ItemDefinition>? itemsById = null,
        short mapId = 1,
        GameServerOptions? options = null)
    {
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById, itemsById: itemsById);
        var dirtyTracker = new DirtyTracker<int>();
        var opts = options ?? ZoneTestKit.Options();
        var optionsWrapper = Options.Create(opts);
        var zone = ZoneTestKit.CreateZone(mapId, opts, dirtyTracker,
            [new AutoHuntTickSystem(worldData, dirtyTracker, optionsWrapper)],
            worldData);

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, mapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!);
    }

        private static AutoHunt Config(params int[] buffStorePairs)
    {
        var buffStore = new int[16];
        Array.Copy(buffStorePairs, buffStore, buffStorePairs.Length);
        return new AutoHunt
        {
            BuffType = 0, BuffStore = buffStore, HuntType = 0, AttackType = new int[4],
            MonNum = 0, ItemType = 0, InvenCmd = 0, DeathCmd = 0, AnimalPreyCmd = 0, AnimalFoodCmd = 0
        };
    }

    private static void Equip(Zone zone, int characterId, int itemId)
    {
        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(ContainerMatrix.Equipment,
            ImmutableDictionary<byte, ItemStack>.Empty.SetItem(7,
                new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0))));
        zone.PostInventoryCommand(new InventoryZoneCommand(characterId, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static SkillDefinition HolyShieldSkill(byte maxUpgradePoint, short manaUse, byte shieldPercent,
        short runTime)
    {
        var row = new SkillRowDto(82, "Holy Shield", 0, 0, 0,
            0, 0, 1, maxUpgradePoint, 1,
            0);
        var grade0 = HolyShieldGrade(0, manaUse, shieldPercent, runTime);
        var grade1 = HolyShieldGrade(1, manaUse, shieldPercent, runTime);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static SkillGradeRowDto HolyShieldGrade(byte gradeIndex, short manaUse, byte shieldPercent,
        short runTime)
    {
        return new SkillGradeRowDto(82, gradeIndex, manaUse, 0,
            0, 0, 0, 0, 0, 0,
            0, runTime, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, shieldPercent, 0, 0, 0,
            0, 0);
    }

    private static SkillDefinition CriticalSkill(byte maxUpgradePoint, short manaUse, byte criticalUp, short runTime)
    {
        var row = new SkillRowDto(83, "Critical", 0, 0, 0,
            0, 0, 1, maxUpgradePoint, 1,
            0);
        var grade0 = CriticalGrade(0, manaUse, criticalUp, runTime);
        var grade1 = CriticalGrade(1, manaUse, criticalUp, runTime);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static SkillGradeRowDto CriticalGrade(byte gradeIndex, short manaUse, byte criticalUp, short runTime)
    {
        return new SkillGradeRowDto(83, gradeIndex, manaUse, 0,
            0, 0, 0, 0, 0, 0,
            0, runTime, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, criticalUp, 0,
            0, 0);
    }

    private static SkillDefinition DamageSkill(byte maxUpgradePoint, short manaUse, byte attackPowerUp,
        short runTime)
    {
        var row = new SkillRowDto(15, "Damage", 0, 0, 0,
            0, 0, 1, maxUpgradePoint, 1,
            0);
        var grade0 = DamageGrade(0, manaUse, attackPowerUp, runTime);
        var grade1 = DamageGrade(1, manaUse, attackPowerUp, runTime);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static SkillGradeRowDto DamageGrade(byte gradeIndex, short manaUse, byte attackPowerUp, short runTime)
    {
        return new SkillGradeRowDto(15, gradeIndex, manaUse, 0,
            0, 0, 0, 0, 0, 0,
            0, runTime, 0, attackPowerUp, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            0);
    }

    [Fact]
    public void Enabled_CastsFirstEligibleConfiguredBuff()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Equal(168, state.Buffs.Buff[9 * 2]);
        Assert.Equal(40, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void Disabled_DoesNothingEvenWithAConfiguredBuff()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = false;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
    }

    [Fact]
    public void EnabledWithNoConfig_DoesNothing()
    {
        var (zone, state) = SetUp();
        var manaBefore = state.Mana;
        state.AutoHuntEnabled = true;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
    }

    [Fact]
    public void AlreadyActiveBuff_IsNotRecast()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));
        state.Buffs.Buff[9 * 2] = 100;
        state.Buffs.Buff[9 * 2 + 1] = 5;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(100, state.Buffs.Buff[9 * 2]);
        Assert.Equal(5, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void ChargeSkill_IsNeverAutoCast_EvenThoughItIsAnOrdinarySelfBuffForManualCasts()
    {
        var (zone, state) = SetUp();
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(6, 5);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(6, 5));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[8 * 2]);
    }

    [Fact]
    public void WrongWeaponClass_SkipsCast()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [9001] = new(WorldDataTestRows.Item(9001) with { Sort = 99 }, [])
        }.ToFrozenDictionary();
        var skillsById = new Dictionary<int, SkillDefinition> { [15] = DamageSkill(10, 20, 50, 30) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById, itemsById);
        Equip(zone, 10, 9001);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(15, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(15, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[0]);
    }

    [Fact]
    public void CorrectWeaponClass_Casts()
    {
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [9002] = new(WorldDataTestRows.Item(9002) with { Sort = 14 }, [])
        }.ToFrozenDictionary();
        var skillsById = new Dictionary<int, SkillDefinition> { [15] = DamageSkill(10, 20, 50, 30) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById, itemsById);
        Equip(zone, 10, 9002);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(15, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(15, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore - 20, state.Mana);
        Assert.Equal(50, state.Buffs.Buff[0]);
        Assert.Equal(30, state.Buffs.Buff[1]);
    }

    [Fact]
    public void InsufficientMana_SkipsCast()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 9999, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
    }

    [Fact]
    public void DeadPlayer_DoesNothing()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));
        state.IsDead = true;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
    }

    [Fact]
    public void PshopOpen_DoesNothing()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));
        state.PshopOpen = true;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
    }

        [Fact]
    public void Stunned_DoesNothing()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));
        state.IsStunned = true;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
    }

        [Fact]
    public void RegularWarMap_DoesNothing()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById, mapId: 49);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
    }

        [Fact]
    public void HolyStoneMapArmed_DoesNothing()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var options = ZoneTestKit.Options();
        options.HolyStoneMapId = 38;
        options.HolyStoneWarEnabled = true;
        var (zone, state) = SetUp(skillsById, mapId: 38, options: options);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
    }

        [Fact]
    public void HolyStoneMapIdWithoutArmedFlag_StillCasts()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var options = ZoneTestKit.Options();
        options.HolyStoneMapId = 38;
        options.HolyStoneWarEnabled = false;
        var (zone, state) = SetUp(skillsById, mapId: 38, options: options);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Equal(168, state.Buffs.Buff[9 * 2]);
    }

        [Fact]
    public void Zone241DungeonMap_DoesNothing()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var options = ZoneTestKit.Options();
        options.Zone241DungeonMapIds = new HashSet<short> { 325 };
        var (zone, state) = SetUp(skillsById, mapId: 325, options: options);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
    }

        [Fact]
    public void OrdinaryMap_UnaffectedByZoneServerTypeGate()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById, mapId: 1);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Equal(168, state.Buffs.Buff[9 * 2]);
    }

    [Fact]
    public void OnlyOneBuffCastPerLegacyTick()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 30, 20, 40),
            [83] = CriticalSkill(10, 10, 15, 25)
        }.ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById);

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10, 83, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty
            .Add(0, new LearnedSkill(82, 10)).Add(1, new LearnedSkill(83, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.Buffs.Buff[9 * 2 + 1] > 0);
        Assert.Equal(0, state.Buffs.Buff[10 * 2 + 1]);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.Buffs.Buff[10 * 2 + 1] > 0);
    }

    [Fact]
    public void UnlearnedConfiguredSkill_ResolvesSafelyWithNoBuffEffect()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
        Assert.Equal(0, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void AutoBuffTimeNotActive_OnlyFirstTwoSlotsAreConsidered()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(0, 0, 0, 0, 82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));
        state.AutoBuffTime = 0;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);

        state.AutoBuffTime = 99_991_231;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Equal(168, state.Buffs.Buff[9 * 2]);
    }
}

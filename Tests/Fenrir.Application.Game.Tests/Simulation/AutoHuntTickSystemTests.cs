using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="AutoHuntTickSystem" />: the auto-hunt bot's configured-buff auto-cast loop
///     (<c>AVATAR_OBJECT::BotBuff</c>).
/// </summary>
public class AutoHuntTickSystemTests
{
    private static (Zone Zone, PlayerRuntimeState State) SetUp(
        FrozenDictionary<int, SkillDefinition>? skillsById = null,
        FrozenDictionary<int, ItemDefinition>? itemsById = null)
    {
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById, itemsById: itemsById);
        var dirtyTracker = new DirtyTracker<int>();
        var zone = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker,
            simulationSystems: [new AutoHuntTickSystem(worldData, dirtyTracker)], worldData: worldData);

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!);
    }

    /// <summary>Fills a 16-int BuffStore from (skillId, grade) pairs, front-loaded, the rest left at 0 (empty).</summary>
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

    // Holy Shield (82): no weapon requirement, slot 9, value = shieldPercent% of MaxLife.
    private static SkillDefinition HolyShieldSkill(byte maxUpgradePoint, short manaUse, byte shieldPercent,
        short runTime)
    {
        var row = new SkillRowDto(SkillId: 82, Name: "Holy Shield", Type: 0, AttackType: 0, DataNumber2D: 0,
            TribeInfo1: 0, TribeInfo2: 0, LearnSkillPoint: 1, MaxUpgradePoint: maxUpgradePoint, TotalHitNumber: 1,
            ValidRadius: 0);
        var grade0 = HolyShieldGrade(0, manaUse, shieldPercent, runTime);
        var grade1 = HolyShieldGrade(1, manaUse, shieldPercent, runTime);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static SkillGradeRowDto HolyShieldGrade(byte gradeIndex, short manaUse, byte shieldPercent,
        short runTime)
    {
        return new SkillGradeRowDto(SkillId: 82, GradeIndex: gradeIndex, ManaUse: manaUse, RecoverInfo1: 0,
            RecoverInfo2: 0, StunAttack: 0, StunDefense: 0, FastRunSpeed: 0, AttackInfo1: 0, AttackInfo2: 0,
            AttackInfo3: 0, RunTime: runTime, ChargingDamageUp: 0, AttackPowerUp: 0, DefensePowerUp: 0,
            AttackSuccessUp: 0, AttackBlockUp: 0, ElementAttackUp: 0, ElementDefenseUp: 0, AttackSpeedUp: 0,
            RunSpeedUp: 0, ShieldLifeUp: shieldPercent, LuckUp: 0, CriticalUp: 0, ReturnSuccessUp: 0,
            StunDefenseUp: 0, DestroySuccessUp: 0);
    }

    // Critical (83): no weapon requirement, slot 10.
    private static SkillDefinition CriticalSkill(byte maxUpgradePoint, short manaUse, byte criticalUp, short runTime)
    {
        var row = new SkillRowDto(SkillId: 83, Name: "Critical", Type: 0, AttackType: 0, DataNumber2D: 0,
            TribeInfo1: 0, TribeInfo2: 0, LearnSkillPoint: 1, MaxUpgradePoint: maxUpgradePoint, TotalHitNumber: 1,
            ValidRadius: 0);
        var grade0 = CriticalGrade(0, manaUse, criticalUp, runTime);
        var grade1 = CriticalGrade(1, manaUse, criticalUp, runTime);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static SkillGradeRowDto CriticalGrade(byte gradeIndex, short manaUse, byte criticalUp, short runTime)
    {
        return new SkillGradeRowDto(SkillId: 83, GradeIndex: gradeIndex, ManaUse: manaUse, RecoverInfo1: 0,
            RecoverInfo2: 0, StunAttack: 0, StunDefense: 0, FastRunSpeed: 0, AttackInfo1: 0, AttackInfo2: 0,
            AttackInfo3: 0, RunTime: runTime, ChargingDamageUp: 0, AttackPowerUp: 0, DefensePowerUp: 0,
            AttackSuccessUp: 0, AttackBlockUp: 0, ElementAttackUp: 0, ElementDefenseUp: 0, AttackSpeedUp: 0,
            RunSpeedUp: 0, ShieldLifeUp: 0, LuckUp: 0, CriticalUp: criticalUp, ReturnSuccessUp: 0,
            StunDefenseUp: 0, DestroySuccessUp: 0);
    }

    // Damage (15): requires an equipped weapon of Sort 14/16/20, slot 0.
    private static SkillDefinition DamageSkill(byte maxUpgradePoint, short manaUse, byte attackPowerUp,
        short runTime)
    {
        var row = new SkillRowDto(SkillId: 15, Name: "Damage", Type: 0, AttackType: 0, DataNumber2D: 0,
            TribeInfo1: 0, TribeInfo2: 0, LearnSkillPoint: 1, MaxUpgradePoint: maxUpgradePoint, TotalHitNumber: 1,
            ValidRadius: 0);
        var grade0 = DamageGrade(0, manaUse, attackPowerUp, runTime);
        var grade1 = DamageGrade(1, manaUse, attackPowerUp, runTime);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static SkillGradeRowDto DamageGrade(byte gradeIndex, short manaUse, byte attackPowerUp, short runTime)
    {
        return new SkillGradeRowDto(SkillId: 15, GradeIndex: gradeIndex, ManaUse: manaUse, RecoverInfo1: 0,
            RecoverInfo2: 0, StunAttack: 0, StunDefense: 0, FastRunSpeed: 0, AttackInfo1: 0, AttackInfo2: 0,
            AttackInfo3: 0, RunTime: runTime, ChargingDamageUp: 0, AttackPowerUp: attackPowerUp, DefensePowerUp: 0,
            AttackSuccessUp: 0, AttackBlockUp: 0, ElementAttackUp: 0, ElementDefenseUp: 0, AttackSpeedUp: 0,
            RunSpeedUp: 0, ShieldLifeUp: 0, LuckUp: 0, CriticalUp: 0, ReturnSuccessUp: 0, StunDefenseUp: 0,
            DestroySuccessUp: 0);
    }

    [Fact]
    public void Enabled_CastsFirstEligibleConfiguredBuff()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById: skillsById);
        var manaBefore = state.Mana; // 300 (EnterData default), MaxLife=840

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Equal(168, state.Buffs.Buff[9 * 2]); // 20% of MaxLife(840)
        Assert.Equal(40, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void Disabled_DoesNothingEvenWithAConfiguredBuff()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById: skillsById);
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
        state.AutoHuntEnabled = true; // AutoHuntConfig left null

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
    }

    [Fact]
    public void AlreadyActiveBuff_IsNotRecast()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById: skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));
        state.Buffs.Buff[9 * 2] = 100;
        state.Buffs.Buff[9 * 2 + 1] = 5; // still active

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(100, state.Buffs.Buff[9 * 2]);
        Assert.Equal(5, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void ChargeSkill_IsNeverAutoCast_EvenThoughItIsAnOrdinarySelfBuffForManualCasts()
    {
        // Skill 6 (Charge) deliberately absent from worldData -- the whitelist gate must reject it before any
        // catalog lookup is even attempted.
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
        var (zone, state) = SetUp(skillsById: skillsById, itemsById: itemsById);
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
        var (zone, state) = SetUp(skillsById: skillsById, itemsById: itemsById);
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
        var (zone, state) = SetUp(skillsById: skillsById);
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
        var (zone, state) = SetUp(skillsById: skillsById);
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
        var (zone, state) = SetUp(skillsById: skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));
        state.PshopOpen = true;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
    }

    [Fact]
    public void OnlyOneBuffCastPerLegacyTick()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 30, 20, 40),
            [83] = CriticalSkill(10, 10, 15, 25)
        }.ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById: skillsById);

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10, 83, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty
            .Add(0, new LearnedSkill(82, 10)).Add(1, new LearnedSkill(83, 10));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.Buffs.Buff[9 * 2 + 1] > 0); // Holy Shield (first slot) applied
        Assert.Equal(0, state.Buffs.Buff[10 * 2 + 1]); // Critical (second slot) NOT applied this same tick

        zone.Tick(SimulationClock.LegacyTick); // slot 0 already active -> falls through to slot 1

        Assert.True(state.Buffs.Buff[10 * 2 + 1] > 0);
    }

    [Fact]
    public void UnlearnedConfiguredSkill_ResolvesSafelyWithNoBuffEffect()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById: skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        state.AutoHuntConfig = Config(82, 10);
        // LearnedSkills left empty -- the character never actually learned skill 82.

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana); // grade clamps to -1 -> ManaCost resolves to 0
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
        Assert.Equal(0, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void AutoBuffTimeNotActive_OnlyFirstTwoSlotsAreConsidered()
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [82] = HolyShieldSkill(10, 30, 20, 40) }
            .ToFrozenDictionary();
        var (zone, state) = SetUp(skillsById: skillsById);
        var manaBefore = state.Mana;

        state.AutoHuntEnabled = true;
        // Slot index 2 (the 3rd configured pair) is only reachable once slotCount == 8.
        state.AutoHuntConfig = Config(0, 0, 0, 0, 82, 10);
        state.LearnedSkills = ImmutableDictionary<byte, LearnedSkill>.Empty.Add(0, new LearnedSkill(82, 10));
        state.AutoBuffTime = 0; // < GameDate.Today() -> slotCount == 2

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);

        state.AutoBuffTime = 99_991_231; // >= GameDate.Today() -> slotCount == 8

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Equal(168, state.Buffs.Buff[9 * 2]);
    }
}

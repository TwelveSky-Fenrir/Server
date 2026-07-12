using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneSkillCastTests
{
    private static void SeedSkillHotkey(PlayerRuntimeState state, int skillId, int investedGrade, byte page = 0,
        byte index = 0)
    {
        state.Hotkeys = state.Hotkeys.SetItem((page, index), new HotkeySlot(HotkeyBindingKind.Skill, skillId,
            investedGrade));
        state.LearnedSkills = state.LearnedSkills.SetItem(index, new LearnedSkill(skillId, investedGrade));
    }

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

    private static SkillDefinition HolyShieldSkillGraded(byte maxUpgradePoint,
        (short Mana, byte Shield, short RunTime) grade0, (short Mana, byte Shield, short RunTime) grade1)
    {
        var row = new SkillRowDto(82, "Holy Shield", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var g0 = new SkillGradeRowDto(82, 0, grade0.Mana, 0, 0, 0, 0, 0, 0, 0, 0, grade0.RunTime, 0, 0, 0, 0, 0, 0,
            0, 0, 0, grade0.Shield, 0, 0, 0, 0, 0);
        var g1 = new SkillGradeRowDto(82, 1, grade1.Mana, 0, 0, 0, 0, 0, 0, 0, 0, grade1.RunTime, 0, 0, 0, 0, 0, 0,
            0, 0, 0, grade1.Shield, 0, 0, 0, 0, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [g0, g1]);
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

    private static SkillDefinition HealManaSkill(byte maxUpgradePoint, short manaUse, byte healAmount)
    {
        var row = new SkillRowDto(107, "Mana Heal", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var grade0 = new SkillGradeRowDto(107, 0, manaUse, 0, healAmount, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0);
        var grade1 = new SkillGradeRowDto(107, 1, manaUse, 0, healAmount, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static ActionInfo SkillCastStartAction(int skillNumber, int gradeNum1, int gradeNum2 = 0)
    {
        return new ActionInfo
        {
            Type = 0, Sort = 32, Frame = 0,
            Location = [100, 0, 100], TargetLocation = [100, 0, 100],
            Front = 0, TargetFront = 0,
            PetLocation = new float[3], PetTargetLocation = new float[3], PetFront = 0, PetSort = 0,
            TargetObjectSort = 0, TargetObjectIndex = 0, TargetObjectUniqueNumber = 0,
            SkillNumber = skillNumber, SkillGradeNum1 = gradeNum1, SkillGradeNum2 = gradeNum2, SkillValue = 0
        };
    }

    private static ActionInfo SkillEffectConfirmAction(int skillNumber, int gradeNum1, int targetCharacterId = 0,
        int gradeNum2 = 0)
    {
        return new ActionInfo
        {
            Type = 0, Sort = 1, Frame = 0,
            Location = [100, 0, 100], TargetLocation = [100, 0, 100],
            Front = 0, TargetFront = 0,
            PetLocation = new float[3], PetTargetLocation = new float[3], PetFront = 0, PetSort = 0,
            TargetObjectSort = 0, TargetObjectIndex = targetCharacterId,
            TargetObjectUniqueNumber = unchecked((int)(uint)targetCharacterId),
            SkillNumber = skillNumber, SkillGradeNum1 = gradeNum1, SkillGradeNum2 = gradeNum2, SkillValue = 0
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
        var manaBefore = state!.Mana;
        SeedSkillHotkey(state, 82, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Equal(168, state.Buffs.Buff[9 * 2]);
        Assert.Equal(40, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void SkillCast_ManaUsesInvestedGradeOnly_EffectUsesInvestedPlusItemBonusGrade()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkillGraded(10, (0, 0, 40), (100, 20, 40))
        }.ToFrozenDictionary();
        var bonusItem = new ItemDefinition(WorldDataTestRows.Item(9000),
            [new ItemBonusSkillRowDto(9000, 0, 82, 5)]);
        var itemsById = new Dictionary<int, ItemDefinition> { [9000] = bonusItem }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById, itemsById: itemsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var manaBefore = state!.Mana;
        SeedSkillHotkey(state, 82, 5);
        state.Inventory.ReplaceContainer(ContainerMatrix.Equipment,
            ImmutableDictionary<byte, ItemStack>.Empty.Add(0, new ItemStack(9000, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0)));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 5, 5)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 50, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 5, 0, 5), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 50, state.Mana);
        Assert.Equal(168, state.Buffs.Buff[9 * 2]);
        Assert.Equal(40, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void EffectConfirm_WithoutMatchingCastStart_IsSilentNoOp()
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
        var manaBefore = state!.Mana;

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
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
        SeedSkillHotkey(state, 82, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore, state.Mana);
        Assert.Equal(0, state.Buffs.Buff[9 * 2]);
    }

    [Fact]
    public void RepeatedCastWithinSameLegacyTick_ChargesManaIndependentlyEachTime()
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
        SeedSkillHotkey(state, 82, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 20, state.Mana);
    }

    [Fact]
    public void RepeatedCastWithinSameLegacyTick_SecondChargeSilentlyDroppedWhenManaExhausted()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 200, 20, 40)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var manaBefore = state!.Mana;
        SeedSkillHotkey(state, 82, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 200, state.Mana);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 200, state.Mana);
    }

    [Fact]
    public void HolyShieldOnZone124_SecondApplicationWithinRealTenSeconds_SkipsTheRewrite_ButStillChargesMana()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [82] = HolyShieldSkill(10, 30, 20, 40)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(124, worldData: worldData);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 124)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        var manaBefore = state!.Mana;
        SeedSkillHotkey(state, 82, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 30, state.Mana);
        Assert.Equal(168, state.Buffs.Buff[9 * 2]);
        Assert.Equal(40, state.Buffs.Buff[9 * 2 + 1]);

        state.Buffs.Buff[9 * 2] = -1;
        state.Buffs.Buff[9 * 2 + 1] = -1;

        zone.Tick(TimeSpan.FromMilliseconds(600));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(manaBefore - 60, state.Mana);
        Assert.Equal(-1, state.Buffs.Buff[9 * 2]);
        Assert.Equal(-1, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void HolyShieldOnNonZone124_SecondApplicationInQuickSuccession_StillRewritesTheBuff()
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
        SeedSkillHotkey(state!, 82, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(168, state!.Buffs.Buff[9 * 2]);
        Assert.Equal(40, state.Buffs.Buff[9 * 2 + 1]);

        state.Buffs.Buff[9 * 2] = -1;
        state.Buffs.Buff[9 * 2 + 1] = -1;

        zone.Tick(TimeSpan.FromMilliseconds(600));

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(82, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(82, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.NotEqual(-1, state.Buffs.Buff[9 * 2]);
        Assert.NotEqual(-1, state.Buffs.Buff[9 * 2 + 1]);
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
        target!.Life = 700;
        target.ActionSort = 1;
        Assert.True(zone.TryGetPlayer(10, out var healer));
        SeedSkillHotkey(healer!, 106, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(106, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(106, 10, 20), true));
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
        target!.Life = 800;
        target.ActionSort = 1;
        Assert.True(zone.TryGetPlayer(10, out var healer));
        SeedSkillHotkey(healer!, 106, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(106, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(106, 10, 20), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(840, target.Life);
    }

    [Fact]
    public async Task TargetedHeal_Success_SendsCasterEffectSnapshotButNoTargetPacket()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [106] = HealLifeSkill(10, 5, 100)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (healerSession, healerPipe) = ZoneTestKit.CreateSession(1);
        var (targetSession, targetPipe) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(healerSession, 1, "Healer")));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(targetSession, 1, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(20, out var target));
        target!.Life = 700;
        target.ActionSort = 1;
        Assert.True(zone.TryGetPlayer(10, out var healer));
        SeedSkillHotkey(healer!, 106, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(106, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        ZoneTestKit.DrainOutbound(healerPipe);
        ZoneTestKit.DrainOutbound(targetPipe);

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(106, 10, 20), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(800, target.Life);

        await PacketAssert.AssertSentAsync(healerPipe, new AvatarEffectStateResponse
        {
            ServerIndex = healer!.CharacterId,
            UniqueNumber = healer.UniqueNumber,
            EffectValue = healer.Buffs.Buff,
            EffectValueState = new int[35]
        });

        PacketAssert.AssertNothingSent(targetPipe);
    }

    [Fact]
    public void TargetedHeal_SelfTarget_IsRejected()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [106] = HealLifeSkill(10, 5, 100)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (healerSession, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(healerSession, 1, "Healer")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var healer));
        healer!.Life = 700;
        healer.ActionSort = 1;
        SeedSkillHotkey(healer, 106, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(106, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(106, 10, 10), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(700, healer.Life);
    }

    [Fact]
    public void TargetedHeal_TargetAlreadyAtMaxLife_IsRejected()
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
        target!.Life = target.MaxLife;
        target.ActionSort = 1;
        Assert.True(zone.TryGetPlayer(10, out var healer));
        SeedSkillHotkey(healer!, 106, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(106, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(106, 10, 20), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(target.MaxLife, target.Life);
    }

    [Fact]
    public void TargetedHeal_TargetRunningPersonalShop_IsRejected()
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
        target!.Life = 700;
        target.ActionSort = 1;
        target.PshopOpen = true;
        Assert.True(zone.TryGetPlayer(10, out var healer));
        SeedSkillHotkey(healer!, 106, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(106, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(106, 10, 20), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(700, target.Life);
    }

    [Fact]
    public void TargetedHeal_TargetStunned_IsRejected()
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
        target!.Life = 700;
        target.ActionSort = 1;
        target.IsStunned = true;
        Assert.True(zone.TryGetPlayer(10, out var healer));
        SeedSkillHotkey(healer!, 106, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(106, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(106, 10, 20), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(700, target.Life);
    }

    [Fact]
    public void TargetedHeal_TargetHidden_IsRejected()
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
        target!.Life = 700;
        target.ActionSort = 1;
        target.VisibleState = 0;
        Assert.True(zone.TryGetPlayer(10, out var healer));
        SeedSkillHotkey(healer!, 106, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(106, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(106, 10, 20), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(700, target.Life);
    }

    [Fact]
    public void TargetedHeal_TargetNeverActed_FailsGeneralTargetLegitimacyCheck()
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
        target!.Life = 700;
        Assert.True(zone.TryGetPlayer(10, out var healer));
        SeedSkillHotkey(healer!, 106, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(106, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(106, 10, 20), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(700, target.Life);
    }

    [Fact]
    public async Task TargetedHealMana_Success_RestoresManaAndBroadcastsToTargetAndCaster()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [107] = HealManaSkill(10, 5, 100)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (healerSession, healerPipe) = ZoneTestKit.CreateSession(1);
        var (targetSession, targetPipe) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(healerSession, 1, "Healer")));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(targetSession, 1, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(20, out var target));
        target!.Mana = 200;
        target.ActionSort = 1;
        Assert.True(zone.TryGetPlayer(10, out var healer));
        SeedSkillHotkey(healer!, 107, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(107, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        ZoneTestKit.DrainOutbound(healerPipe);
        ZoneTestKit.DrainOutbound(targetPipe);

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(107, 10, 20), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(300, target.Mana);

        await PacketAssert.AssertSentAsync(healerPipe, new AvatarEffectStateResponse
        {
            ServerIndex = healer!.CharacterId,
            UniqueNumber = healer.UniqueNumber,
            EffectValue = healer.Buffs.Buff,
            EffectValueState = new int[35]
        });

        await PacketAssert.AssertSentAsync(targetPipe,
            new AvatarStatUpdateResponse { Sort = 11, Value = 300, Value2 = 0 });
    }

    [Fact]
    public void TargetedHealMana_ClampsToMaxMana()
    {
        var skillsById = new Dictionary<int, SkillDefinition>
        {
            [107] = HealManaSkill(10, 5, 100)
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (healerSession, _) = ZoneTestKit.CreateSession(1);
        var (targetSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(healerSession, 1, "Healer")));
        zone.Post(ZoneCommand.Enter(20, ZoneTestKit.EnterData(targetSession, 1, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(20, out var target));
        target!.Mana = 300;
        target.ActionSort = 1;
        Assert.True(zone.TryGetPlayer(10, out var healer));
        SeedSkillHotkey(healer!, 107, 10);

        zone.Post(ZoneCommand.Move(10, SkillCastStartAction(107, 10)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Post(ZoneCommand.Move(10, SkillEffectConfirmAction(107, 10, 20), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(320, target.Mana);
    }
}

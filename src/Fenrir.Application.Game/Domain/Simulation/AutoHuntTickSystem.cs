using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Core.Packets.Shared;
using Fenrir.Data.WriteBehind;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class AutoHuntTickSystem(
    WorldDataCache worldData,
    DirtyTracker<int> dirtyTracker,
    IOptions<GameServerOptions> options) : ISimulationSystem
{
    private const int NoManaRelocateThreshold = 1000;

    private const int InventorySlotCount = 64;

    private const byte HotkeyBindableItemSort = 2;

    private const int AutoHuntDayStatSort = 61;

    private const int AutoHuntMinuteStatSort = 62;

    private const int RawTruthGateBuffSlot = 31;

    private static readonly FrozenDictionary<int, ImmutableArray<int>> AutoCastGateSlots =
        new Dictionary<int, ImmutableArray<int>>
        {
            [7] = [4], [26] = [4], [45] = [4],
            [11] = [1], [34] = [1], [49] = [1],
            [15] = [0], [30] = [0], [53] = [0],
            [19] = [3, 7], [38] = [3, 7], [57] = [3, 7],
            [82] = [9, RawTruthGateBuffSlot],
            [83] = [10],
            [84] = [11],
            [103] = [12],
            [104] = [13],
            [105] = [14]
        }.ToFrozenDictionary();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            RunBotUpkeep(zone, state, legacyTicksElapsed);
    }

    private void RunBotUpkeep(Zone zone, PlayerRuntimeState state, int legacyTicksElapsed)
    {
        if (!state.AutoHuntEnabled || state.AutoHuntConfig is not { } config)
            return;

        if (!state.PshopOpen && state.CanIssueGameplayActions && !IsSuppressedByZoneServerType(zone))
        {
            TryAutoCastBuff(zone, state, config);
            TryResupplyHotkeys(state, config);
        }

        AdvanceBudgetAndRelocateIfExhausted(state, legacyTicksElapsed);
    }

    private void TryAutoCastBuff(Zone zone, PlayerRuntimeState state, AutoHunt config)
    {
        if (state.IsMovingZone)
            return;

        if (state.Mana < 1)
            return;

        var slotCount = state.AutoBuffTime >= GameDate.Today() ? 8 : 2;

        if (IsStunOrDeathMotion(state.ActionSort) || IsBroadcastBuffMotion(state.ActionSort))
            return;

        var equipSlotItems = BuildEquipSlotItems(state);
        var weaponItemId = state.Inventory.GetSlot(ContainerMatrix.Equipment, EquipmentSlots.WeaponSlot)?.ItemId;
        var weaponSort = weaponItemId is { } itemId && worldData.ItemsById.TryGetValue(itemId, out var weaponDef)
            ? (int?)weaponDef.Item.Sort
            : null;
        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
        var manaReductionRatioPercent = ManaCostReduction.GetRatioPercent(equipSlotItems);

        var isZone126 = zone.IsZone126TypeZone;

        for (var i = 0; i < slotCount; i++)
        {
            var skillId = config.BuffStore[i * 2];
            var requestedGrade = config.BuffStore[i * 2 + 1];
            if (!worldData.SkillsById.TryGetValue(skillId, out var skillDef) ||
                !AutoBuffSkillResolver.TryResolveOwnedSkill(skillId, requestedGrade, state.LearnedSkills,
                    out var selection))
            {
                ClearRejectedAutoBuffSelection(state, config, i);
                continue;
            }

            var grade = selection.BaseGrade;
            if (requestedGrade != grade)
            {
                config.BuffStore[i * 2 + 1] = grade;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
            }

            if (!AutoCastGateSlots.TryGetValue(skillId, out var gateSlots))
                continue;

            if (IsAlreadyActive(state, gateSlots))
                continue;

            var bonusGrade = SkillGradeAuthority.GetBonusSkillValue(skillId, equipSlotItems, 0, skillDef,
                state.GuildBuffType, state.GuildBuffActive);

            var result = SkillCastResolver.TryCast(skillDef, grade + bonusGrade, int.MaxValue, maxLife, weaponSort,
                state.SupportSkillTimeUpRatio, manaReductionRatioPercent);

            if (!result.Success || result.Kind != SkillEffectKind.SelfBuff)
                continue;

            var manaCost = ResolveManaCost(skillDef, grade, manaReductionRatioPercent);

            if (state.Mana < manaCost && isZone126)
            {
                EscalateNoMana(state);
                return;
            }

            state.NoManaCount = 0;

            if (manaCost > 0)
            {
                state.Mana -= manaCost;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
            }

            zone.ApplyBuffWrites(state, result.BuffWrites);

            var (actionType, actionSort) = ResolveBuffCastMotion(skillId, weaponSort);
            if (actionSort != 0)
                zone.BroadcastAutoHuntBuffCast(state, actionType, actionSort, skillId, grade, bonusGrade);

            return;
        }
    }

    private static int ResolveManaCost(SkillDefinition skill, int gradePoints, int manaReductionRatioPercent)
    {
        var rawCost = (int)SkillCatalog.ReturnSkillValue(skill, gradePoints, SkillValueKind.ManaUse);
        return manaReductionRatioPercent > 0
            ? rawCost - rawCost * manaReductionRatioPercent / 100
            : rawCost;
    }

    private static (int Type, int Sort) ResolveBuffCastMotion(int skillId, int? weaponSort)
    {
        return skillId switch
        {
            7 or 26 or 45 => (0, 41),
            11 or 34 or 49 => weaponSort == 17 ? (5, 61) : (3, 60),
            15 or 30 or 53 => weaponSort == 16 ? (3, 60) : (5, 61),
            19 or 38 or 57 => (7, 62),
            82 => (0, 66),
            83 => (0, 67),
            84 => (0, 68),
            103 or 104 or 105 => (0, 75),
            _ => (0, 0)
        };
    }

    private void TryResupplyHotkeys(PlayerRuntimeState state, AutoHunt config)
    {
        var bound = ScanBoundPotionCategories(state);

        var petStack = state.Inventory.GetSlot(ContainerMatrix.Equipment, PetSlots.EquipmentSlot);
        var petEquipped = petStack is { ItemId: > 0 };
        var animalPreyCmd = config.AnimalPreyCmd != 0;
        var animalFoodCmd = config.AnimalFoodCmd != 0;
        var animalPresent = state.AnimalNumber > 0;

        var needsAnything = (!bound.HasHp && !bound.HasHpMp) ||
                            (!bound.HasMp && !bound.HasHpMp) ||
                            (!bound.HasPetPrey && animalPreyCmd && petEquipped) ||
                            (!bound.HasPetFood && animalFoodCmd && animalPresent);
        if (!needsAnything)
            return;

        var emptyHotkeySlots = CollectEmptyHotkeySlots(state);
        if (emptyHotkeySlots.Count == 0)
            return;

        var candidates = CollectInventoryPillCandidates(state);
        if (candidates.Count == 0)
            return;

        var moves = BotHotKeyResupplyPolicy.Resolve(bound, candidates, emptyHotkeySlots, animalPreyCmd, petEquipped,
            animalFoodCmd, animalPresent);

        foreach (var move in moves)
            ApplyResupplyMove(state, move);
    }

    private static void ApplyResupplyMove(PlayerRuntimeState state, BotHotKeyResupplyPolicy.ResupplyMove move)
    {
        state.Inventory.ReplaceContainer(move.SourcePage,
            state.Inventory.GetContainer(move.SourcePage).Remove(move.SourceSlot));

        state.SetHotkeySlot(move.DestinationPage, move.DestinationIndex,
            new HotkeySlot(HotkeyBindingKind.Item, move.ItemId, move.Quantity));

        state.Session.Send(new AutoHuntHotkeyRebindResponse
        {
            Page1 = move.SourcePage,
            Index1 = move.SourceSlot,
            Page2 = move.DestinationPage,
            Index2 = move.DestinationIndex,
            Value0 = move.ItemId,
            Value1 = move.Quantity,
            Value2 = (int)HotkeyBindingKind.Item
        });
    }

    private BotHotKeyResupplyPolicy.BoundCategories ScanBoundPotionCategories(PlayerRuntimeState state)
    {
        bool hasHp = false, hasMp = false, hasHpMp = false, hasPetPrey = false, hasPetFood = false;

        foreach (var (_, slot) in state.Hotkeys)
        {
            if (slot.Kind != HotkeyBindingKind.Item || slot.Value2 < 1)
                continue;

            if (!worldData.ItemsById.TryGetValue(slot.Value1, out var definition) ||
                definition.Item.Sort != HotkeyBindableItemSort)
                continue;

            switch (definition.Item.PotionType1)
            {
                case 1:
                case 2:
                    hasHp = true;
                    break;
                case 3:
                case 4:
                    hasMp = true;
                    break;
                case 5:
                    hasHpMp = true;
                    break;
                case 6:
                    hasPetPrey = true;
                    break;
                case 16:
                    hasPetFood = true;
                    break;
            }
        }

        return new BotHotKeyResupplyPolicy.BoundCategories(hasHp, hasMp, hasHpMp, hasPetPrey, hasPetFood);
    }

    private static List<BotHotKeyResupplyPolicy.HotkeyAddress> CollectEmptyHotkeySlots(PlayerRuntimeState state)
    {
        var empty = new List<BotHotKeyResupplyPolicy.HotkeyAddress>(4);

        for (byte page = 0; page < HotkeyActionResolver.PageCount; page++)
        for (byte index = 0; index < HotkeyActionResolver.SlotsPerPage; index++)
            if (state.GetHotkeySlot(page, index).Value1 == 0)
                empty.Add(new BotHotKeyResupplyPolicy.HotkeyAddress(page, index));

        return empty;
    }

    private List<BotHotKeyResupplyPolicy.InventoryCandidate> CollectInventoryPillCandidates(PlayerRuntimeState state)
    {
        var candidates = new List<BotHotKeyResupplyPolicy.InventoryCandidate>();

        var pageCount = state.InventoryDate != 0 && state.InventoryDate > GameDate.Today() ? 2 : 1;

        for (byte page = 0; page < pageCount; page++)
        {
            var container = state.Inventory.GetContainer(page);
            for (byte slot = 0; slot < InventorySlotCount; slot++)
            {
                if (!container.TryGetValue(slot, out var stack) || stack.ItemId == 0)
                    continue;

                if (!worldData.ItemsById.TryGetValue(stack.ItemId, out var definition))
                    continue;

                var category = ClassifyInventoryPill(definition.Item.PotionType1);
                if (category == BotHotKeyResupplyPolicy.ResupplyCategory.None)
                    continue;

                candidates.Add(new BotHotKeyResupplyPolicy.InventoryCandidate(page, slot, stack.ItemId,
                    stack.Quantity, category));
            }
        }

        return candidates;
    }

    private static BotHotKeyResupplyPolicy.ResupplyCategory ClassifyInventoryPill(int potionType1)
    {
        return potionType1 switch
        {
            6 => BotHotKeyResupplyPolicy.ResupplyCategory.PetPrey,
            16 => BotHotKeyResupplyPolicy.ResupplyCategory.PetFood,
            _ => BotHotKeyResupplyPolicy.ClassifyHpMpByPotionType(potionType1)
        };
    }

    private ItemDefinition?[] BuildEquipSlotItems(PlayerRuntimeState state)
    {
        var equipSlotItems = new ItemDefinition?[SkillGradeAuthority.EquipSlotCount];
        for (var slot = 0; slot < SkillGradeAuthority.EquipSlotCount; slot++)
            if (state.Inventory.GetSlot(ContainerMatrix.Equipment, (byte)slot) is { } stack &&
                worldData.ItemsById.TryGetValue(stack.ItemId, out var definition))
                equipSlotItems[slot] = definition;

        return equipSlotItems;
    }

    private static bool IsStunOrDeathMotion(int actionSort)
    {
        return actionSort is 11 or 12;
    }

    private static bool IsBroadcastBuffMotion(int actionSort)
    {
        return actionSort is 41 or 60 or 61 or 62 or 66 or 67 or 68 or 75;
    }

    private static void AdvanceBudgetAndRelocateIfExhausted(PlayerRuntimeState state, int legacyTicksElapsed)
    {
        var result = AutoHuntBudgetPolicy.Advance(state.AutoHuntPaidDayBudget, state.AutoHuntPaidMinuteBudget,
            state.AutoHuntBudgetMinuteAccrualTicks, legacyTicksElapsed, GameDate.Today());

        state.AutoHuntPaidDayBudget = result.DayBudget;
        state.AutoHuntPaidMinuteBudget = result.MinuteBudget;
        state.AutoHuntBudgetMinuteAccrualTicks = result.MinuteAccrualTicks;

        switch (result.Signal)
        {
            case AutoHuntBudgetPolicy.Signal.DayBudgetExpired:
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = AutoHuntDayStatSort, Value = result.DayBudget, Value2 = 0 });
                break;
            case AutoHuntBudgetPolicy.Signal.MinuteBudgetDecremented:
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = AutoHuntMinuteStatSort, Value = result.MinuteBudget, Value2 = 0 });
                break;
            case AutoHuntBudgetPolicy.Signal.Exhausted:
                if (state.OneSecondGateOpenCount > 0)
                    state.Session.Send(new ReturnToHomeZoneResponse());
                break;
        }
    }

    private static void EscalateNoMana(PlayerRuntimeState state)
    {
        state.NoManaCount++;
        if (state.NoManaCount == NoManaRelocateThreshold)
            state.Session.Send(new ReturnToHomeZoneResponse());
        else if (state.NoManaCount > NoManaRelocateThreshold && state.Session is { } client)
            client.Abort(DisconnectReason.StateViolation);
    }

    private bool IsSuppressedByZoneServerType(Zone zone)
    {
        if (RegularWarMapCatalog.TryGet(zone.MapId, out _))
            return true;

        if (options.Value.HolyStoneWarEnabled && zone.MapId == options.Value.HolyStoneMapId)
            return true;

        if (options.Value.HolyStoneBattleEnabled && zone.MapId == options.Value.TribeSymbolBattleMapId)
            return true;

        return zone.IsZone241TypeZone;
    }

    private static bool IsAlreadyActive(PlayerRuntimeState state, ImmutableArray<int> gateSlots)
    {
        foreach (var slot in gateSlots)
        {
            var value = state.Buffs.Buff[slot * 2 + 1];
            if (slot == RawTruthGateBuffSlot ? value != 0 : value > 0)
                return true;
        }

        return false;
    }

    private void ClearRejectedAutoBuffSelection(PlayerRuntimeState state, AutoHunt config, int slot)
    {
        var offset = slot * 2;
        if (config.BuffStore[offset] == 0 && config.BuffStore[offset + 1] == 0)
            return;

        config.BuffStore[offset] = 0;
        config.BuffStore[offset + 1] = 0;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }
}

using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;
using Fenrir.Core.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Tribes;

public sealed class TribeActionService(
    ZoneRegistry zones,
    ITribeRepository tribes,
    ICharacterRepository characters,
    WorldDataCache worldData,
    WorldStateService worldState,
    ILogger<TribeActionService> logger) : ITribeActionService
{
    private const int TribeWeaponMoneyCost = 100_000_000;
    private const int TowerScrollMoneyCost = 500_000_000;
    private const int HaloEnchantMoneyCost = 1_000_000;
    private const int HaloEnchantCpCost = 100;
    private const int MapScrollCpCost = 1;
    private const int AlertCharmCpCost = 10;
    private const int RebirthCpCost = 10_000;

    private const int MaxRebirth = 6;

    public async ValueTask<TribeActionOutcome> ResetStatsAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        if (state.Level > 39 || !IsValidTown(state.Tribe, zone.MapId))
            return TribeActionOutcome.Abort;

        var refund = state.StatVit + state.StatStr + state.StatInt + state.StatDex - 4;
        var newStatPoints = state.StatPoints + refund;

        var attributes = new CharacterBaseAttributes(1, 1, 1, 1, state.Level, state.Tribe, state.PreviousTribe,
            state.Title, state.Halo, state.RebirthCount, state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer), runtimeState: state);

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            StatVit: 1, StatStr: 1, StatInt: 1, StatDex: 1, StatPoints: newStatPoints,
            Life: 1, Mana: 0, UpdatedStats: updatedStats), ct);

        logger.LogInformation("Character {CharacterId} reset base stats, refunding {Refund} points", characterId,
            refund);

        return TribeActionOutcome.Ok();
    }

    public async ValueTask<TribeActionOutcome> AppointSubMasterAsync(Zone zone, PlayerRuntimeState state, byte[] data,
        CancellationToken ct)
    {
        if (state.TribeRole != 1 || !IsSubMasterCapitalZone(state.Tribe, zone.MapId) ||
            !TribeWorkNamePayload.TryRead(data, out var payload))
            return TribeActionOutcome.Abort;

        var targetName = payload.AvatarName.Trim();
        if (targetName.Length == 0)
            return TribeActionOutcome.Abort;

        var targetIdByName = await characters.GetIdByNameAsync(targetName, ct);
        var subMasters = await tribes.GetSubMastersAsync(state.Tribe, ct);
        if (targetIdByName is { } knownId && subMasters.Any(s => s.CharacterId == knownId))
            return TribeActionOutcome.Abort;

        var freeSlot = -1;
        for (byte slot = 0; slot < 12; slot++)
            if (subMasters.All(s => s.SlotIndex != slot))
            {
                freeSlot = slot;
                break;
            }

        if (freeSlot < 0)
            return TribeActionOutcome.Abort;

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
        {
            logger.LogDebug("Tribe {Tribe} sub-master appointment rejected: target {TargetName} not online in zone",
                state.Tribe, targetName);
            return TribeActionOutcome.Ok(1);
        }

        if (target.Level < 113)
        {
            logger.LogDebug(
                "Tribe {Tribe} sub-master appointment of {TargetCharacterId} rejected: level {Level} below 113",
                state.Tribe, target.CharacterId, target.Level);
            return TribeActionOutcome.Ok(2);
        }

        if (target.ContributionPoints < 1000)
        {
            logger.LogDebug(
                "Tribe {Tribe} sub-master appointment of {TargetCharacterId} rejected: contribution points below 1000",
                state.Tribe, target.CharacterId);
            return TribeActionOutcome.Ok(3);
        }

        if (subMasters.Any(s => s.CharacterId == target.CharacterId))
        {
            logger.LogDebug(
                "Tribe {Tribe} sub-master appointment of {TargetCharacterId} rejected: already a sub-master",
                state.Tribe, target.CharacterId);
            return TribeActionOutcome.Ok(4);
        }

        await tribes.SetSubMasterAsync(state.Tribe, (byte)freeSlot, target.CharacterId, ct);

        zone.PostTribeProgressCommand(new TribeProgressZoneCommand(target.CharacterId, TribeRole: 2));

        logger.LogInformation("Tribe {Tribe} appointed character {TargetCharacterId} as sub-master (slot {Slot})",
            state.Tribe, target.CharacterId, freeSlot);

        return TribeActionOutcome.Ok();
    }

    public async ValueTask<TribeActionOutcome> RemoveSubMasterAsync(Zone zone, PlayerRuntimeState state, byte[] data,
        CancellationToken ct)
    {
        if (state.TribeRole != 1 || !IsSubMasterCapitalZone(state.Tribe, zone.MapId) ||
            !TribeWorkNamePayload.TryRead(data, out var payload))
            return TribeActionOutcome.Abort;

        var targetName = payload.AvatarName.Trim();
        var targetId = await characters.GetIdByNameAsync(targetName, ct);

        var subMasters = await tribes.GetSubMastersAsync(state.Tribe, ct);
        if (targetId is null || subMasters.All(s => s.CharacterId != targetId.Value))
            return TribeActionOutcome.Abort;

        await tribes.ClearSubMasterAsync(state.Tribe, targetId.Value, ct);

        if (zones.TryGetPlayerAndZone(targetId.Value, out _, out var targetZone))
            targetZone.PostTribeProgressCommand(new TribeProgressZoneCommand(targetId.Value, TribeRole: 0));

        logger.LogInformation("Tribe {Tribe} removed character {TargetCharacterId} as sub-master", state.Tribe,
            targetId.Value);

        return TribeActionOutcome.Ok();
    }

    public async ValueTask<TribeActionOutcome> UseTribeWeaponAsync(Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken ct)
    {
        if (state.TribeRole is not (1 or 2) || !IsValidTown(state.Tribe, zone.MapId))
            return TribeActionOutcome.Abort;

        var itemId = 1075 + state.Tribe;

        try
        {
            await characters.AdjustMoneyAsync(characterId, -TribeWeaponMoneyCost, 0, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Character {CharacterId} tribe-weapon money debit failed (insufficient funds)",
                characterId);
            return TribeActionOutcome.Abort;
        }

        logger.LogInformation("Character {CharacterId} purchased tribe weapon (item {ItemId}) for tribe {Tribe}",
            characterId, itemId, state.Tribe);

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            DropItems: [new TribeGroundItemDrop(itemId, 1)]), ct);

        return TribeActionOutcome.Ok();
    }

    public TribeActionOutcome ValidateTribeSkill(PlayerRuntimeState state, byte[] data)
    {
        if (state.TribeRole != 1)
            return TribeActionOutcome.Abort;

        if (!TribeWorkSkillPayload.TryRead(data, out var payload) || payload.TribeSkillSort is < 0 or > 4)
            return TribeActionOutcome.Abort;

        var tribes = worldState.GetAllTribes();

        if (!TribeFormationAbilityEligibility.AllTribesAboveFloor(tribes))
            return TribeActionOutcome.Abort;

        var lowestPointTribe = TribeFormationAbilityEligibility.FindLowestPointTribe(tribes);
        if (state.Tribe != lowestPointTribe)
            return TribeActionOutcome.Abort;

        var combinedPoints = TribeFormationAbilityEligibility.CombinedPoints(tribes);
        if (!TribeFormationAbilityEligibility.IsUnderShareThreshold(tribes[state.Tribe].Points, combinedPoints))
            return TribeActionOutcome.Abort;

        if (!worldState.World.TribeSymbolBattle)
            return TribeActionOutcome.Abort;

        var formationCode = (byte)payload.TribeSkillSort;
        worldState.SetTribeFormationAbility(state.Tribe, formationCode);

        logger.LogInformation(
            "Character {CharacterId} (tribe {Tribe}) declared Formation ability code {FormationCode}",
            state.CharacterId, state.Tribe, formationCode);

        return TribeActionOutcome.Ok();
    }

    public async ValueTask<TribeActionOutcome> PurchaseTitleAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte[] data, CancellationToken ct)
    {
        if (!TribeWorkTitlePayload.TryRead(data, out var payload))
            return TribeActionOutcome.Abort;

        var currentRank = state.Title % 100;
        if (currentRank is < 0 or > 11)
            return TribeActionOutcome.Abort;

        var cost = TitleContributionCost.PurchaseStepCost(currentRank);
        if (state.ContributionPoints < cost)
            return TribeActionOutcome.Abort;

        var newTitle = (payload.TitleSort - 1) * 100 + currentRank + 1;

        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, newTitle, state.Halo, state.RebirthCount,
            state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer), runtimeState: state);

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            state.ContributionPoints - cost, Title: newTitle,
            Life: updatedStats.MaxLife, Mana: updatedStats.MaxMana, UpdatedStats: updatedStats), ct);

        logger.LogInformation(
            "Character {CharacterId} purchased title tier, new title {NewTitle} (spent {Cost} CP)", characterId,
            newTitle, cost);

        return TribeActionOutcome.Ok();
    }

    public async ValueTask<TribeActionOutcome> HaloEnchantAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (now - state.LastHaloEnchantAttemptUtc < SimulationClock.LegacyTick)
        {
            logger.LogWarning(
                "Character {CharacterId} halo-enchant rejected: same-tick repeat request", characterId);
            return TribeActionOutcome.Abort;
        }

        state.LastHaloEnchantAttemptUtc = now;

        if (state.ContributionPoints < HaloEnchantCpCost || state.Halo >= 96)
            return TribeActionOutcome.Abort;

        try
        {
            await characters.AdjustMoneyAsync(characterId, -HaloEnchantMoneyCost, 0, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Character {CharacterId} halo-enchant money debit failed (insufficient funds)",
                characterId);
            return TribeActionOutcome.Abort;
        }

        var (outcome, newHalo, newProtect) =
            TribeHaloEnchantResolver.Resolve(state.Halo, state.ProtectForHalo, SystemRandomSource.Instance);

        var result = outcome switch
        {
            TribeHaloEnchantOutcome.Success => 0,
            TribeHaloEnchantOutcome.Downgraded => 2,
            _ => 1
        };

        if (outcome is TribeHaloEnchantOutcome.Success or TribeHaloEnchantOutcome.Downgraded)
        {
            var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
                state.Level, state.Tribe, state.PreviousTribe, state.Title, newHalo, state.RebirthCount,
                state.Level2);
            var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
            var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
                pet: ComputePetContribution(state, equipmentContainer), runtimeState: state);

            await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                state.ContributionPoints - HaloEnchantCpCost, Halo: newHalo,
                ProtectForHalo: newProtect, UpdatedStats: updatedStats), ct);
        }
        else
        {
            await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
                state.ContributionPoints - HaloEnchantCpCost, ProtectForHalo: newProtect), ct);
        }

        logger.LogInformation("Character {CharacterId} halo-enchant {Outcome}: halo {OldHalo} -> {NewHalo}",
            characterId, outcome, state.Halo, newHalo);

        return TribeActionOutcome.Ok(result);
    }

    public async ValueTask<TribeActionOutcome> ClaimLevelBonusAsync(Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken ct)
    {
        if (!LevelMilestoneBonus.TryResolveClaimDrops(state.BonusItemLevel, state.PreviousTribe, out var drops))
            return TribeActionOutcome.Abort;

        var claimedTier = state.BonusItemLevel;

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            BonusItemLevel: 0, BonusItemValue: false, DropItems: drops), ct);

        logger.LogInformation(
            "Character {CharacterId} claimed level-milestone bonus for tier {Tier} ({DropCount} items)",
            characterId, claimedTier, drops.Length);

        return TribeActionOutcome.Ok();
    }

    public async ValueTask<TribeActionOutcome> SetOrnamentAsync(Zone zone, PlayerRuntimeState state, int characterId,
        bool on, CancellationToken ct)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount,
            state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);

        var zoneOverride = new ZoneContext(state.MapId, on, RankBuffType: state.RankBuffType,
            TribeRole: state.TribeRole, GuildBuffActive: state.GuildBuffActive, GuildId: state.GuildId ?? 0);

        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer), runtimeState: state, zoneOverride: zoneOverride);

        var command = on
            ? new TribeProgressZoneCommand(characterId, UseOrnament: true, UpdatedStats: updatedStats)
            : new TribeProgressZoneCommand(characterId, UseOrnament: false, Life: updatedStats.MaxLife,
                Mana: updatedStats.MaxMana, UpdatedStats: updatedStats);

        await zone.PostTribeProgressCommandAndWaitAsync(command, ct);

        logger.LogDebug("Character {CharacterId} set tribe ornament to {OnOff}", characterId, on ? "on" : "off");

        return TribeActionOutcome.Ok();
    }

    public async ValueTask<TribeActionOutcome> RebirthAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        if (state.RebirthCount >= RebirthProgression.MaxRebirthGeneration ||
            state.Level + state.Level2 != RebirthProgression.CombinedLevelCap ||
            !RebirthProgression.IsHighLevelExperienceFull(state.Level2, state.Exp2) ||
            state.ContributionPoints < RebirthCpCost)
            return TribeActionOutcome.Abort;

        if (state.RebirthCount >= MaxRebirth)
        {
            logger.LogDebug(
                "Character {CharacterId} Max Rebirth (Path B) rejected: already at the path-specific cap ({RebirthCount}/{MaxRebirth})",
                characterId, state.RebirthCount, MaxRebirth);
            return TribeActionOutcome.Ok(1);
        }

        var newRebirthCount = state.RebirthCount + 1;
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, newRebirthCount,
            state.Level2);
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var updatedStats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData,
            pet: ComputePetContribution(state, equipmentContainer), runtimeState: state);

        int newZone241Time;
        try
        {
            newZone241Time = await characters.AdjustZone241TimeAsync(characterId, 10, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Character {CharacterId} rebirth Zone241Time adjustment failed", characterId);
            return TribeActionOutcome.Abort;
        }

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            state.ContributionPoints - RebirthCpCost, RebirthCount: newRebirthCount, Exp2: 0,
            Life: updatedStats.MaxLife, Mana: updatedStats.MaxMana, UpdatedStats: updatedStats,
            RebirthBroadcast: true, Zone241Time: newZone241Time), ct);


        logger.LogInformation(
            "Character {CharacterId} completed Max Rebirth (Path B): generation {OldGeneration} -> {NewGeneration}",
            characterId, state.RebirthCount, newRebirthCount);

        return TribeActionOutcome.Ok();
    }

    public ValueTask<TribeActionOutcome> RedeemMapScrollAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        return RedeemScrollAsync(zone, state, characterId, 591, MapScrollCpCost, ct);
    }

    public ValueTask<TribeActionOutcome> RedeemAlertCharmAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        return RedeemScrollAsync(zone, state, characterId, 590, AlertCharmCpCost, ct);
    }

    public async ValueTask<TribeActionOutcome> UseTowerScrollAsync(Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken ct)
    {
        if (state.TribeRole is not (1 or 2))
            return TribeActionOutcome.Abort;

        try
        {
            await characters.AdjustMoneyAsync(characterId, -TowerScrollMoneyCost, 0, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Character {CharacterId} tower-scroll money debit failed (insufficient funds)",
                characterId);
            return TribeActionOutcome.Abort;
        }

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            DropItems: [new TribeGroundItemDrop(665, 1)]), ct);

        logger.LogInformation("Character {CharacterId} purchased a tower-construction scroll for tribe {Tribe}",
            characterId, state.Tribe);

        return TribeActionOutcome.Ok();
    }

    private async ValueTask<TribeActionOutcome> RedeemScrollAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int itemId, int cpCost, CancellationToken ct)
    {
        if (state.TribeRole is not (1 or 2) || state.ContributionPoints < cpCost)
            return TribeActionOutcome.Abort;

        await zone.PostTribeProgressCommandAndWaitAsync(new TribeProgressZoneCommand(characterId,
            state.ContributionPoints - cpCost,
            DropItems: [new TribeGroundItemDrop(itemId, 1)]), ct);

        logger.LogInformation("Character {CharacterId} redeemed item {ItemId} for {CpCost} CP", characterId, itemId,
            cpCost);

        return TribeActionOutcome.Ok();
    }

    private static bool IsValidTown(byte tribe, short mapId)
    {
        return tribe switch
        {
            0 => mapId == 1,
            1 => mapId == 6,
            2 => mapId == 11,
            3 => mapId == 140,
            _ => false
        };
    }

    private static bool IsSubMasterCapitalZone(byte tribe, short mapId)
    {
        return tribe switch
        {
            0 or 1 or 2 => mapId == 71 + tribe,
            3 => mapId == 140,
            _ => false
        };
    }

    private PetStatContribution ComputePetContribution(PlayerRuntimeState state,
        IReadOnlyDictionary<byte, ItemStack> equipmentContainer)
    {
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;

        return PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity, worldData.ItemsById);
    }
}

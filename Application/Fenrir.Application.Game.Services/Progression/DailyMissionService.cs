using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Progression;

/// <summary>Business logic extracted from <c>DailyMissionHandler</c> (CZ_MISSION_COMPLETE_SEND, opcode 126).</summary>
public sealed class DailyMissionService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<DailyMissionService> logger) : IDailyMissionService
{
    /// <summary>
    ///     <c>LV_M1</c> -- shared with <see cref="ExperienceFormulas.RebirthDivisorLevelThreshold" />. Gated on
    ///     <see cref="PlayerRuntimeState.CombinedLevel" /> (Level + Level2), matching every other LV_M1 consumer
    ///     (<c>Zone.Combat.cs</c>'s attacker/defenderCombinedLevel, <see cref="PvpKillPetExperienceCalculator" />'s
    ///     LevelFloor check) -- not the plain, non-rebirth-inclusive Level field.
    /// </summary>
    private const int MinimumClaimLevel = ExperienceFormulas.RebirthDivisorLevelThreshold;

    private const int RequiredJoinWar = 1;
    private const int RequiredKillOtherTribe = 10;

    /// <summary>
    ///     AVATAR_CHANGE_INFO_1 sort 14 -- the same ContributionPoints/RebirthCount/Zone241Time wire triple
    ///     <see cref="Fenrir.Application.Game.Domain.World.Zone.ApplyTribeProgressCommand" />'s own
    ///     <c>RebirthBroadcast</c> uses (see
    ///     <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState.Zone241Time" />'s
    ///     remarks) -- reused here for the unrelated Zone241Time source this claim adds, since it is the only
    ///     wire meaning this codebase has ever established for that field. Sent as a direct self-only unicast
    ///     (<see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState.Session" />), NOT through
    ///     <c>Zone.BroadcastAvatarStateFlag</c>'s AOI-wide fan-out -- this cluster's own cross-cutting contract
    ///     note is explicit that every response/push across QuestProgress/DailyMission/GetDailyRewardCatalog/
    ///     ClaimDailyReward is "a targeted unicast to one specific connection, never a zone-wide or proximity
    ///     broadcast", unlike the CZ_TRIBE_WORK_SEND tSort-11 Max Rebirth broadcast this sort code was first
    ///     established for.
    /// </summary>
    private const int Zone241TimeAvatarChangeInfoSort = 14;

    private static readonly byte[] InventoryPages = [ContainerMatrix.InventoryPage0, ContainerMatrix.InventoryPage1];

    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:14538-14606,14611-14618 (claim preconditions, item-creation
    ///     notification, and the second-tier-level-cap Zone241Time bonus) ; Server/Header/Protocol/DEFINE.h:400-403
    ///     (MAX_MISSION_* caps), 451 (LV_M1 = 113).
    ///     <para>
    ///         The item-creation notification is sent as <see cref="SetInventorySlotResponse" /> (ZC_SET_INVENTORY_ITEM_RECV,
    ///         opcode 194) rather than the dead <c>MultiItemCreateResponse</c> (ZC_MULTI_ITEM_CREATE_RECV,
    ///         opcode 119): the latter is documented (ServerDocs/12_ts25zone/06_MyWork03_Items_Equipement_Craft_Quetes.md
    ///         §8, "makeMultiItem") as the primitive for a single consumable resolving into *several* simultaneous
    ///         reward slots (bit-packed positional Page/Index/Xy encoding across up to 8 items) -- a different
    ///         shape than this single-item grant. <see cref="SetInventorySlotResponse" />'s plain Page/Index/Value[6]
    ///         layout matches every other already-wired single-slot notification in this codebase, and its
    ///         Value[6] population (<c>[itemId, 0, 0, quantity, 0, 0]</c>) mirrors the identical, already-shipped
    ///         convention this same contract cluster's sibling behavior already uses for the same shape
    ///         (<see cref="Fenrir.Application.Game.Services.Commerce.ClaimDailyRewardService" />'s own
    ///         <c>ClaimDailyRewardResponse.Value</c> construction).
    ///     </para>
    ///     <para>
    ///         "Second-tier level cap" is <see cref="PlayerRuntimeState.Level2" /> (aLevel2, the post-cap "high
    ///         level" rebirth ladder) sitting exactly at <see cref="RebirthProgression.MaxHighLevel" /> -- the
    ///         only "second-tier" level concept this codebase's own domain vocabulary already establishes (see
    ///         that constant's own remarks, shared with the Rebirth-advancement contract).
    ///     </para>
    /// </remarks>
    public async ValueTask<DailyMissionClaimResult> ClaimAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (state.CombinedLevel < MinimumClaimLevel || state.MissionJoinWar < RequiredJoinWar ||
            state.MissionKillOtherTribe < RequiredKillOtherTribe)
            return new DailyMissionClaimResult(DailyMissionClaimOutcome.Aborted, 0, 0);

        var itemId = DailyMissionRewardTable.Roll(Random.Shared.NextDouble);
        if (!worldData.ItemsById.TryGetValue(itemId, out var itemDefinition))
            // Can't happen against a fully-seeded catalog; mirrors legacy's mITEM.Search-NULL guard.
            return new DailyMissionClaimResult(DailyMissionClaimOutcome.Aborted, 0, 0);

        var quantity = itemDefinition.Item.Sort == 99 ? 1 : 0;

        if (!TryFindEmptySlot(state, out var container, out var slot))
            return new DailyMissionClaimResult(DailyMissionClaimOutcome.InventoryFull, 0, 0);

        var projected = state.Inventory.GetContainer(container)
            .SetItem(slot, new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var newJoinWar = state.MissionJoinWar - RequiredJoinWar;
        var newKillOtherTribe = state.MissionKillOtherTribe - RequiredKillOtherTribe;

        await characters.ApplyDailyMissionClaimAsync(characterId, newJoinWar, newKillOtherTribe,
            state.MissionKillMonster, state.MissionPlayTime, container, ToTvps(projected), cancellationToken);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projected));
        if (!await zone.PostMissionCommandAndWaitAsync(
                new MissionZoneCommand(characterId, newJoinWar, newKillOtherTribe, state.MissionKillMonster,
                    state.MissionPlayTime, containers), cancellationToken))
            logger.LogError(
                "Zone {MapId} mission inbox full: dropped claim mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        // Side effect 4 -- separate unicast item-creation notification, sent before DailyMissionResponse
        // itself (DailyMissionResponse carries only Sort/Result/counters, no item/slot fields at all).
        state.Session.Send(new SetInventorySlotResponse
        {
            Page = container,
            Index = slot,
            Value = [itemId, 0, 0, quantity, 0, 0]
        });

        // Side effect 5 -- second-tier-level-cap Zone241Time bonus, additive to (not a substitute for) the
        // ordinary claim above; a failure here never unwinds the already-granted, already-persisted reward.
        if (state.Level2 == RebirthProgression.MaxHighLevel)
            await GrantSecondTierZone241TimeBonusAsync(zone, state, characterId, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} claimed daily mission reward: item {ItemId}x{Quantity} at container {Container} slot {Slot}",
            characterId, itemId, quantity, container, slot);

        return new DailyMissionClaimResult(DailyMissionClaimOutcome.Success, newJoinWar, newKillOtherTribe);
    }

    /// <summary>
    ///     Side effect 5 of a successful claim -- durably persists the +1 aZone241Time bump
    ///     (<see cref="ICharacterRepository.AdjustZone241TimeAsync" />, the same dumb delta-adjust primitive
    ///     <c>TribeActionService.RebirthAsync</c>'s own Path B +10 uses), mirrors it onto
    ///     <see cref="PlayerRuntimeState.Zone241Time" /> via the tick thread (single-writer invariant -- this
    ///     method itself never assigns to <paramref name="state" /> directly), then pushes the
    ///     <see cref="Zone241TimeAvatarChangeInfoSort" /> notification directly to the claimant's own session.
    ///     Best-effort: a persistence failure here is logged and swallowed rather than propagated, since the
    ///     reward item itself was already granted and durably committed by the time this runs.
    /// </summary>
    private async ValueTask GrantSecondTierZone241TimeBonusAsync(Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken cancellationToken)
    {
        int newZone241Time;
        try
        {
            newZone241Time = await characters.AdjustZone241TimeAsync(characterId, 1, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} daily-mission Zone241Time bonus adjustment failed -- reward claim itself already succeeded",
                characterId);
            return;
        }

        // No RebirthBroadcast here (see Zone241TimeAvatarChangeInfoSort's own remarks) -- this only mirrors
        // the already-persisted value onto PlayerRuntimeState, matching Tribe/QuestProgress/StoreMoney's own
        // "no dirty mark, nothing left to flush" posture for the same reason (see
        // Zone.ApplyTribeProgressCommand's remarks).
        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, Zone241Time: newZone241Time), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Zone241Time mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        state.Session.Send(new AvatarStateFlagResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            Sort = Zone241TimeAvatarChangeInfoSort,
            Value01 = state.ContributionPoints,
            Value02 = state.RebirthCount,
            Value03 = newZone241Time
        });

        logger.LogInformation(
            "Character {CharacterId} claimed daily-mission second-tier-level-cap Zone241Time bonus: new total {NewZone241Time}",
            characterId, newZone241Time);
    }

    /// <summary>First empty slot across both inventory pages, page 0 then page 1 (legacy scan order).</summary>
    private static bool TryFindEmptySlot(PlayerRuntimeState state, out byte container, out byte slot)
    {
        foreach (var page in InventoryPages)
        {
            var occupied = state.Inventory.GetContainer(page);
            for (var i = 0; i <= 63; i++)
            {
                if (occupied.ContainsKey((byte)i))
                    continue;
                container = page;
                slot = (byte)i;
                return true;
            }
        }

        container = 0;
        slot = 0;
        return false;
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}

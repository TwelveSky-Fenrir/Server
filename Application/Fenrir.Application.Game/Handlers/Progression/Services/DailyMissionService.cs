using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Progression;
using Fenrir.Application.Game.World;
using Fenrir.Data.Characters;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Progression.Services;

public enum DailyMissionClaimOutcome
{
    /// <summary>A pre-claim gate failed (level/war/kill-tribe thresholds, or an unresolvable roll) -- caller must disconnect.</summary>
    Aborted,

    /// <summary>Every gate passed but no empty inventory slot was found for the reward -- clean failure (Result = 3).</summary>
    InventoryFull,

    Success
}

/// <summary>
///     <see cref="DailyMissionClaimOutcome.Success" /> carries the post-claim <see cref="JoinWar" />/
///     <see cref="KillOtherTribe" /> counters (both decremented by their claim requirement); unused for the other outcomes.
/// </summary>
public readonly record struct DailyMissionClaimResult(DailyMissionClaimOutcome Outcome, int JoinWar, int KillOtherTribe);

public interface IDailyMissionService
{
    ValueTask<DailyMissionClaimResult> ClaimAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken);
}

/// <summary>Business logic extracted from <c>DailyMissionHandler</c> (CZ_MISSION_COMPLETE_SEND, opcode 126).</summary>
public sealed class DailyMissionService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<DailyMissionService> logger) : IDailyMissionService
{
    /// <summary><c>LV_M1</c> -- shared with <see cref="ExperienceFormulas.RebirthDivisorLevelThreshold" />.</summary>
    private const int MinimumClaimLevel = ExperienceFormulas.RebirthDivisorLevelThreshold;

    private const int RequiredJoinWar = 1;
    private const int RequiredKillOtherTribe = 10;

    private static readonly byte[] InventoryPages = [ContainerMatrix.InventoryPage0, ContainerMatrix.InventoryPage1];

    public async ValueTask<DailyMissionClaimResult> ClaimAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (state.Level < MinimumClaimLevel || state.MissionJoinWar < RequiredJoinWar ||
            state.MissionKillOtherTribe < RequiredKillOtherTribe)
            return new DailyMissionClaimResult(DailyMissionClaimOutcome.Aborted, 0, 0);

        var itemId = DailyMissionRewardTable.Roll(Random.Shared.NextDouble);
        if (!worldData.ItemsById.TryGetValue(itemId, out var itemDefinition))
        {
            // Can't happen against a fully-seeded catalog; mirrors legacy's mITEM.Search-NULL guard.
            return new DailyMissionClaimResult(DailyMissionClaimOutcome.Aborted, 0, 0);
        }

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

        return new DailyMissionClaimResult(DailyMissionClaimOutcome.Success, newJoinWar, newKillOtherTribe);
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

using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Progression;

public sealed class DailyMissionService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    IOptions<GameServerOptions> options,
    ILogger<DailyMissionService> logger) : IDailyMissionService
{
    private const int MinimumClaimLevel = ExperienceFormulas.RebirthDivisorLevelThreshold;

    private const int RequiredJoinWar = 1;
    private const int RequiredKillOtherTribe = 10;

    private const int Zone241TimeStatSort = 200;

    private const int MultiItemCreateNumBase = 7001;

    private const int RewardItemCount = 1;

    public async ValueTask<DailyMissionClaimResult> ClaimAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (state.Level < MinimumClaimLevel || state.MissionJoinWar < RequiredJoinWar ||
            state.MissionKillOtherTribe < RequiredKillOtherTribe)
            return new DailyMissionClaimResult(DailyMissionClaimOutcome.Aborted, 0, 0);

        var itemId = DailyMissionRewardTable.Roll(Random.Shared.NextDouble);
        if (!worldData.ItemsById.TryGetValue(itemId, out var itemDefinition))
            return new DailyMissionClaimResult(DailyMissionClaimOutcome.Aborted, 0, 0);

        var quantity = itemDefinition.Item.Sort == 99 ? 1 : 0;

        var freeSlot = InventoryFreeSlotFinder.Find(state.Inventory, worldData, itemId, state.InventoryDate,
            GameDate.Today());
        if (freeSlot is not { } destination)
            return new DailyMissionClaimResult(DailyMissionClaimOutcome.InventoryFull, 0, 0);

        var container = destination.Container;
        var slot = destination.Slot;

        var projected = state.Inventory.GetContainer(container)
            .SetItem(slot,
                new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0, destination.X, destination.Y));

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

        state.Session.Send(new MultiItemCreateResponse
        {
            Num = MultiItemCreateNumBase + RewardItemCount,
            Page = container,
            Index1 = slot,
            Index2 = 0,
            Xy1 = destination.GridIndex,
            Xy2 = 0,
            ItemIndex = [itemId, 0, 0, 0, 0, 0, 0, 0],
            Value = [0, 0, 0, 0]
        });

        if (options.Value.ChallengeContentEnabled && state.Level2 == RebirthProgression.MaxHighLevel)
            await GrantSecondTierZone241TimeBonusAsync(zone, state, characterId, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} claimed daily mission reward: item {ItemId}x{Quantity} at container {Container} slot {Slot}",
            characterId, itemId, quantity, container, slot);

        return new DailyMissionClaimResult(DailyMissionClaimOutcome.Success, newJoinWar, newKillOtherTribe);
    }

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

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId, Zone241Time: newZone241Time), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped Zone241Time mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        state.Session.Send(new AvatarStatUpdateResponse
            { Sort = Zone241TimeStatSort, Value = newZone241Time, Value2 = 0 });

        logger.LogInformation(
            "Character {CharacterId} claimed daily-mission second-tier-level-cap Zone241Time bonus: new total {NewZone241Time}",
            characterId, newZone241Time);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}

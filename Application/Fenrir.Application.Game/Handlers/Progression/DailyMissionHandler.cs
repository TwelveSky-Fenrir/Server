using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Progression;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>
///     CZ_MISSION_COMPLETE_SEND (opcode 126) -- daily mission view/claim. Claim gates on level &gt;= LV_M1 and
///     war/kill-tribe thresholds; <c>aKillMonster</c>/<c>aPlayTime</c> gates are compiled out in EU33 so they
///     never block a claim. A full inventory on claim is a clean failure (<c>Result = 3</c>).
/// </summary>
/// <remarks>
///     <see cref="PlayerRuntimeState.MissionJoinWar" /> still has no increment hook in Fenrir (war tracking out of
///     scope), so a claim needs a war-system gap to close before it's reachable end to end.
///     <see cref="PlayerRuntimeState.MissionKillOtherTribe" /> DOES increment now -- <c>Zone.ApplyPvpKillMissionProgress</c>,
///     gated by <see cref="Combat.KillCooldownTracker" /> (C05) -- so a claim is blocked only by the join-war side today.
/// </remarks>
public sealed class DailyMissionHandler(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<DailyMissionHandler> logger)
    : IAsyncPacketHandler<DailyMissionRequest>
{
    /// <summary><c>LV_M1</c> -- shared with <see cref="ExperienceFormulas.RebirthDivisorLevelThreshold" />.</summary>
    private const int MinimumClaimLevel = ExperienceFormulas.RebirthDivisorLevelThreshold;

    private const int RequiredJoinWar = 1;
    private const int RequiredKillOtherTribe = 10;

    private static readonly byte[] InventoryPages = [ContainerMatrix.InventoryPage0, ContainerMatrix.InventoryPage1];

    public async ValueTask HandleAsync(DailyMissionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (packet.Sort is not (1 or 2))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.Sort == 1)
        {
            SendResult(session, packet.Sort, 0, state);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await HandleClaimAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask HandleClaimAsync(DailyMissionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        if (state.Level < MinimumClaimLevel || state.MissionJoinWar < RequiredJoinWar ||
            state.MissionKillOtherTribe < RequiredKillOtherTribe)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var itemId = DailyMissionRewardTable.Roll(Random.Shared.NextDouble);
        if (!worldData.ItemsById.TryGetValue(itemId, out var itemDefinition))
        {
            // Can't happen against a fully-seeded catalog; mirrors legacy's mITEM.Search-NULL guard.
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var quantity = itemDefinition.Item.Sort == 99 ? 1 : 0;

        if (!TryFindEmptySlot(state, out var container, out var slot))
        {
            SendResult(session, packet.Sort, 3, state);
            return;
        }

        var projected = state.Inventory.GetContainer(container)
            .SetItem(slot, new ItemStack(itemId, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var newJoinWar = state.MissionJoinWar - RequiredJoinWar;
        var newKillOtherTribe = state.MissionKillOtherTribe - RequiredKillOtherTribe;

        await characters.ApplyDailyMissionClaimAsync(characterId, newJoinWar, newKillOtherTribe,
            state.MissionKillMonster, state.MissionPlayTime, container, ToTvps(projected), ct);

        SendResult(session, packet.Sort, 0, state, newJoinWar, newKillOtherTribe);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(container, projected));
        if (!await zone.PostMissionCommandAndWaitAsync(
                new MissionZoneCommand(characterId, newJoinWar, newKillOtherTribe, state.MissionKillMonster,
                    state.MissionPlayTime, containers), ct))
            logger.LogError(
                "Zone {MapId} mission inbox full: dropped claim mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
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

    private static void SendResult(IPacketSession session, int sort, int result, PlayerRuntimeState state,
        int? joinWarOverride = null, int? killOtherTribeOverride = null)
    {
        session.Send(new DailyMissionResponse
        {
            Sort = sort,
            Result = result,
            Mission = new MissionDate
            {
                JoinWar = joinWarOverride ?? state.MissionJoinWar,
                KillOtherTribe = killOtherTribeOverride ?? state.MissionKillOtherTribe,
                KillMonster = state.MissionKillMonster,
                PlayTime = state.MissionPlayTime
            }
        });
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}

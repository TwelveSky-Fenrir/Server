using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Core.Packets.Shared;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmClearInventoryService(
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    ILogger<GmClearInventoryService> logger) : IGmClearInventoryService
{
    private const int Sort = 701;

    private const int AcceptedResult = 0;

    private static readonly ImmutableDictionary<byte, ItemStack> EmptyContainer =
        ImmutableDictionary<byte, ItemStack>.Empty;

    private static readonly IReadOnlyList<CharacterItemSlotTvp> EmptyTvps = [];

    public async ValueTask HandleAsync(GmClearInventoryPayload packet, byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Basic))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Basic-tier GM_CLEAR_INVENTORY command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        bool clearPage0;
        bool clearPage1;
        switch (packet.PageSelector)
        {
            case 0:
                clearPage0 = true;
                clearPage1 = false;
                break;
            case 1:
                clearPage0 = false;
                clearPage1 = true;
                break;
            default:
                clearPage0 = true;
                clearPage1 = true;
                break;
        }

        if (clearPage0)
            await characters.ReplaceContainerAsync(state.CharacterId, ContainerMatrix.InventoryPage0, EmptyTvps,
                cancellationToken);
        if (clearPage1)
            await characters.ReplaceContainerAsync(state.CharacterId, ContainerMatrix.InventoryPage1, EmptyTvps,
                cancellationToken);

        var containers = clearPage0 && clearPage1
            ? ImmutableArray.Create(
                new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, EmptyContainer),
                new InventoryContainerSnapshot(ContainerMatrix.InventoryPage1, EmptyContainer))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot(
                    clearPage0 ? ContainerMatrix.InventoryPage0 : ContainerMatrix.InventoryPage1, EmptyContainer));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(state.CharacterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped GM_CLEAR_INVENTORY mirror for character {CharacterId}",
                zone.MapId, state.CharacterId);

        await eventLog.LogAsync(GmDuelAndInventoryActionEventCodes.ClearInventory, EventLogCategory.GmAction,
            zoneSession.AccountId, zoneSession.CharacterId, null, null, null, null, null, null, null, 1,
            $"PageSelector={packet.PageSelector};ClearedPage0={clearPage0};ClearedPage1={clearPage1}",
            cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} applied the Basic-tier GM_CLEAR_INVENTORY command (pageSelector {PageSelector}, clearedPage0={ClearedPage0}, clearedPage1={ClearedPage1})",
            state.CharacterId, packet.PageSelector, clearPage0, clearPage1);

        zoneSession.Send(new GenericActionResponse
            { Result = AcceptedResult, Sort = Sort, Data = data, RuneValue = 0 });
    }
}

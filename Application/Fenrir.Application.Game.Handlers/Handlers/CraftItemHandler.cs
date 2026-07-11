using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class CraftItemHandler(ICraftItemService craftItemService, ILogger<CraftItemHandler> logger)
    : IAsyncPacketHandler<CraftItemRequest>
{
    public async ValueTask HandleAsync(CraftItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: CraftItemRequest received, recipe sort {Sort}",
                zoneSession.SessionId, characterId, packet.Sort);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
        {
            logger.LogDebug(
                "Session {SessionId} character {CharacterId}: CraftItemRequest dropped, no live zone/player state",
                zoneSession.SessionId, characterId);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            switch (packet.Sort)
            {
                case CraftRecipeCatalog.JadeUpgradeSort:
                {
                    var result = await craftItemService.ResolveJadeUpgradeAsync(packet, zone, state, characterId,
                        accountId, cancellationToken);

                    if (result.Outcome != JadeUpgradeOutcome.Applied)
                    {
                        zoneSession.Abort(DisconnectReason.Faulted);
                        return;
                    }

                    session.Send(new CraftItemResponse
                    {
                        Result = 0, Value = [result.ResultItemId, 0, 0, 0, 0, result.Serial]
                    });
                    return;
                }
                case CraftRecipeCatalog.AdvancedElixirSort:
                {
                    var result = await craftItemService.ResolveAdvancedElixirAsync(packet, zone, state, characterId,
                        accountId, cancellationToken);

                    if (result.Outcome == AdvancedElixirOutcome.Rejected)
                    {
                        zoneSession.Abort(DisconnectReason.Faulted);
                        return;
                    }

                    if (result.Outcome == AdvancedElixirOutcome.Success)
                    {
                        var newItemStack = result.NewItemStack!.Value;

                        session.Send(new AddInventoryItemResponse
                        {
                            Result = 0,
                            ItemIndex = newItemStack.ItemId,
                            Page = result.ResultPage,
                            Index = result.ResultIndex,
                            Xy = 0,
                            Quantity = newItemStack.Quantity,
                            Value = 0,
                            Serial = newItemStack.Serial,
                            Socket = [0, 0, 0],
                            Expire = 0
                        });
                    }

                    session.Send(new CraftItemResponse
                    {
                        Result = MaterialResultCode(result.RemainingMaterial),
                        Value = MaterialValue(result.RemainingMaterial)
                    });
                    return;
                }
                case CraftRecipeCatalog.StoneMatCombineSort:
                {
                    var result = await craftItemService.ResolveStoneMatCombineAsync(packet, zone, state,
                        characterId, accountId, cancellationToken);

                    if (!result.Applied)
                    {
                        zoneSession.Abort(DisconnectReason.Faulted);
                        return;
                    }

                    session.Send(new CraftItemResponse { Result = 0, Value = ResponseValue(result) });
                    return;
                }
                case CraftRecipeCatalog.MountFusionTier1Sort:
                case CraftRecipeCatalog.MountFusionTier2Sort:
                {
                    var result = await craftItemService.ResolveMountFusionAsync(packet, zone, state, characterId,
                        accountId, cancellationToken);

                    if (!result.Applied)
                    {
                        zoneSession.Abort(DisconnectReason.Faulted);
                        return;
                    }

                    session.Send(StandardResponse(result));
                    return;
                }
                case CraftRecipeCatalog.WingAssemblySort:
                {
                    var result = await craftItemService.ResolveWingAssemblyAsync(packet, zone, state, characterId,
                        accountId, cancellationToken);

                    if (!result.Applied)
                    {
                        zoneSession.Abort(DisconnectReason.Faulted);
                        return;
                    }

                    session.Send(WingP1Response(result));
                    return;
                }
                case CraftRecipeCatalog.FeatherTierUpWhiteToBlackSort:
                case CraftRecipeCatalog.FeatherTierUpBlackToGoldSort:
                {
                    var result = await craftItemService.ResolveFeatherTierUpAsync(packet, zone, state, characterId,
                        accountId, cancellationToken);

                    if (!result.Applied)
                    {
                        zoneSession.Abort(DisconnectReason.Faulted);
                        return;
                    }

                    SendGrantedItem(session, result);
                    session.Send(StandardResponse(result));
                    return;
                }
                case CraftRecipeCatalog.WingTierRerollSort:
                {
                    var result = await craftItemService.ResolveWingTierRerollAsync(packet, zone, state, characterId,
                        accountId, cancellationToken);

                    if (!result.Applied)
                    {
                        zoneSession.Abort(DisconnectReason.Faulted);
                        return;
                    }

                    session.Send(WingP3Response(result));
                    return;
                }
                case CraftRecipeCatalog.WingFifthTierSort:
                case CraftRecipeCatalog.WingSixthTierUnvalidatedSort:
                {
                    var result = await craftItemService.ResolveWingFifthTierAsync(packet, zone, state, characterId,
                        accountId, cancellationToken);

                    if (!result.Applied)
                    {
                        zoneSession.Abort(DisconnectReason.Faulted);
                        return;
                    }

                    session.Send(StandardResponse(result));
                    return;
                }
                case CraftRecipeCatalog.DustRecycleWingSort:
                case CraftRecipeCatalog.DustRecycleCloakSort:
                case CraftRecipeCatalog.DustRecycleAnimalSort:
                case CraftRecipeCatalog.DustRecyclePet1Sort:
                case CraftRecipeCatalog.DustRecyclePet2Sort:
                {
                    var result = await craftItemService.ResolveDustRecycleAsync(packet, zone, state, characterId,
                        accountId, cancellationToken);

                    if (!result.Applied)
                    {
                        zoneSession.Abort(DisconnectReason.Faulted);
                        return;
                    }

                    SendGrantedItem(session, result);

                    var response = packet.Sort == CraftRecipeCatalog.DustRecycleWingSort && result.GrantedItem is null
                        ? WingP3Response(result)
                        : StandardResponse(result);
                    session.Send(response);
                    return;
                }
                default:
                    logger.LogInformation(
                        "Session {SessionId} character {CharacterId}: CraftItemRequest aborted, unrecognized recipe sort {Sort}",
                        zoneSession.SessionId, characterId, packet.Sort);
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
            }
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

        private static int MaterialResultCode(ItemStack? remainingMaterial)
    {
        return remainingMaterial is null ? 1001 : 10001;
    }

    private static int[] MaterialValue(ItemStack? remainingMaterial)
    {
        return remainingMaterial is { } m ? [m.ItemId, 0, 0, m.Quantity, 0, m.Serial] : [0, 0, 0, 0, 0, 0];
    }

        private static CraftItemResponse StandardResponse(CraftFamilyResult result)
    {
        return new CraftItemResponse { Result = WireResult(1001, result.ResultItemId), Value = ResponseValue(result) };
    }

        private static CraftItemResponse WingP1Response(CraftFamilyResult result)
    {
        return new CraftItemResponse { Result = WireResult(1002, result.ResultItemId), Value = ResponseValue(result) };
    }

        private static CraftItemResponse WingP3Response(CraftFamilyResult result)
    {
        return new CraftItemResponse { Result = WireResult(1003, result.ResultItemId), Value = ResponseValue(result) };
    }

    private static int WireResult(int baseCode, int slot1ItemId)
    {
        return slot1ItemId != 0 ? baseCode + 9000 : baseCode;
    }

    private static int[] ResponseValue(CraftFamilyResult result)
    {
        return [result.ResultItemId, 0, 0, result.ResultQuantity, 0, result.Serial];
    }

        private static void SendGrantedItem(IPacketSession session, CraftFamilyResult result)
    {
        if (result.GrantedItem is not { } granted)
            return;

        session.Send(new AddInventoryItemResponse
        {
            Result = 0,
            ItemIndex = granted.ItemId,
            Page = result.GrantedPage,
            Index = result.GrantedIndex,
            Xy = 0,
            Quantity = granted.Quantity,
            Value = 0,
            Serial = granted.Serial,
            Socket = [0, 0, 0],
            Expire = 0
        });
    }
}

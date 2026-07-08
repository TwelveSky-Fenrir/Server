using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op29, CZ_MAKE_ITEM_SEND -- dispatches every reachable <c>MK_*</c> recipe sort verified against the
///     shipped <c>#ifdef LNW33</c>/<c>USE_WING</c> branch (Jade upgrade, Advanced Elixir, stone-mat combine,
///     mount fusion, wing assembly/tier-up/reroll, wing-dust recycling) via <see cref="ICraftItemService" />;
///     every other MK_* sort aborts, matching the legacy's own <c>default: Quit()</c>. The legacy source's
///     tribe-wide "notable craft" announcement (<c>MakeNotice</c>) that several of these recipes also trigger
///     is not reproduced here -- same precedent as <see cref="CraftPetHandler" />'s own remarks (no
///     single-process equivalent for that cross-server notice).
/// </summary>
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

        // Serializes the read/SQL/mirror sequence per character to close an item-duplication window.
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

                    // X/Y sub-grid position has no Fenrir-side backing (ContainerMatrix is flat-slot) -- left 0,
                    // cosmetic only.
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

                        // B_MAKE_ITEM_RECV describes the CONSUMED material slot, not the new item -- the new item
                        // rides the separate ZC_ADD_USER_INVENTORY_ITEM_RECV, sent first so the client learns of
                        // the new item before the craft result referencing the consumed slot.
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

                    // mTRANSFER.B_MAKE_ITEM_RECV(0, tValue) -- hardcoded Result=0, same convention as Jade
                    // Upgrade above, not the sendMakeItem/P1/P3 1001-family lambdas.
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

                    // sendMakeWingP1 -- the one recipe in this family with a genuine hard-failure roll
                    // outcome (Result carries itemId=0 rather than a dust substitute).
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

                    // MK_DUST_WING's exact-quantity (in-place convert) branch alone uses sendMakeWingP3; every
                    // other outcome in this family -- including MK_DUST_WING's own over-threshold branch --
                    // uses the standard sendMakeItem helper (S04_MyWork02.cpp:5590 vs
                    // :5617/5643/5670/5696/5723/5762/5789/5826/5853).
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

    /// <summary>1001 if the material slot emptied out, 10001 if some quantity remains.</summary>
    private static int MaterialResultCode(ItemStack? remainingMaterial)
    {
        return remainingMaterial is null ? 1001 : 10001;
    }

    private static int[] MaterialValue(ItemStack? remainingMaterial)
    {
        return remainingMaterial is { } m ? [m.ItemId, 0, 0, m.Quantity, 0, m.Serial] : [0, 0, 0, 0, 0, 0];
    }

    /// <summary>
    ///     sendMakeItem's own Result convention: 1001 if slot1 ends up empty (itemId 0, only reachable via
    ///     <see cref="CraftRecipeCatalog.WingAssemblySort" />'s destroy branch), else 10001.
    /// </summary>
    private static CraftItemResponse StandardResponse(CraftFamilyResult result)
    {
        return new CraftItemResponse { Result = WireResult(1001, result.ResultItemId), Value = ResponseValue(result) };
    }

    /// <summary>
    ///     sendMakeWingP1's own Result convention: 1002 (empty) / 10002 --
    ///     <see cref="CraftRecipeCatalog.WingAssemblySort" /> only.
    /// </summary>
    private static CraftItemResponse WingP1Response(CraftFamilyResult result)
    {
        return new CraftItemResponse { Result = WireResult(1002, result.ResultItemId), Value = ResponseValue(result) };
    }

    /// <summary>sendMakeWingP3's own Result convention: 1003 (empty) / 10003.</summary>
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

    /// <summary>
    ///     B_ADD_USER_INVENTORY_ITEM_RECV for the brand-new stack some recipes grant into a separate free slot
    ///     (feather tier-up over-quantity, dust-recycle over-threshold) -- sent before the craft-result packet
    ///     describing the consumed/reduced material slot, same ordering as the already-ported Advanced Elixir
    ///     recipe above.
    /// </summary>
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

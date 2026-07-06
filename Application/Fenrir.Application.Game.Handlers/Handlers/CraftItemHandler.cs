using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op29, CZ_MAKE_ITEM_SEND -- implements the two recipes <see cref="CraftRecipeCatalog" />/
///     <see cref="Combat.SystemRandomSource" /> (via <see cref="ICraftItemService" />) cover (Jade upgrade,
///     advanced elixir); every other MK_* sort aborts, matching the legacy's own <c>default: Quit()</c>.
/// </summary>
public sealed class CraftItemHandler(ICraftItemService craftItemService)
    : IAsyncPacketHandler<CraftItemRequest>
{
    public async ValueTask HandleAsync(CraftItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;
        var accountId = zoneSession.AccountId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

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
                default:
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
}

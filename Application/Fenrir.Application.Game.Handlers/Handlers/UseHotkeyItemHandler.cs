using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     op22, CZ_USE_HOTKEY_ITEM_SEND -- NOT a hotkey assignment; activates whatever is already bound at the
///     given hotkey (page, index). Only item-kind bindings are resolved to a real effect today (world.Items
///     iSort==2, the generic consumable/potion family, potion-type codes 1-5 for life/mana gain, 9 as a
///     genuine no-op, and 12-15 for the four fixed-value self-buff scrolls/books) -- see
///     <c>HotkeyItemConsumptionResolver</c>'s own remarks for which potion-type codes (6, 16) are still
///     recognized but a deliberate clean-reject pending their own wire-notification citation.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:2203-2492 (full <c>BEGIN_CZ(USE_HOTKEY_ITEM_SEND)</c>
///     handler: page/index bounds check, hotkey-kind switch, action-state block-list, item lookup/category/
///     quantity checks, the per-potion-type switch, the unconditional decrement-then-acknowledge tail, and the
///     post-acknowledgment stat/effect application). See <c>HotkeyItemConsumptionResolver</c>'s own remarks
///     for the full citation trail (MAX_HOT_KEY_PAGE/NUM, MAX_POTION_SORT_NUM, the "may eat potion" flag's two
///     writers).
/// </remarks>
public sealed class UseHotkeyItemHandler(IUseHotkeyItemService service) : IAsyncPacketHandler<UseHotkeyItemRequest>
{
    public async ValueTask HandleAsync(UseHotkeyItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        var page = packet.Page1;
        var index = packet.Index1;

        UseHotkeyItemOutcome outcome;
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            outcome = await service.UseAsync(zone, state, characterId, page, index, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }

        switch (outcome)
        {
            case UseHotkeyItemOutcome.Disconnect:
                // Malformed input or corrupted/stale state (bad page/index, non-item binding, unresolved
                // item id, wrong item category, exhausted quantity, or an unrecognized potion-type code) --
                // no ack is sent, matching the legacy handler's own Quit() on every one of these branches.
                zoneSession.Abort(DisconnectReason.Malformed);
                return;
            case UseHotkeyItemOutcome.RejectedClean:
                session.Send(new UseHotkeyItemResponse { Result = 1, Page = page, Index = index });
                return;
            default:
                session.Send(new UseHotkeyItemResponse { Result = 0, Page = page, Index = index });
                return;
        }
    }
}

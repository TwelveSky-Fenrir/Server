using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     One per-item(-family) branch of the op23 (CZ_USE_INVENTORY_ITEM_SEND) dispatcher. Introduced by
///     workstream C9 to replace the legacy's flat, ~80-case <c>switch(iIndex)</c> plus two inlined pre-switch
///     direct handlers with a resolved-once registry (<see cref="UseItemHandlerRegistry" />): the op23 service
///     resolves the addressed item to exactly one handler and delegates, instead of a growing if/else cascade.
///     Every handler owns its own family's durable mutation, item consumption, and reply — the same "one
///     branch, all-or-nothing per use" contract the legacy dispatcher gives each of its cases.
///     <para>
///         Reference: Server/ts25zone/S04_MyWork03.cpp:1855-8160 (the whole op23 handler); this interface is
///         the Fenrir dispatch seam, not a legacy construct.
///     </para>
/// </summary>
public interface IUseItemHandler
{
    /// <summary>
    ///     Applies this family's effect for one use. Every rejection is answered with a clean failure reply
    ///     (result 1) rather than a disconnect: <see cref="UseInventoryItemResponse" /> cannot signal
    ///     "disconnect" back through the op23 service's return shape, so the same documented op23 simplification
    ///     the service's own families already use (legacy Quit()/illegal-input paths collapse to result 1) is
    ///     applied here too.
    /// </summary>
    public ValueTask<UseInventoryItemResponse> HandleAsync(UseItemContext context, CancellationToken cancellationToken);
}

/// <summary>
///     Everything a <see cref="IUseItemHandler" /> needs to act on one op23 use, already resolved by the
///     service's common gate (throttle, page/slot bounds, dated-vault access, item lookup). A value type so
///     dispatch never allocates a context on the request-thread path.
/// </summary>
/// <param name="Zone">The zone hosting the acting character — the target of every mirror command a handler posts.</param>
/// <param name="State">The acting character's authoritative in-memory state.</param>
/// <param name="CharacterId">The acting character.</param>
/// <param name="AccountId">The acting character's account, for the audit trail.</param>
/// <param name="Page">The addressed inventory page (already bounds-checked: 0 or 1).</param>
/// <param name="Index">The addressed slot on that page (already bounds-checked: 0-63).</param>
/// <param name="Item">The item occupying the addressed slot (already resolved to a non-empty, cataloged stack).</param>
/// <param name="Definition">The addressed item's reference-table row (already resolved).</param>
/// <param name="Value">
///     The wire request's multi-purpose numeric field — client-supplied and never trusted as a decision input
///     (see the op23 contract's Inputs section); a handler that echoes it does so only in the reply, never as a
///     gate.
/// </param>
public readonly record struct UseItemContext(
    Zone Zone,
    PlayerRuntimeState State,
    int CharacterId,
    int AccountId,
    byte Page,
    byte Index,
    ItemStack Item,
    ItemDefinition Definition,
    int Value);

/// <summary>Shared op23 reply shapes (Server/ts25zone/S04_MyWork03.cpp:101-104: result 0 = ok, 1 = fail, 3 = full).</summary>
public static class UseItemResponses
{
    public static UseInventoryItemResponse Fail(byte page, byte index)
    {
        return new UseInventoryItemResponse { Result = 1, Page = page, Index = index, Value = 0, Value2 = 0 };
    }

    public static UseInventoryItemResponse InventoryFull(byte page, byte index)
    {
        return new UseInventoryItemResponse { Result = 3, Page = page, Index = index, Value = 0, Value2 = 0 };
    }

    public static UseInventoryItemResponse Success(byte page, byte index, int value = 0, int value2 = 0)
    {
        return new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = value, Value2 = value2 };
    }
}

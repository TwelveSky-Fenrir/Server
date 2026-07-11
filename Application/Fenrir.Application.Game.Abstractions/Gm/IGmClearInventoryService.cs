using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the Basic-tier (<c>GmCommandTier.Basic</c>, legacy <c>uUserSort &gt;= 1</c>)
///     "GM_CLEAR_INVENTORY" command: legacy PROCESS_DATA_SEND (opcode 19, <c>GenericActionRequest</c>)
///     sub-command 701, Server/ts25zone/S04_MyWork04.cpp:2084-2111 -- there is no dedicated legacy wire opcode
///     for this action; <c>GenericActionHandler</c>'s own tSort 701 branch decodes
///     <see cref="GmClearInventoryPayload" /> out of the shared envelope's tData blob before invoking this.
///     Operates exclusively on the invoking GM's own inventory -- never a named target's.
///     <para>
///         Once the permission precondition is met, this command cannot fail in any observable way: every
///         page-selector value (0 = first page only, 1 = second page only, anything else = both pages) results
///         in the shared success acknowledgment, with the success indicator set BEFORE the wipe below is even
///         attempted. For every occupied slot on the selected page(s): item-identity/quantity data and
///         socket/gem data are fully cleared; no per-slot notification of any kind is sent.
///     </para>
///     <para>
///         <b>Documented data-model divergence:</b> the source contract states the legacy expiration-timestamp
///         field survives a cleared slot unchanged (a residual value in a fixed per-slot array entry whose
///         item-id field was independently zeroed). Fenrir's own container model has no equivalent -- an
///         occupied slot is a single <see cref="Fenrir.Application.Game.Domain.Inventory.ItemStack" /> record in
///         a sparse per-container dictionary (empty slots are omitted entirely, not zero-filled with a
///         residual field), so "clearing" a slot here means removing its dictionary entry outright, including
///         its <see cref="Fenrir.Application.Game.Domain.Inventory.ItemStack.ExpireDate" />. There is no
///         persisted concept in this schema of an expiration value surviving independently of the item row it
///         was attached to. This is a deliberate, flagged behavioral difference, not a silent omission -- see
///         the implementation's own remarks.
///     </para>
///     <para>
///         Distinct from a separate, pre-existing plain-text GM chat command elsewhere in this codebase that
///         performs a functionally similar but not identical wipe (always both pages, also clears the
///         expiration-date field, sends one explicit per-slot update notification per cleared slot) -- these are
///         two independently implemented commands with materially different wipe semantics; this interface does
///         not share an implementation with that one.
///     </para>
/// </summary>
public interface IGmClearInventoryService
{
    /// <summary><paramref name="data" /> is the raw, unmodified 130-byte tData blob to echo back verbatim.</summary>
    public ValueTask HandleAsync(GmClearInventoryPayload packet, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken);
}

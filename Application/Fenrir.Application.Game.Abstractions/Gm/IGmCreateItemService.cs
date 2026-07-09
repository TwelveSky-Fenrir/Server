using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the Admin-tier (<c>GmCommandTier.Admin</c>) "spawn-item" GM command: legacy
///     PROCESS_DATA_SEND (opcode 19, <c>GenericActionRequest</c>) sub-commands 505 (base form) and 523
///     (explicit-quantity form), Server/ts25zone/S04_MyWork04.cpp:1036-1095 -- there is no dedicated legacy
///     wire opcode for this action; <c>GenericActionHandler</c>'s own tSort 505/523 branch is the only caller.
///     Owns every send/abort itself (rather than returning a Result for the handler to translate), same
///     posture as <c>IGmBlockAvatarService</c>, because the wire-level result-code semantics here are
///     unusually asymmetric: an unauthorized caller is disconnected outright with no reply, an id-range/
///     catalog-lookup failure sends the generic rejected code, a stackable-item (iSort 2/99) downstream
///     quantity-bound failure sends a distinct rejected code (legacy's own <c>tResult=2; break;</c> exiting
///     the outer switch directly for that branch), and every other outcome -- including the non-stackable
///     per-unit branch's own downstream creation failure -- sends the accepted code (a confirmed legacy
///     control-flow defect specific to that branch, which this type deliberately preserves rather than
///     silently "fixing," per the source behavior contract's own explicit flag on that point).
/// </summary>
public interface IGmCreateItemService
{
    /// <summary>
    ///     <paramref name="sort" /> is 505 or 523; <paramref name="data" /> is the raw, unmodified 130-byte tData blob to
    ///     decode and echo back.
    /// </summary>
    public ValueTask HandleAsync(int sort, byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone,
        CancellationToken cancellationToken);
}

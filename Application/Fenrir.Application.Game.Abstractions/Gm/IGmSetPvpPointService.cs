using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the Basic-tier (<c>GmCommandTier.Basic</c>, legacy <c>uUserSort &gt;= 1</c>)
///     "GM-SETPVPPOINT" command: legacy PROCESS_DATA_SEND (opcode 19, <c>GenericActionRequest</c>) sub-command
///     598, Server/ts25zone/S04_MyWork04.cpp:1755-1769 -- there is no dedicated legacy wire opcode for this
///     action; <c>GenericActionHandler</c>'s own tSort 598 branch decodes <see cref="GmSetPvpPointPayload" /> out
///     of the shared envelope's tData blob before invoking this. No target-character parameter exists in the
///     request.
///     <para>
///         <b>Confirmed functional no-op once validated:</b> the point-value input is transmitted by the client
///         but never read by any code path anywhere in the cited legacy source -- see
///         <see cref="GmSetPvpPointPayload.PointValue" />'s own remarks. When the duel-slot field validates (1 or
///         2), the ONLY effect is that the shared acknowledgment reports success; no character field, world
///         state, or persisted value of any kind is read or written. Do not invent a meaning for the point value
///         or add a mutation this contract does not describe.
///     </para>
/// </summary>
public interface IGmSetPvpPointService
{
    /// <summary><paramref name="data" /> is the raw, unmodified 130-byte tData blob to echo back verbatim.</summary>
    public ValueTask HandleAsync(GmSetPvpPointPayload packet, byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}

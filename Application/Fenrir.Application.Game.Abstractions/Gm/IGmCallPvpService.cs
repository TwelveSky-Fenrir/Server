using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the Basic-tier (<c>GmCommandTier.Basic</c>, legacy <c>uUserSort &gt;= 1</c>)
///     "GM-CALLPVP" command: legacy PROCESS_DATA_SEND (opcode 19, <c>GenericActionRequest</c>) sub-command 599,
///     Server/ts25zone/S04_MyWork04.cpp:1770-1823 -- there is no dedicated legacy wire opcode for this action;
///     <c>GenericActionHandler</c>'s own tSort 599 branch decodes <see cref="GmCallPvpPayload" /> out of the
///     shared envelope's tData blob before invoking this.
///     <para>
///         On a valid duel slot (1 or 2): the shared success acknowledgment is sent to the calling GM
///         IMMEDIATELY, before any relocation work runs -- not after it completes (see the "A14-gm-remaining"
///         behavior contract's own Side effects). The calling GM has no way to distinguish "a target was found
///         and moved" from "no player with that name is currently connected" from this response alone. Every
///         character currently connected to this server process (not just this GM's own zone/map) is then
///         examined; every character whose name is an EXACT, case-sensitive match (not the case-insensitive
///         comparison this dispatch table's sibling by-name commands use) is relocated to one of two fixed
///         coordinate triples, unless it is itself mid-connection/mid-zone-transfer or already engaged in
///         another duel-related state -- see <see cref="GmCallPvpPayload" />'s own remarks for the matching
///         semantics, and this interface's implementation for the two open/unrecoverable magnitudes this
///         contract does not supply (the fixed coordinate triples themselves, and the identity of a
///         per-character cooldown field the relocation incidentally resets).
///     </para>
///     <para>On an invalid duel slot: shared failure acknowledgment, no relocation work attempted.</para>
/// </summary>
public interface IGmCallPvpService
{
    /// <summary><paramref name="data" /> is the raw, unmodified 130-byte tData blob to echo back verbatim.</summary>
    public ValueTask HandleAsync(GmCallPvpPayload packet, byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}

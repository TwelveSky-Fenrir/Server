using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the GM-BLOCK command: legacy PROCESS_DATA_SEND (opcode 19, <c>GenericActionRequest</c>)
///     sub-command 519, Server/ts25zone/S04_MyWork04.cpp:1487-1515 -- there is no dedicated legacy wire opcode
///     for this action; <c>GenericActionHandler</c>'s own tSort 519 branch is the only caller, decoding
///     <see cref="GmBlockAvatarPayload" /> out of the shared envelope's tData blob before invoking this. Owns
///     every send/abort itself (rather than returning a Result for the handler to translate)
///     because legacy's own three outcomes are asymmetric and must stay that way: an unauthorized caller is
///     disconnected outright with no reply, a "target not found" (including self-targeting) sends legacy's real
///     shared opcode-23 generic-action ack (<c>GenericActionResponse</c>, Sort=519 --
///     Server/ts25zone/S04_MyWork04.cpp:2121-2122), and a successful block sends the caller nothing at all --
///     silence is the "it worked" signal on that last path.
/// </summary>
public interface IGmBlockAvatarService
{
    public ValueTask HandleAsync(GmBlockAvatarPayload packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}

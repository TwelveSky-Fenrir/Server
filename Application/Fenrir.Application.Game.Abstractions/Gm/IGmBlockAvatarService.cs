using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the GM-BLOCK command (Fenrir's dedicated wire command; legacy multiplexed this as
///     PROCESS_DATA_SEND sub-command 519, Server/ts25zone/S04_MyWork04.cpp:1487-1515). Owns every send/abort
///     itself (rather than returning a Result for the handler to translate) because legacy's own three outcomes
///     are asymmetric and must stay that way: an unauthorized caller is disconnected outright with no reply, a
///     "target not found" (including self-targeting) sends a generic failure ack, and a successful block sends
///     the caller nothing at all -- silence is the "it worked" signal on that last path (see
///     <c>GmBlockAvatarResponse</c>'s own remarks).
/// </summary>
public interface IGmBlockAvatarService
{
    public ValueTask HandleAsync(GmBlockAvatarRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}

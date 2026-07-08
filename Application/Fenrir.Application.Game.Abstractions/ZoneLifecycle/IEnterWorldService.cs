using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

/// <summary>
///     Business logic for op12, the world-entry handler. ZC_REGISTER_AVATAR_RECV carries no Result field, so
///     any anti-tamper failure here closes the socket rather than replying with a clean failure -- see
///     <c>EnterWorldHandler</c>'s own remarks. Owns every send/abort itself (rather than returning a Result for
///     the handler to translate) because success and failure are both threaded through many interleaved,
///     order-dependent session sends -- collapsing that into a single uniform result shape would restructure
///     control flow rather than merely relocate it.
/// </summary>
public interface IEnterWorldService
{
    public ValueTask HandleAsync(EnterWorldRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}

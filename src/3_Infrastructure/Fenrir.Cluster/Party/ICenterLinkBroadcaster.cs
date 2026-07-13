using Fenrir.Core.Abstractions;

namespace Fenrir.Cluster;

/// <summary>
///     Fans one Center → zone packet out to every currently-connected zone link. The legacy equivalent is the
///     "loop over all <c>SERVER_ZONE</c> connections and send" pattern the op57 (party) and op33 (world-event)
///     relays both use; there is no per-sender reply and the sender is NOT excluded from the fan-out.
///     <para>
///         DECLARED by this party unit (and, by the same name, by any other Center unit that needs a fan-out —
///         Main dedups). The IMPLEMENTATION belongs to Main / the CenterServer host, which owns the live set of
///         authenticated zone sessions; it should encode the frame once and copy it to each link
///         (rent-once/write-once/copy-N), isolating a per-link send failure so one dead link never aborts the
///         rest of the broadcast.
///     </para>
/// </summary>
public interface ICenterLinkBroadcaster
{
    /// <summary>Encodes <paramref name="packet" /> once and sends it to every connected zone link.</summary>
    void BroadcastToZones<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket;
}

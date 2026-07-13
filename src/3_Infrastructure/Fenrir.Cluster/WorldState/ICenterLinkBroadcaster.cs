namespace Fenrir.Cluster.WorldState;

/// <summary>
///     Fans an op33 world-event envelope out to every connected zone link. DECLARED here (minimal) and CONSUMED
///     by the op33 ingester (<c>CenterWorldEventIngestor</c>) and by the cadence hosts; the implementation is
///     owned by Main / the session-and-link unit that holds the live <c>CenterLinkSession</c> registry.
/// </summary>
/// <remarks>
///     Reimplemented from <c>BroadcastZone</c> (Server/ts25center/S07_MyGame01.cpp:278-288). Used for the
///     daily-reset (1600), FFA-refresh (1601) and tribe-point-sync (1234) notices, the op33 verbatim fan-out, and
///     the guild-buff removal notice. The 130-byte payload matches MAX_BROADCAST_DATA_SIZE.
///     <para>
///         COORDINATION: this is a DISTINCT abstraction from the party unit's
///         <c>Fenrir.Cluster.ICenterLinkBroadcaster</c> (which exposes a strongly-typed
///         <c>BroadcastToZones&lt;TPacket&gt;</c>). They live in different namespaces, so they do not collide at
///         compile time; Main should back BOTH with a single fan-out implementation (or unify them). This
///         world-event flavour is the one the op33 ingester already consumes, so its signature is fixed here.
///     </para>
/// </remarks>
public interface ICenterLinkBroadcaster
{
    /// <summary>
    ///     Sends an op33 envelope carrying <paramref name="sort" /> and <paramref name="data" /> (the verbatim
    ///     130-byte payload) to every authenticated zone link.
    /// </summary>
    ValueTask BroadcastWorldEventAsync(int sort, ReadOnlyMemory<byte> data, CancellationToken ct);
}

namespace Fenrir.Cluster.EventBus;

/// <summary>
///     Unicasts the op35 "close proxy" envelope (legacy <c>CZP_ZONE_CLOSE_PROXY_FOR_CENTER_RECV</c>,
///     <c>Server/ts25center/S04_MyWork02.cpp:1191-1211</c>) to the single zone link whose server number equals the
///     requested <c>zoneNumber</c>. This is the ONE op33 dispatch capability (<c>tSort == 10000</c>) that neither the
///     world-state fan-out (<c>WorldState.ICenterLinkBroadcaster</c>) nor the party fan-out
///     (<c>ICenterLinkBroadcaster.BroadcastToZones</c>) covers, because it is a targeted single-zone unicast keyed by
///     server number, not a fan-out.
///     <para>
///         COORDINATION: DECLARED (minimal) here by the op33-ingestor unit; the IMPLEMENTATION is owned by Main /
///         <c>CenterServerHost</c>, which holds the authenticated <c>CenterLinkSession</c> registry and can resolve a
///         zone link by its server number. Main should encode the op35 packet once and send it to the matching link;
///         if no connected zone matches <paramref name="zoneNumber" />, it is a silent no-op (legacy parity).
///     </para>
/// </summary>
public interface ICenterCloseProxyRelay
{
    /// <summary>
    ///     Sends the op35 close-proxy envelope carrying (<paramref name="zoneNumber" />, <paramref name="userIndex" />,
    ///     <paramref name="characterIndex" />, <paramref name="openUi" />) to the single zone whose server number is
    ///     <paramref name="zoneNumber" />. Silent no-op if no such zone is connected.
    /// </summary>
    public ValueTask SendCloseProxyAsync(int zoneNumber, int userIndex, int characterIndex, int openUi,
        CancellationToken cancellationToken);
}

using System.Net;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Transport.Logging;

/// <summary>
///     Operational log entries for the two swallowed-by-design fault paths inside
///     <see cref="FenrirTcpListener{TSession}" />
///     's accept loop and the one confirmed blind spot inside <see cref="SocketConnection" />'s send loop --
///     see each method's own remarks for exactly which of the several swallow-catches in this project got a
///     log call and which were deliberately left alone because the fault already resurfaces one layer up
///     (<c>FenrirTcpListener.AcceptLoopAsync</c>'s <c>ObjectDisposedException</c>/<c>OperationCanceledException</c>
///     branch, <c>FenrirTcpListener.RunAcceptedAsync</c>'s catch-all, and <c>SocketConnection.ReceiveLoopAsync</c>'s
///     catch -- none of those three get a method here).
///     <para>
///         <see cref="AcceptPortScanSwallowed" /> in particular can fire at a rate matching an active port scan
///         or connection-storm attempt -- exactly the kind of repeat-under-load event
///         <see cref="Fenrir.Network.Dispatch.Logging.PacketLog" /> exists to make cheap when disabled -- so
///         this class follows the same <c>[LoggerMessage]</c> source-gen shape for the same reason: a
///         hand-written <c>logger.LogDebug(...)</c> call would box its arguments into a <c>params object?[]</c>
///         array before the <c>IsEnabled</c> check ever runs, on every single swallowed accept exception,
///         regardless of whether Debug logging is enabled.
///     </para>
/// </summary>
internal static partial class TransportLog
{
    /// <summary>
    ///     Fires once per <see cref="System.Net.Sockets.SocketException" /> swallowed by
    ///     <c>FenrirTcpListener{TSession}.AcceptLoopAsync</c>'s per-accept catch -- a half-open port scan (or
    ///     any other transient accept failure that isn't the listen socket itself being torn down) surfaces
    ///     this way. Deliberately <see cref="LogLevel.Debug" />, not <see cref="LogLevel.Warning" />: this is
    ///     expected noise on any internet-facing listener, not an anomaly, and a scan can repeat fast enough
    ///     that a default-visible level would flood the log stream for no operational benefit.
    /// </summary>
    [LoggerMessage(
        EventId = 4201,
        EventName = "AcceptPortScanSwallowed",
        Level = LogLevel.Debug,
        Message =
            "Accept on {LocalEndPoint} failed with a transient socket error (half-open scan or similar) -- continuing the accept loop")]
    public static partial void AcceptPortScanSwallowed(this ILogger logger, Exception exception,
        EndPoint? localEndPoint);

    /// <summary>
    ///     Fires once per exception swallowed by <c>FenrirTcpListener{TSession}.AcceptLoopAsync</c>'s
    ///     construction catch -- thrown by <see cref="SocketConnection" />'s own constructor or by the caller's
    ///     <c>sessionFactory</c> delegate, strictly AFTER a socket was already successfully accepted. Unlike
    ///     <see cref="AcceptPortScanSwallowed" />, this is a genuine anomaly (a healthy accept that failed to
    ///     turn into a running session) worth <see cref="LogLevel.Warning" /> -- the accepted socket is disposed
    ///     right after this fires (see that catch block's own remarks), so the connection is cleanly gone, but
    ///     an operator watching live logs still needs to know a would-be connection never got a chance to run.
    /// </summary>
    [LoggerMessage(
        EventId = 4202,
        EventName = "ConnectionConstructionFailed",
        Level = LogLevel.Warning,
        Message =
            "Failed to construct a session for an accepted connection on {LocalEndPoint} -- the accepted socket is being disposed without ever running")]
    public static partial void ConnectionConstructionFailed(this ILogger logger, Exception exception,
        EndPoint? localEndPoint);

    /// <summary>
    ///     Fires once per exception swallowed by <see cref="SocketConnection" />'s <c>SendLoopAsync</c> --
    ///     unlike its sibling <c>ReceiveLoopAsync</c> (whose own captured fault reliably resurfaces through
    ///     <c>SessionLoop.RunAsync</c>'s <c>PipeReader.ReadAsync</c> on the RX pipe, then gets classified and
    ///     logged one layer up by <c>LoginConnectionHost</c>/<c>GameConnectionHost</c>'s own
    ///     <c>OnAcceptedAsync</c> catch block -- see that class's own remarks), this fault only ever resurfaces
    ///     inside <c>ClientSession.ObserveFlushAsync</c>'s own <c>catch (Exception)</c>, which discards it
    ///     silently by design (a failed flush must never fault the caller's <c>Send</c>/<c>SendRaw</c>) with no
    ///     log call anywhere on that path either. Confirmed via the exception-propagation contract change to
    ///     <c>System.IO.Pipelines</c> (a <c>PipeReader.Complete(exception)</c> call is rethrown from the paired
    ///     <c>PipeWriter</c>'s next <c>FlushAsync</c>/<c>WriteAsync</c>, fixed since .NET 5 --
    ///     see dotnet/runtime#43640): this method's own <paramref name="exception" /> IS the exact exception
    ///     object that eventually reaches (and is silently dropped by) that catch, so this is the one and only
    ///     place it can be surfaced. <see cref="LogLevel.Warning" />: a session whose send loop just faulted can
    ///     never successfully deliver another packet for the rest of its lifetime, silently, until something
    ///     else (the receive loop's own eventual fault, an idle sweep, ...) notices and tears it down.
    /// </summary>
    [LoggerMessage(
        EventId = 4203,
        EventName = "SendLoopFaulted",
        Level = LogLevel.Warning,
        Message =
            "Send loop faulted for connection {RemoteEndPoint} -- this connection can no longer send anything until an independent teardown path notices")]
    public static partial void SendLoopFaulted(this ILogger logger, Exception exception, EndPoint? remoteEndPoint);
}

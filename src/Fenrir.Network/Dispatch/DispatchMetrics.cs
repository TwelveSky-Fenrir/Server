using System.Diagnostics.Metrics;

namespace Fenrir.Network.Dispatch;

internal static class DispatchMetrics
{
    public const string MeterName = "Fenrir.Network.Dispatch";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> PacketsDispatched = Meter.CreateCounter<long>(
        "fenrir.dispatch.packets", null, "Number of frames successfully dispatched to a packet handler.");

    public static readonly Histogram<double> DispatchDurationMs = Meter.CreateHistogram<double>(
        "fenrir.dispatch.duration", "ms", "Duration of a single IFrameDispatcher.DispatchAsync call.");

    private static readonly KeyValuePair<string, object?> LoginServerTag = new("server", "Login");
    private static readonly KeyValuePair<string, object?> ZoneServerTag = new("server", "Zone");

    public static KeyValuePair<string, object?> ServerTag(FenrirServer server)
    {
        return server == FenrirServer.Login ? LoginServerTag : ZoneServerTag;
    }
}

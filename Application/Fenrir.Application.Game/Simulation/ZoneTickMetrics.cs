using System.Diagnostics.Metrics;

namespace Fenrir.Application.Game.Simulation;

/// <summary>OpenTelemetry instrumentation for the zone tick stages (drain -> simulate -> rebroadcast).</summary>
internal static class ZoneTickMetrics
{
    /// <summary>Meter name to register on the OpenTelemetry side (.AddMeter(...) in ServiceDefaults).</summary>
    public const string MeterName = "Fenrir.GameServer.Zone";

    public static readonly KeyValuePair<string, object?> DrainStage = new("stage", "drain");
    public static readonly KeyValuePair<string, object?> SimulateStage = new("stage", "simulate");
    public static readonly KeyValuePair<string, object?> RebroadcastStage = new("stage", "rebroadcast");

    private static readonly Meter Meter = new(MeterName);

    public static readonly Histogram<double> StageDurationMs = Meter.CreateHistogram<double>(
        "fenrir.zone.tick.stage.duration", "ms", "Duration of one zone tick stage");

    /// <summary>Boxes the map id once, not per record.</summary>
    public static KeyValuePair<string, object?> MapTag(short mapId)
    {
        return new KeyValuePair<string, object?>("map", (int)mapId);
    }
}

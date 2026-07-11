using System.Diagnostics.Metrics;

namespace Fenrir.Application.Game.Domain.Simulation;

internal static class ZoneTickMetrics
{

        public const string MeterName = "Fenrir.GameServer.Zone";

    public static readonly KeyValuePair<string, object?> DrainStage = new("stage", "drain");
    public static readonly KeyValuePair<string, object?> SimulateStage = new("stage", "simulate");
    public static readonly KeyValuePair<string, object?> RebroadcastStage = new("stage", "rebroadcast");

        public static readonly KeyValuePair<string, object?> PopulationIdleTag = new("population", "idle");

    public static readonly KeyValuePair<string, object?> PopulationActiveTag = new("population", "active");

    private static readonly Meter Meter = new(MeterName);

    public static readonly Histogram<double> StageDurationMs = Meter.CreateHistogram<double>(
        "fenrir.zone.tick.stage.duration", "ms", "Duration of one zone tick stage");

        public static KeyValuePair<string, object?> MapTag(short mapId)
    {
        return new KeyValuePair<string, object?>("map", (int)mapId);
    }
}

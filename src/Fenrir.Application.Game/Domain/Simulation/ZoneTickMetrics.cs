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

    public static readonly KeyValuePair<string, object?> CoreCommandQueueTag = new("queue", "core");

    private static readonly Meter Meter = new(MeterName);

    public static readonly Histogram<double> StageDurationMs = Meter.CreateHistogram<double>(
        "fenrir.zone.tick.stage.duration", "ms", "Duration of one zone tick stage");

    public static readonly Counter<long> Ticks = Meter.CreateCounter<long>(
        "fenrir.zone.tick.count", null, "Number of zone actor ticks.");

    public static readonly Histogram<double> TickDurationMs = Meter.CreateHistogram<double>(
        "fenrir.zone.tick.duration", "ms", "Duration of a complete zone actor tick.");

    public static readonly Counter<long> LateTicks = Meter.CreateCounter<long>(
        "fenrir.zone.tick.late", null, "Number of zone actor ticks that arrived after the simulation cadence.");

    public static readonly Histogram<double> TickLatenessMs = Meter.CreateHistogram<double>(
        "fenrir.zone.tick.lateness", "ms", "Amount by which a zone actor tick arrived after the simulation cadence.");

    public static readonly Histogram<long> CommandQueueDepth = Meter.CreateHistogram<long>(
        "fenrir.zone.command.queue.depth", "commands", "Observed depth of the zone core command queue.");

    public static readonly Histogram<double> CommandQueueAgeMs = Meter.CreateHistogram<double>(
        "fenrir.zone.command.queue.age", "ms", "Time spent waiting in the zone core command queue.");

    public static readonly Counter<long> CommandQueueRejections = Meter.CreateCounter<long>(
        "fenrir.zone.command.queue.rejections", "commands", "Commands rejected because the zone core command queue was full.");

    public static KeyValuePair<string, object?> MapTag(short mapId)
    {
        return new KeyValuePair<string, object?>("map", (int)mapId);
    }
}

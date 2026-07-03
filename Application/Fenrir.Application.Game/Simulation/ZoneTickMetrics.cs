using System.Diagnostics.Metrics;

namespace Fenrir.Application.Game.Simulation;

/// <summary>
///     OpenTelemetry-friendly instrumentation for the zone tick stages (drain → simulate → rebroadcast) — a
///     <see cref="System.Diagnostics.Metrics.Meter" /> is the AOT-safe, reflection-free way to surface these
///     (the OTLP export pipeline already wired by Fenrir.ServiceDefaults picks it up once
///     <c>AddMeter(ZoneTickMetrics.MeterName)</c> is added to its metrics configuration). Also the honest data
///     source for the directory heartbeat's <c>TickP99Ms</c>, which reports 0 ("not measured yet") until a
///     future milestone aggregates this histogram.
/// </summary>
internal static class ZoneTickMetrics
{
    /// <summary>Meter name to register on the OpenTelemetry side (<c>.AddMeter(...)</c> in ServiceDefaults).</summary>
    public const string MeterName = "Fenrir.GameServer.Zone";

    /// <summary>Tag value for the inbox-drain stage (Enter/Leave/Move command processing).</summary>
    public static readonly KeyValuePair<string, object?> DrainStage = new("stage", "drain");

    /// <summary>Tag value for the legacy-tick simulation stage (<see cref="ISimulationSystem" /> pass).</summary>
    public static readonly KeyValuePair<string, object?> SimulateStage = new("stage", "simulate");

    /// <summary>Tag value for the periodic keep-alive rebroadcast stage (avatars 3.5 s; monsters/items in Phase C).</summary>
    public static readonly KeyValuePair<string, object?> RebroadcastStage = new("stage", "rebroadcast");

    private static readonly Meter Meter = new(MeterName);

    /// <summary>
    ///     Wall-clock duration of one tick stage, tagged with <c>stage</c> (drain/simulate/rebroadcast) and
    ///     <c>map</c> (the zone's map id) so per-zone budgets remain distinguishable on a multi-map shard.
    /// </summary>
    public static readonly Histogram<double> StageDurationMs = Meter.CreateHistogram<double>(
        "fenrir.zone.tick.stage.duration", "ms", "Duration of one zone tick stage");

    /// <summary>Builds the per-zone <c>map</c> tag once (boxes the map id a single time, not per record).</summary>
    public static KeyValuePair<string, object?> MapTag(short mapId)
    {
        return new KeyValuePair<string, object?>("map", (int)mapId);
    }
}

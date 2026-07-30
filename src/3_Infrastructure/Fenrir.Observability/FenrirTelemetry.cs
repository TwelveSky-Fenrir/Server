using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Fenrir.Observability;

public static class FenrirTelemetry
{
    public const string NetworkMeterName = "Fenrir.Network";

    public const string GameMeterName = "Fenrir.Game";

    public const string ActivitySourceName = "Fenrir";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly Meter NetworkMeter = new(NetworkMeterName);

    public static readonly Meter GameMeter = new(GameMeterName);
}

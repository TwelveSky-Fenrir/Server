using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Fenrir.Observability;

/// <summary>
/// Sources d'instrumentation centralisées (<see cref="ActivitySource"/> + <see cref="Meter"/>). Home unique
/// (correctif F7). ⚠️ Ces meters doivent être <b>enregistrés</b> auprès d'OpenTelemetry (<c>AddMeter(...)</c>) —
/// le trou actuel est que le meter de tick existe mais n'est jamais exporté (cf. <c>Roadmap/FONDATIONS/08</c>).
/// Règle de sobriété : jamais d'<c>Activity</c> par paquet — le hot path est observé par métriques (histogrammes).
/// </summary>
public static class FenrirTelemetry
{
    /// <summary>Nom du meter réseau (ccu, packets/s, flood, latence de dispatch).</summary>
    public const string NetworkMeterName = "Fenrir.Network";

    /// <summary>Nom du meter jeu (tick, entités par zone, flush).</summary>
    public const string GameMeterName = "Fenrir.Game";

    /// <summary>Nom de la source d'activité (spans rares : login complet, consommation de ticket, flush, transaction).</summary>
    public const string ActivitySourceName = "Fenrir";

    /// <summary>Source de traces distribuées (opérations rares et longues uniquement).</summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>Meter des métriques réseau.</summary>
    public static readonly Meter NetworkMeter = new(NetworkMeterName);

    /// <summary>Meter des métriques de jeu.</summary>
    public static readonly Meter GameMeter = new(GameMeterName);
}

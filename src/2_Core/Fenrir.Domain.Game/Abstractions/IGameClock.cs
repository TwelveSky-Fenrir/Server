namespace Fenrir.Domain.Game.Abstractions;

/// <summary>
/// Horloge <b>injectée</b> : le domaine ne lit jamais <c>DateTime.Now</c> directement (testabilité + invariant
/// d'architecture). ⚠️ Rappel legacy : toute cadence/timeout se dérive du <b>temps réel</b> (millisecondes),
/// jamais d'un compteur de ticks (le legacy mêlait les deux via « 120 ticks = 1 min » codé en dur).
/// </summary>
public interface IGameClock
{
    /// <summary>Horloge murale en millisecondes UTC (base des throttles, timeouts, TTL).</summary>
    long UtcMilliseconds { get; }
}

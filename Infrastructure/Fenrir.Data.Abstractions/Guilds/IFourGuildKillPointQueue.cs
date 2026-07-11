namespace Fenrir.Data.Abstractions.Guilds;

/// <summary>
///     Non-blocking producer handle for the C17 Part B1 four-guild per-kill point accrual write-behind path
///     (<c>Application.Game.Services.Guilds.FourGuildScoringService.AccrueKillPointAsync</c>) -- the mirror
///     image of <c>IEventLogQueue</c>'s own Abstractions/Implementation split: there, <c>*.Services</c>
///     classes are the producers and a <c>*.Hosting</c> composition root drains into SQL; here, <c>Zone</c>
///     (<c>*.Domain</c>, on its own tick thread) is the producer and a <c>*.Services</c>-hosted background
///     drain loop is the consumer, because <c>Zone</c> cannot depend on
///     <c>FourGuildScoringService</c> directly (Domain -&gt; Services is a forbidden reverse dependency in
///     this codebase's project graph) and must never await a database round trip from its own tick.
///     Declared here, alongside <see cref="IFourGuildScoringRepository" />, for the same reason
///     <c>IEventLogQueue</c> is declared alongside <c>IEventLogRepository</c> in
///     <c>Fenrir.Data.Abstractions.Game</c> rather than next to either side's concrete implementation.
/// </summary>
public interface IFourGuildKillPointQueue
{
    /// <summary>
    ///     Non-blocking, synchronous enqueue of a +1 four-guild point credit for <paramref name="guildId" />.
    ///     Never awaits, never throws on backpressure. Returns false only if the bounded relay queue is
    ///     already full (sustained DB unavailability); the point is then dropped -- a hot per-kill call site
    ///     on a zone's own tick thread must never block on this call, matching
    ///     <c>FourGuildScoringService.AccrueKillPointAsync</c>'s own "a lost point never blocks the kill
    ///     pipeline" posture.
    /// </summary>
    public bool Enqueue(int guildId);
}

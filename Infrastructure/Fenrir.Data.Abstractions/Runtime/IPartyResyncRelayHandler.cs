namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Implemented by the <c>*.Services</c>-side party authority (<c>PartyResyncRelayHandler</c>, with access
///     to this shard's live <c>PartyRegistry</c>/<c>ZoneRegistry</c>) and registered in DI alongside it.
///     <c>PartyResyncRelayHost</c>'s own inbound poll routes every delivered row to this handler; it
///     deliberately does not reconcile anything itself (unlike its fan-out sibling
///     <c>GuildTribeBroadcastRelayHost</c>, which delivers guild/tribe chat directly), because party
///     reconciliation needs each shard's own in-memory membership table -- exactly the "route to a handler, do
///     not deliver generically" split its point-to-point sibling <c>SocialCrossShardRelayHost</c> uses. If a
///     given composition has no handler registered (e.g. a narrower test host), a delivered row is logged and
///     dropped instead, which costs nothing: same-shard party delivery is entirely unaffected.
/// </summary>
public interface IPartyResyncRelayHandler
{
    /// <summary>
    ///     Reconciles one delivered cross-shard party-resync row against this shard's own live party
    ///     membership. For a resync REQUEST (<see cref="PartyResyncRelaySort.Request" />, 108) whose claimed
    ///     party this shard hosts, the handler resolves the roster (up to five member names) and re-publishes a
    ///     party-info result / party-break, mirroring the legacy hub's case-108 reconciliation
    ///     (Server/ts25center/S04_MyWork02.cpp:1443-1494); for a delivered RESULT
    ///     (<see cref="PartyResyncRelaySort.PartyInfo" />/<see cref="PartyResyncRelaySort.PartyBreak" />), it
    ///     applies the roster/break to the locally-present subject character's party UI. A row for a party this
    ///     shard does not host is a no-op (logged, not an error).
    /// </summary>
    public ValueTask HandleAsync(PartyResyncRelayDto row, CancellationToken ct);
}

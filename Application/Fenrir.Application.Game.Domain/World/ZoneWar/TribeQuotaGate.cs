namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Which per-map tribe-population quota rule (if any) a zone process enforces on TEMP_REGISTER_SEND
///     (op11/ZoneHandshake) admission. Legacy dispatches this from a literal map-id switch/case list per zone
///     process (one process instance per map); Fenrir shards a disjoint set of maps per process instead, and
///     the character/map isn't even resolved yet at this point in the flow, so
///     <see cref="Domain.GameServerOptions.TribeQuotaGroup" /> models it as one operator-configured, per-shard
///     setting -- the same "Fenrir shards by map, not by numbered server instance" translation already used by
///     <see cref="Domain.GameServerOptions.Zone241DungeonMapIds" />/<see cref="Domain.GameServerOptions.HolyStoneTerritoryMapIds" />,
///     just collapsed to a single flag rather than a map-id set since no map identity exists yet to key one by.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:572-744 (full handler body, both quota branches, dead third
///     branch) ; :596-654 (three-way branch, valid declared-tribe range 0-2) ; :637-695 (four-way branch, valid
///     declared-tribe range 0-3; the extra zone-state sub-check at :684-694 is gated by <c>USE_ZONE_200</c>,
///     never <c>#define</c>d anywhere in <c>Server/</c> -- dead code in every build) ; :696-726 (third,
///     instanced/RvR-adjacent map group -- its own population/state check at :717-724 is wrapped in
///     <c>#ifndef USE_INSTANCE</c>, and Server/ts25zone/H01_MainApplication.h:5 <c>#define</c>s
///     <c>USE_INSTANCE</c>, so that check never runs; Server/ts25zone/H07_MyGame.h:30-31's <c>ZONE241</c>/
///     <c>ZONE241_V2</c> confirm the surrounding map-identifier case list itself is still compiled in).
/// </remarks>
public enum TribeQuotaGroup : byte
{
    /// <summary>
    ///     No population/state gate at all: any declared tribe is accepted and admission always proceeds. Also
    ///     what applies to any map not listed in any group.
    /// </summary>
    None = 0,

    /// <summary>Three-way tribe split: declared tribe must be 0-2; each tribe capped at Capacity/3 temp-registered connections.</summary>
    ThreeWay = 1,

    /// <summary>Four-way tribe split: declared tribe must be 0-3; each tribe capped at Capacity/4 temp-registered connections.</summary>
    FourWay = 2,

    /// <summary>
    ///     Instanced/RvR-adjacent maps (the Zone241 family): the legacy dispatch recognizes these maps, but the
    ///     entry gate itself is compiled out (see this type's own remarks) -- functionally identical to
    ///     <see cref="None" />, kept as a distinct, documented value rather than silently merged with "no group
    ///     configured at all".
    /// </summary>
    RvrInstancedNoGate = 3
}

/// <summary>Result of evaluating a TEMP_REGISTER_SEND attempt's tribe-population quota (see <see cref="TribeQuotaGate" />).</summary>
public enum TribeQuotaOutcome
{
    /// <summary>Population for the declared tribe is below (or at, per <see cref="TribeQuotaGate" />'s remarks) the threshold -- entry proceeds.</summary>
    Accepted,

    /// <summary>
    ///     The declared tribe's currently-temp-registered population already meets the group's threshold -- a
    ///     generic rejection response is sent and the connection stays open, retry-able.
    /// </summary>
    QuotaFull
}

/// <summary>
///     Pure decision logic for the TEMP_REGISTER_SEND tribe-population quota gate -- see <see cref="TribeQuotaGroup" />'s
///     own remarks for the map-group translation and citations.
/// </summary>
public static class TribeQuotaGate
{
    /// <summary>
    ///     Whether <paramref name="declaredTribe" /> falls inside the valid range for <paramref name="group" />.
    ///     Out of range is a protocol violation (the caller must drop the connection, not send a response) --
    ///     distinct from <see cref="TribeQuotaOutcome.QuotaFull" />, which is a normal, retry-able rejection.
    ///     <see cref="TribeQuotaGroup.None" />/<see cref="TribeQuotaGroup.RvrInstancedNoGate" /> enforce no range
    ///     at all (their dispatch performs no check of any kind), so every declared tribe is considered in range.
    /// </summary>
    public static bool IsDeclaredTribeInRange(TribeQuotaGroup group, int declaredTribe)
    {
        return group switch
        {
            TribeQuotaGroup.ThreeWay => declaredTribe is >= 0 and <= 2,
            TribeQuotaGroup.FourWay => declaredTribe is >= 0 and <= 3,
            _ => true
        };
    }

    /// <summary>
    ///     Evaluates the population threshold for an already-range-valid declared tribe. Callers must have
    ///     already checked <see cref="IsDeclaredTribeInRange" /> -- this method only decides Accepted vs.
    ///     <see cref="TribeQuotaOutcome.QuotaFull" />, never a range violation.
    /// </summary>
    /// <param name="group">Which quota rule this shard enforces.</param>
    /// <param name="maxConcurrentConnections">
    ///     This zone process's configured maximum concurrent connections (<see cref="Domain.GameServerOptions.Capacity" />)
    ///     -- the numerator the per-tribe cap is derived from (Capacity/3 for <see cref="TribeQuotaGroup.ThreeWay" />,
    ///     Capacity/4 for <see cref="TribeQuotaGroup.FourWay" />), using the same truncating integer division as legacy.
    /// </param>
    /// <param name="currentPopulationForDeclaredTribe">
    ///     How many connections are already recorded (by their upstream-resolved, not declared, tribe -- see
    ///     <see cref="TribeQuotaRegistry" />'s own remarks) under the tribe this attempt is declaring.
    /// </param>
    public static TribeQuotaOutcome Evaluate(TribeQuotaGroup group, int maxConcurrentConnections,
        int currentPopulationForDeclaredTribe)
    {
        var divisor = group switch
        {
            TribeQuotaGroup.ThreeWay => 3,
            TribeQuotaGroup.FourWay => 4,
            _ => 0
        };

        if (divisor == 0)
            return TribeQuotaOutcome.Accepted;

        var threshold = maxConcurrentConnections / divisor;
        return currentPopulationForDeclaredTribe >= threshold ? TribeQuotaOutcome.QuotaFull : TribeQuotaOutcome.Accepted;
    }
}

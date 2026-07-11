using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Fixed per-map identity/geometry for one Zone195 "Nok-San" stone shard -- which stone slot this map
///     represents, the legacy server number that identifies it in the client-facing broadcasts, and the fixed
///     capture-post location plus its <see cref="Zone195NokSanState.DefaultCaptureRadius" /> radius. None of
///     the concrete post coordinates are recoverable from the translated behavior contract (only that a "fixed
///     point" and a 12.5-unit radius exist, Server/ts25zone/S07_MyGame01.cpp:1148-1151), so -- exactly like
///     <see cref="HolyStoneWarSite" /> -- these must be supplied by operator configuration rather than guessed.
/// </summary>
/// <param name="MapId">The hosted map id this Nok-San instance runs on.</param>
/// <param name="StoneSlotIndex">
///     Which <see cref="Zone195NokSanState" /> owner-array slot (0-8) this map's stone occupies. The legacy
///     ships exactly three: server 196 -> slot 0, server 99 -> slot 2, server 100 -> slot 3
///     (Server/ts25zone/S07_MyGame01.cpp:1140-1176; the fourth server-195 -> slot-0 mapping is commented-out
///     dead code and does not ship).
/// </param>
/// <param name="LegacyServerNumber">
///     The legacy server number (196/99/100) the client-facing broadcasts carry to identify which stone -- see
///     <see cref="IZone195NokSanBroadcaster" />. Purely a broadcast-payload value; no gameplay logic branches
///     on it (the reward window keys on <see cref="IsRewardWindowShard" /> instead).
/// </param>
/// <param name="PostX">Fixed capture-post X (operator-configured; unrecoverable from the contract).</param>
/// <param name="PostZ">Fixed capture-post Z (operator-configured; unrecoverable from the contract).</param>
/// <param name="CaptureRadius">
///     The capture-spot radius; defaults to <see cref="Zone195NokSanState.DefaultCaptureRadius" />
///     (12.5).
/// </param>
public sealed record Zone195NokSanSite(
    short MapId,
    int StoneSlotIndex,
    short LegacyServerNumber,
    float PostX,
    float PostZ,
    float CaptureRadius = Zone195NokSanState.DefaultCaptureRadius)
{
    /// <summary>
    ///     Whether this shard is the one the LNW33 time-bonus reward window can ever open on. Only ever true
    ///     for stone slot 0 -- the legacy "server 196" -- since the recompute pass is hard-gated to that one
    ///     server (Server/ts25zone/S07_MyGame01.cpp:274,294-305). Servers 99/100 (slots 2/3) flip stones and
    ///     broadcast state but never grant the CP/hero-point reward.
    /// </summary>
    public bool IsRewardWindowShard => StoneSlotIndex == 0;
}

/// <summary>
///     Resolves the <see cref="Zone195NokSanSite" /> for a hosted map, if any. Built once and never mutated
///     after -- a <see cref="FrozenDictionary{TKey,TValue}" /> keyed by map id, read from any zone's tick
///     thread with no lock (same immutable-after-boot posture as <see cref="ZoneRegistry" />). Empty by
///     default (<see cref="Empty" />), matching every other operator-configured map-id set in this cluster
///     (<c>GameServerOptions.Zone195MapIds</c>, <see cref="AntiCampingGuardPointCatalog.Empty" />): a map is
///     never a Nok-San capture shard until an operator configures its site, so the feature is fully dormant
///     until then.
/// </summary>
public sealed class Zone195NokSanSiteCatalog
{
    /// <summary>The dormant default: no Nok-San capture shard on any map. See class remarks.</summary>
    public static readonly Zone195NokSanSiteCatalog Empty = new([]);

    private readonly FrozenDictionary<short, Zone195NokSanSite> _byMapId;

    public Zone195NokSanSiteCatalog(IEnumerable<Zone195NokSanSite> sites)
    {
        _byMapId = sites.ToFrozenDictionary(static site => site.MapId);
    }

    public bool TryGet(short mapId, out Zone195NokSanSite? site)
    {
        return _byMapId.TryGetValue(mapId, out site);
    }
}

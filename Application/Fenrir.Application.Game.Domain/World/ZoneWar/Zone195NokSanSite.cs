using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Fixed per-map identity/geometry for one Zone195 "Nok-San" stone shard -- which stone slot this map
///     represents, the legacy server number that identifies it in the client-facing broadcasts, and the fixed
///     capture-post location plus its <see cref="Zone195NokSanState.DefaultCaptureRadius" /> radius. The
///     capture-post X/Z coordinates are RECOVERED, fixed legacy world constants -- one shared literal pair
///     written unconditionally at Zone195 boot-time state initialization, before the per-server-number switch
///     that assigns the stone-slot index, so the same point applies identically to all three live shards
///     (Server/ts25zone/S07_MyGame01.cpp:1148-1151; axis order independently re-derived, not assumed -- see
///     <see cref="Zone195NokSanState.DefaultPostX" />/<see cref="Zone195NokSanState.DefaultPostZ" />). Only
///     <see cref="MapId" /> (which of an operator's own hosted maps runs this shard) and the resulting
///     <see cref="StoneSlotIndex" />/<see cref="LegacyServerNumber" /> pairing remain operator-supplied --
///     exactly like <see cref="HolyStoneWarSite" />'s own per-deployment map assignment -- since Fenrir has no
///     fixed map-id-to-shard convention of its own to hardcode.
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
/// <param name="PostX">
///     Fixed capture-post world-space X, -20.0 (Server/ts25zone/S07_MyGame01.cpp:1148); identical across all
///     three live shards. Defaults to <see cref="Zone195NokSanState.DefaultPostX" />.
/// </param>
/// <param name="PostZ">
///     Fixed capture-post world-space Z, 2510.0 (Server/ts25zone/S07_MyGame01.cpp:1150); identical across all
///     three live shards. Defaults to <see cref="Zone195NokSanState.DefaultPostZ" />.
/// </param>
/// <param name="CaptureRadius">
///     The capture-spot radius; defaults to <see cref="Zone195NokSanState.DefaultCaptureRadius" />
///     (12.5).
/// </param>
public sealed record Zone195NokSanSite(
    short MapId,
    int StoneSlotIndex,
    short LegacyServerNumber,
    float PostX = Zone195NokSanState.DefaultPostX,
    float PostZ = Zone195NokSanState.DefaultPostZ,
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
///     (<c>GameServerOptions.Zone195MapIds</c>): a map is never a Nok-San capture shard until an operator
///     configures its site, so the feature is fully dormant until then -- unlike
///     <see cref="AntiCampingGuardPointCatalog" />, whose per-map coordinates are now recovered legacy data
///     (<see cref="AntiCampingGuardPointCatalog.Default" />), not operator configuration.
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

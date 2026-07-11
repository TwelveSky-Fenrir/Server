using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.Configuration;

/// <summary>
///     Boot-frozen, read-only lookup of every configured zone's <see cref="ZoneConfig" />, keyed by map id --
///     the C# home for the A7 "Per-zone configuration and data tables" contract's frozen-at-boot surface. Built
///     exactly once at startup from an <c>IOptions</c>/appsettings-bound per-zone table and never mutated
///     afterward, so every zone actor and handler reads it lock-free for the process lifetime (same posture as
///     <c>WorldDataCache</c> and <see cref="Combat.ZonePvpZoneCatalog" />).
/// </summary>
/// <remarks>
///     <para>
///         The read accessors reproduce the legacy accessors' defensive out-of-range/unconfigured defaults
///         exactly (<c>Server/Header/S18_MyZoneInfo.cpp:399-433</c>): a map id that is not configured -- including
///         one outside the valid 1..350 zone-number range, which can never appear in a live Fenrir
///         <see cref="Zone" /> -- yields a neutral 0 from the level accessors and <see cref="ZoneConfig.NoOwnerTribe" />
///         (-1) from the owner accessor, silently and defensively, never as an error.
///     </para>
///     <para>
///         Freeze-at-boot semantics: both legacy freeze steps (the compiled <c>ZONEMAININFO::Init</c> table and
///         the INI read) must complete before the zone accepts its first connection or runs its first tick, since
///         every consumer reads these frozen values rather than re-deriving them (A7 contract, Side effects). In
///         Fenrir that ordering is a boot-sequence concern (Program.cs / DI registration) -- see this workstream's
///         wiring notes; this catalog only owns the frozen data structure and its accessors.
///     </para>
///     <para>
///         The validation helpers (<see cref="IsValidZoneNumber" />, <see cref="IsValidMaxUser" />) carry the
///         legacy boot-abort ranges (zone number 1..350; max-user 1..1000). Note the max-user guard is 1..1000
///         even though the legacy operator-facing error text misworded the range as "1-100"
///         (<c>Server/Header/ini.h:300-303</c>) -- the guard, not the message, is authoritative. Whether an
///         out-of-range value actually aborts boot is a wiring decision (a validator/boot step), not something
///         this data-holding catalog enforces on its own.
///     </para>
/// </remarks>
public sealed class ZoneConfigCatalog
{
    /// <summary>Lowest valid legacy zone number (<c>Server/Header/Protocol/DEFINE.h:373-375</c>, <c>mapcheck.h:74-78</c>).</summary>
    public const int MinValidZoneNumber = 1;

    /// <summary>Highest valid legacy zone number, <c>MAX_ZONE_NUMBER_NUM</c> (<c>DEFINE.h:373-375</c>).</summary>
    public const int MaxValidZoneNumber = 350;

    /// <summary>Lowest max-user cap the legacy INI guard accepts before aborting boot (<c>ini.h:297-304</c>).</summary>
    public const int MinMaxUser = 1;

    /// <summary>
    ///     Highest max-user cap the legacy INI guard accepts before aborting boot (<c>ini.h:297-304</c>). The
    ///     legacy error text misstates this as 100; 1000 is the actual enforced ceiling.
    /// </summary>
    public const int MaxMaxUser = 1000;

    private readonly FrozenDictionary<int, ZoneConfig> _byMapId;

    /// <summary>
    ///     Freezes the supplied per-zone entries into the lookup. Duplicate map ids resolve last-write-wins (an
    ///     operator misconfiguration, not a wire input); an <c>IReadOnlyDictionary</c> naturally supplies unique
    ///     keys.
    /// </summary>
    public ZoneConfigCatalog(IEnumerable<KeyValuePair<int, ZoneConfig>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var staging = new Dictionary<int, ZoneConfig>();
        foreach (var (mapId, config) in entries)
            staging[mapId] = config;

        _byMapId = staging.ToFrozenDictionary();
    }

    /// <summary>An empty catalog -- every accessor returns its neutral default. The safe boot-time fallback.</summary>
    public static ZoneConfigCatalog Empty { get; } = new(Array.Empty<KeyValuePair<int, ZoneConfig>>());

    /// <summary>Number of configured zones.</summary>
    public int Count => _byMapId.Count;

    /// <summary>
    ///     True and yields the frozen <see cref="ZoneConfig" /> when <paramref name="mapId" /> is configured;
    ///     false with <see cref="ZoneConfig.Unconfigured" /> otherwise.
    /// </summary>
    public bool TryGet(int mapId, out ZoneConfig config)
    {
        if (_byMapId.TryGetValue(mapId, out config))
            return true;

        config = ZoneConfig.Unconfigured;
        return false;
    }

    /// <summary>
    ///     The configured <see cref="ZoneConfig" /> for <paramref name="mapId" />, or
    ///     <see cref="ZoneConfig.Unconfigured" /> (level 0-0, owner -1, tunables 0) when unconfigured -- matching
    ///     the legacy per-slot default an unmatched zone keeps.
    /// </summary>
    public ZoneConfig GetOrDefault(int mapId)
    {
        return _byMapId.TryGetValue(mapId, out var config) ? config : ZoneConfig.Unconfigured;
    }

    /// <summary>Minimum level for <paramref name="mapId" />; 0 for an unconfigured/out-of-range zone (S18:399-433).</summary>
    public int GetMinLevel(int mapId)
    {
        return _byMapId.TryGetValue(mapId, out var config) ? config.MinLevel : 0;
    }

    /// <summary>Maximum level for <paramref name="mapId" />; 0 for an unconfigured/out-of-range zone (S18:399-433).</summary>
    public int GetMaxLevel(int mapId)
    {
        return _byMapId.TryGetValue(mapId, out var config) ? config.MaxLevel : 0;
    }

    /// <summary>
    ///     Owner/faction code for <paramref name="mapId" />; <see cref="ZoneConfig.NoOwnerTribe" /> (-1) for an
    ///     unconfigured/out-of-range zone (S18:399-433).
    /// </summary>
    public int GetOwnerTribe(int mapId)
    {
        return _byMapId.TryGetValue(mapId, out var config) ? config.OwnerTribe : ZoneConfig.NoOwnerTribe;
    }

    /// <summary>Secondary classification for <paramref name="mapId" />; 0 for an unconfigured/out-of-range zone.</summary>
    public int GetSecondaryClassification(int mapId)
    {
        return _byMapId.TryGetValue(mapId, out var config) ? config.SecondaryClassification : 0;
    }

    /// <summary>
    ///     The hard max-user cap for <paramref name="mapId" />; 0 ("no per-zone cap configured", fail-open) for an
    ///     unconfigured zone. See <see cref="ZoneConfig.MaxUser" /> for why 0 means unlimited here rather than the
    ///     legacy boot-abort case.
    /// </summary>
    public int GetMaxUser(int mapId)
    {
        return _byMapId.TryGetValue(mapId, out var config) ? config.MaxUser : 0;
    }

    /// <summary>
    ///     Whether <paramref name="level" /> is within <paramref name="mapId" />'s configured level band -- the
    ///     data surface behind the legacy "can this character enter this battle zone" gate
    ///     (<c>CheckLevelBattleZoneNumber</c>, <c>Server/Header/S18_MyZoneInfo.cpp:440-508</c>). A zone with no
    ///     configured band (unconfigured, or <see cref="ZoneConfig.MaxLevel" /> == 0, the legacy per-slot 0-0
    ///     default) is treated as ungated (always eligible); otherwise the check is inclusive
    ///     <c>MinLevel &lt;= level &lt;= MaxLevel</c>.
    /// </summary>
    /// <remarks>
    ///     The EXACT legacy comparison in <c>CheckLevelBattleZoneNumber</c> (S18:440-508) -- inclusivity at each
    ///     bound and its handling of the 0-0 default -- is not spelled out by the A7 contract (which states only
    ///     that the check "reads the frozen min/max via the accessors"). This straightforward inclusive
    ///     band-with-ungated-0-0 reading is flagged in this workstream's deferred notes; confirm it against a
    ///     dedicated <c>CheckLevelBattleZoneNumber</c> finding before relying on it for a real level gate.
    /// </remarks>
    public bool IsWithinLevelBand(int mapId, int level)
    {
        if (!_byMapId.TryGetValue(mapId, out var config) || config.MaxLevel == 0)
            return true;

        return level >= config.MinLevel && level <= config.MaxLevel;
    }

    /// <summary>
    ///     The legacy valid-zone-number boot guard (1..350 inclusive, <c>mapcheck.h:74-78</c> /
    ///     <c>DEFINE.h:373-375</c>). A zone number outside this range aborts legacy boot; a boot-sequence
    ///     validator (not this catalog) owns that abort.
    /// </summary>
    public static bool IsValidZoneNumber(int zoneNumber)
    {
        return zoneNumber is >= MinValidZoneNumber and <= MaxValidZoneNumber;
    }

    /// <summary>
    ///     The legacy max-user boot guard (1..1000 inclusive, <c>ini.h:297-304</c>). A value outside this range
    ///     aborts legacy boot -- authoritative over the misworded "1-100" operator message. A boot-sequence
    ///     validator (not this catalog) owns that abort.
    /// </summary>
    public static bool IsValidMaxUser(int maxUser)
    {
        return maxUser is >= MinMaxUser and <= MaxMaxUser;
    }
}

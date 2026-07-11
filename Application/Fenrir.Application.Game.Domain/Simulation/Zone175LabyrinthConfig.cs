using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     One Zone175-type ("Labyrinth") instance's configuration, the Fenrir equivalent of the legacy's
///     server-number-to-<c>(index1, index2)</c> static mapping plus the configured experience/money ratios.
/// </summary>
/// <param name="Index1">
///     Selects the mission row in the shared cross-process world-info table
///     (<c>Server/ts25zone/S07_MyGame01.cpp:924-943</c>).
/// </param>
/// <param name="Index2">
///     Selects the cell AND doubles as the instance's configured depth cap: several inter-wave gates require
///     <c>Index2</c> to be at least the wave number being entered (observed values 0-4, see
///     <see cref="Zone175RewardTables.CanAdvanceToNextWave" />).
/// </param>
/// <param name="ExperienceRatio">Scales the wave-clear experience (currently a no-op -- the base table is a GAP).</param>
/// <param name="MoneyRatio">
///     Configured money ratio -- present for parity but has <b>no surviving effect</b>: the fixed 100M/200M
///     money table unconditionally overrides the ratio computation (<c>S07_MyGame01.cpp:8645-8672</c>). Carried
///     only so the config shape matches the legacy inputs.
/// </param>
public readonly record struct Zone175InstanceConfig(
    int Index1,
    int Index2,
    float ExperienceRatio,
    float MoneyRatio);

/// <summary>
///     Which hosted maps are Zone175-type ("Labyrinth") servers and, for each, its
///     <see cref="Zone175InstanceConfig" />. The Fenrir equivalent of legacy's boot-time
///     <c>mCheckZone175TypeServer</c> arm (set only for dedicated Labyrinth server numbers 175+, each statically
///     mapped to a fixed <c>(index1, index2)</c> pair, <c>Server/ts25zone/S07_MyGame01.cpp:915-943</c>).
/// </summary>
/// <remarks>
///     Built once at boot from <see cref="GameServerOptions" /> (see this class's wiring note in the workstream
///     report) and never mutated after, so it is safe to read from any zone's tick thread with no
///     synchronization -- same posture as <c>GameServerOptions.Zone241DungeonMapIds</c>. Defaults to
///     <see cref="Disabled" /> (no configured maps -> <see cref="Zone175LabyrinthSystem" /> is a total no-op)
///     until Hosting supplies the real catalog, mirroring how <c>Zone.PersonalDungeonBossCatalog</c> defaults to
///     a Null instance until wired.
///     <para>
///         <b>GAP:</b> the concrete server-number-to-<c>(index1, index2)</c> table is only partially recovered
///         by the source contract ("the full server-number-to-index table beyond the first entries read" is
///         flagged not-observed). The catalog is therefore populated from configuration rather than a hardcoded
///         table; the concrete map ids and index pairs must come from a <c>cpp-zone-gameplay-analyst</c>
///         follow-up before any live map is enabled.
///     </para>
/// </remarks>
public sealed class Zone175LabyrinthConfig
{
    /// <summary>An empty catalog: every map is treated as non-Zone175-type, so the system does nothing.</summary>
    public static readonly Zone175LabyrinthConfig Disabled = new(FrozenDictionary<short, Zone175InstanceConfig>.Empty);

    private readonly FrozenDictionary<short, Zone175InstanceConfig> _byMapId;

    public Zone175LabyrinthConfig(IReadOnlyDictionary<short, Zone175InstanceConfig> byMapId)
    {
        _byMapId = byMapId as FrozenDictionary<short, Zone175InstanceConfig> ?? byMapId.ToFrozenDictionary();
    }

    /// <summary>How many maps are configured as Zone175-type.</summary>
    public int Count => _byMapId.Count;

    /// <summary>True (with <paramref name="config" /> set) when <paramref name="mapId" /> is a Zone175-type map.</summary>
    public bool TryGet(short mapId, out Zone175InstanceConfig config)
    {
        return _byMapId.TryGetValue(mapId, out config);
    }
}

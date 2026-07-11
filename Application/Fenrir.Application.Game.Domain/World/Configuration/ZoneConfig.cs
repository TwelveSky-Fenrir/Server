namespace Fenrir.Application.Game.Domain.World.Configuration;

/// <summary>
///     Immutable, boot-frozen per-zone configuration snapshot -- the C# reimplementation of the two independent
///     legacy freeze steps the A7 "Per-zone configuration and data tables" behavior contract describes:
///     <list type="bullet">
///         <item>
///             the compiled per-zone level-band and owner-tribe table baked into <c>ZONEMAININFO::Init</c>
///             (<c>Server/Header/S18_MyZoneInfo.cpp:9-393</c>, array shape
///             <c>Server/Header/H18_MyZoneInfo.h:6-7</c>) -- <see cref="MinLevel" />/<see cref="MaxLevel" />,
///             <see cref="OwnerTribe" />, <see cref="SecondaryClassification" />;
///         </item>
///         <item>
///             the zone build-target INI freeze of this zone's hard max-user cap and its ~34 <c>[Zone.Server]</c>
///             gameplay tunables (<c>Server/Header/ini.h:273-347</c>) -- <see cref="MaxUser" /> plus the
///             experience/drop/kill/war ratio fields below.
///         </item>
///     </list>
///     Every value here is established from server-side sources ONLY (a compiled constant, or the server's own
///     config) and is frozen for the whole process lifetime -- no client packet can ever supply or override any
///     of it (A7 contract, "Client can never supply or override any of these values").
/// </summary>
/// <remarks>
///     <para>
///         A <c>readonly record struct</c>: value semantics, structural equality for free, no per-lookup heap
///         allocation when read out of <see cref="ZoneConfigCatalog" />'s <c>FrozenDictionary</c>. Populated once
///         at boot from an <c>IOptions</c>/appsettings-bound per-zone table (see <see cref="ZoneConfigCatalog" />)
///         and never mutated afterward.
///     </para>
///     <para>
///         <b>Deliberate omissions (avoid a second source of truth):</b>
///         <list type="bullet">
///             <item>
///                 The <c>[Zone.Server]</c> vote / alliance-tribe / holy-stone-battle enable TOGGLES the A7
///                 contract also lists (<c>Server/Header/ini.h:306-347</c>) are already modeled as map-id-keyed
///                 arming flags on <see cref="GameServerOptions" /> (<c>VoteTribeEnabled</c>,
///                 <c>AllianceTribeEnabled</c>, <c>HolyStoneBattleEnabled</c>, each citing the same ini.h keys) --
///                 they are intentionally NOT duplicated here. Only the tunable RATIO/VALUE fields live on this
///                 record.
///             </item>
///             <item>
///                 <see cref="SecondaryClassification" /> is the legacy <c>mZoneTribeInfo[id-1][1]</c> column,
///                 whose "enemy-tribe PvP authorized here" reading is already extracted verbatim (all 89 nonzero
///                 zone ids) in <see cref="Combat.ZonePvpZoneCatalog" />. That catalog remains the authoritative
///                 extracted source for the PvP-enable reading of this column; this field exists so an operator
///                 CAN carry the raw value per zone, but its per-zone data is not re-baked here.
///             </item>
///         </list>
///     </para>
///     <para>
///         <b>Unrecovered legacy data (flagged, not invented):</b> the actual 350 per-zone
///         <see cref="MinLevel" />/<see cref="MaxLevel" />/<see cref="OwnerTribe" /> values, the real per-zone
///         <see cref="MaxUser" /> caps, and the exact <c>[Zone.Server]</c> ratio defaults/scales (e.g. whether a
///         ratio of 100 means 1.0x) are NOT enumerated in the A7 contract -- it describes the SHAPE, not the
///         numbers. This type therefore ships with neutral CLR-default values (0/false, except
///         <see cref="OwnerTribe" /> whose contract default is <see cref="NoOwnerTribe" />); real values arrive
///         from appsettings, and the compiled level-band/owner numbers need a dedicated legacy extraction pass
///         (per the contract's own "flag for cpp-protocol-header-analyst/cpp-ts25-explorer" note) before the
///         level-band and owner columns carry anything but the neutral default.
///     </para>
/// </remarks>
public readonly record struct ZoneConfig
{
    /// <summary>
    ///     Owner/faction sentinel meaning "no owner" -- the legacy per-slot default applied before the selector
    ///     runs (<c>Server/Header/S18_MyZoneInfo.cpp:15-18</c>) and the value the accessors yield for a zone
    ///     number outside 1..350 (<c>S18_MyZoneInfo.cpp:399-433</c>).
    /// </summary>
    public const int NoOwnerTribe = -1;

    // ---- Compiled per-zone level-band + owner-tribe table (S18_MyZoneInfo.cpp:9-393) ----

    /// <summary>
    ///     Minimum level for this zone. Legacy per-slot default 0 (<c>S18_MyZoneInfo.cpp:15-18</c>); several zone
    ///     entries express this via symbolic level-tier constants (values 113..145,
    ///     <c>Server/Header/Protocol/DEFINE.h:451-483</c>) rather than inline literals.
    /// </summary>
    public int MinLevel { get; init; }

    /// <summary>Maximum level for this zone. Legacy per-slot default 0 (<c>S18_MyZoneInfo.cpp:15-18</c>).</summary>
    public int MaxLevel { get; init; }

    /// <summary>
    ///     Owner/faction code for this zone. Legacy per-slot default <see cref="NoOwnerTribe" />
    ///     (<c>S18_MyZoneInfo.cpp:15-18</c>). Range in the legacy table is BROADER than the 0..3 playable-tribe
    ///     range -- it holds 0..7, 10..16, and -1 (<c>S18_MyZoneInfo.cpp:21-388</c>); the full semantics of the
    ///     higher codes are not established by the A7 contract and are flagged there for re-check before assigning
    ///     them meaning.
    /// </summary>
    public int OwnerTribe { get; init; }

    /// <summary>
    ///     The legacy <c>mZoneTribeInfo[id-1][1]</c> secondary classification column. Its "enemy-tribe PvP
    ///     authorized here" reading is already extracted in <see cref="Combat.ZonePvpZoneCatalog" /> (the
    ///     authoritative source for that reading); the full semantics of this column beyond that reading are not
    ///     established by the A7 contract. Legacy per-slot default 0.
    /// </summary>
    public int SecondaryClassification { get; init; }

    // ---- INI hard max-user cap (Server/Header/ini.h:297-304, key `Zone`+3-digit number, [Zone.MaxUser]) ----

    /// <summary>
    ///     This zone's hard maximum concurrent-user cap. In legacy this is a mandatory INI value validated to
    ///     1..1000 with boot abort on an out-of-range value (see <see cref="ZoneConfigCatalog.IsValidMaxUser" />).
    ///     Here, 0 means "no per-zone cap configured" -- <see cref="ZoneConfigCatalog.GetMaxUser" /> returns 0 for
    ///     an unconfigured zone and the connect-time enforcement (out of this workstream's scope -- the legacy
    ///     enforcement SITE is unobserved by the A7 finding) is expected to treat 0 as "unlimited/fail-open",
    ///     matching how <see cref="GameServerOptions.Capacity" /> is currently informational-only.
    /// </summary>
    public int MaxUser { get; init; }

    // ---- INI [Zone.Server] gameplay tunables (Server/Header/ini.h:306-347) ----
    // Each optional in legacy (an absent key leaves the prior/default value, no error, unlike the mandatory
    // endpoint keys). Names below are Fenrir-chosen descriptive translations; the exact legacy INI key names,
    // default magnitudes, and ratio scales are NOT enumerated in the A7 contract -- default 0 here, real values
    // from appsettings. Do NOT read a ratio scale (100 == 1.0x, etc.) into these without a dedicated finding.

    /// <summary>General experience-gain ratio for this zone.</summary>
    public int GeneralExperienceRatio { get; init; }

    /// <summary>Pet experience-gain ratio for this zone.</summary>
    public int PetExperienceRatio { get; init; }

    /// <summary>Mount experience-gain ratio for this zone.</summary>
    public int MountExperienceRatio { get; init; }

    /// <summary>Bonus experience-gain ratio for this zone.</summary>
    public int BonusExperienceRatio { get; init; }

    /// <summary>Teacher/mentor-point gain ratio for this zone.</summary>
    public int TeacherPointRatio { get; init; }

    /// <summary>Normal-tier item-drop ratio for this zone.</summary>
    public int NormalItemDropRatio { get; init; }

    /// <summary>Rare-tier item-drop ratio for this zone.</summary>
    public int RareItemDropRatio { get; init; }

    /// <summary>Elite-tier item-drop ratio for this zone.</summary>
    public int EliteItemDropRatio { get; init; }

    /// <summary>Money-drop ratio for this zone.</summary>
    public int MoneyDropRatio { get; init; }

    /// <summary>Kill-other-tribe add-value tunable for this zone.</summary>
    public int KillOtherTribeAddValue { get; init; }

    /// <summary>Kill-other-tribe experience ratio for this zone.</summary>
    public int KillOtherTribeExperienceRatio { get; init; }

    /// <summary>
    ///     Special-zone-38 experience ratio. Zone 38 is the Holy Stone zone; its enable toggle is already modeled
    ///     as <see cref="GameServerOptions.HolyStoneWarEnabled" />, so only the ratio value lives here.
    /// </summary>
    public int SpecialZone38ExperienceRatio { get; init; }

    /// <summary>Special-zone-38 money ratio. See <see cref="SpecialZone38ExperienceRatio" /> for the toggle note.</summary>
    public int SpecialZone38MoneyRatio { get; init; }

    /// <summary>
    ///     Special-zone-175 experience ratio. Zone 175 is the labyrinth-mission content; its own mechanic config
    ///     lives in <c>Zone175LabyrinthConfig</c>, so only the ratio value lives here.
    /// </summary>
    public int SpecialZone175ExperienceRatio { get; init; }

    /// <summary>Special-zone-175 money ratio. See <see cref="SpecialZone175ExperienceRatio" /> for the overlap note.</summary>
    public int SpecialZone175MoneyRatio { get; init; }

    /// <summary>
    ///     Regular-war schedule window for the zone-49 war (named after zone 49 in the legacy INI). Overlaps
    ///     conceptually with the in-code <c>RegularWarMapCatalog</c> per-map war config -- this is the INI-side
    ///     schedule-timing tunable, kept distinct.
    /// </summary>
    public int RegularWarZone49Window { get; init; }

    /// <summary>Regular-war schedule window for the zone-51 war. See <see cref="RegularWarZone49Window" />.</summary>
    public int RegularWarZone51Window { get; init; }

    /// <summary>Regular-war schedule window for the zone-53 war. See <see cref="RegularWarZone49Window" />.</summary>
    public int RegularWarZone53Window { get; init; }

    /// <summary>War countdown time tunable.</summary>
    public int WarCountdownTime { get; init; }

    /// <summary>War experience ratio.</summary>
    public int WarExperienceRatio { get; init; }

    /// <summary>War PvP ratio.</summary>
    public int WarPvpRatio { get; init; }

    /// <summary>War money ratio.</summary>
    public int WarMoneyRatio { get; init; }

    /// <summary>
    ///     The neutral, unconfigured-zone snapshot -- the exact legacy per-slot default a zone id that matches no
    ///     selector case keeps (<c>Server/Header/S18_MyZoneInfo.cpp:15-18</c>): level band 0-0,
    ///     <see cref="OwnerTribe" /> = <see cref="NoOwnerTribe" />, secondary 0, and every tunable at its neutral
    ///     0/unset default. Use this rather than <c>default(ZoneConfig)</c>, whose <see cref="OwnerTribe" /> would
    ///     be 0 (a real tribe) instead of -1.
    /// </summary>
    public static ZoneConfig Unconfigured { get; } = new() { OwnerTribe = NoOwnerTribe };
}

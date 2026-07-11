using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     One war-zone's registration eligibility band: an avatar may register into (initial world entry into, or
///     transfer into) this zone number only when its combined level (grade portion plus base-level portion --
///     <see cref="PlayerRuntimeState.CombinedLevel" />) and rebirth count both fall within the inclusive ranges
///     below.
/// </summary>
/// <remarks>
///     Réf. contract A8-portal-warzone, Inputs (Server/Header/S18_MyZoneInfo.cpp:435-437, 486;
///     Server/ts25zone/S07_MyGame03.cpp:5946-6056, 6008).
/// </remarks>
public readonly record struct WarZoneEntryRule(
    short ZoneNumber,
    int MinCombinedLevel,
    int MaxCombinedLevel,
    int MinRebirthCount,
    int MaxRebirthCount);

/// <summary>
///     Boot-frozen table backing <see cref="WarZoneEntryGate" /> -- the C# equivalent of the live
///     <c>CheckRegisterAvatar</c> registration eligibility switch (Server/ts25zone/S07_MyGame03.cpp:5946-6056),
///     restricted to exactly the subset of its cases the A8-portal-warzone contract could cite with concrete
///     min/max numbers: the two rebirth-tiered "maximum grade" war zones (295/322 = low rebirth tier, 296/323 =
///     high rebirth tier), the exact-level zone 164, and the FFA arena zone 335.
/// </summary>
/// <remarks>
///     <para>
///         A zone number absent from <see cref="Rules" /> is entirely unrestricted by this gate --
///         <c>CheckRegisterAvatar</c>'s switch only special-cases a fixed, enumerated set of server numbers, and
///         every zone outside that set passes through unconditionally (there is no cited "default deny"
///         branch), so "not in the table" and "explicitly unrestricted" are the same outcome here, never an
///         omission that under-restricts an otherwise-gated zone.
///     </para>
///     <para>
///         Réf. C++ (every combined-level/rebirth number below is taken directly from the contract's own
///         citations, none re-derived from Server/ this session):
///         <list type="bullet">
///             <item>
///                 Zone 164: combined level exactly <see cref="RebirthProgression.CombinedLevelCap" /> (157),
///                 rebirth 0-6 (S07_MyGame03.cpp:5998-6013, 6032-6055).
///             </item>
///             <item>
///                 Zone 295: combined level exactly 157, rebirth 0-6 -- the LOW rebirth tier
///                 (S18_MyZoneInfo.cpp:356-357,486-501; S07_MyGame03.cpp:6015-6030).
///             </item>
///             <item>
///                 Zone 296: combined level exactly 157, rebirth 7 and above -- the HIGH rebirth tier
///                 (S18_MyZoneInfo.cpp:356-357,486-501). Upper bound uses
///                 <see cref="RebirthProgression.MaxRebirthGeneration" /> (12), the engine-wide absolute rebirth
///                 ceiling this codebase already establishes elsewhere -- not a new citation, just reused here
///                 because the contract itself gives no explicit upper bound for the high tier beyond "7 or
///                 more" and <see cref="PlayerRuntimeState.RebirthCount" /> can never exceed that ceiling anyway.
///             </item>
///             <item>
///                 Zone 322: combined level exactly 157, rebirth 0-6 -- same LOW tier as 295
///                 (S18_MyZoneInfo.cpp:374-375,486-501).
///             </item>
///             <item>
///                 Zone 323: combined level exactly 157, rebirth 7 and above -- same HIGH tier as 296
///                 (S18_MyZoneInfo.cpp:374-375,486-501).
///             </item>
///             <item>
///                 Zone 335 (FFA arena): combined level 145-157, rebirth 0-12 (S07_MyGame03.cpp:5983-5997).
///             </item>
///         </list>
///     </para>
///     <para>
///         <b>Deliberately NOT in this table (open question, not invented):</b> the Odawa-cave custom zones
///         251-266 also gate registration entry, via an inline level-band recheck that additionally clears
///         several dungeon/time flags on success (S07_MyGame03.cpp:5966-5981), but the contract that cites this
///         mechanism does not carry the actual min/max numbers for that recheck -- only that it exists. Omitting
///         these 16 zone numbers here never lets a forbidden zone through that this table already covers; it
///         only leaves them un-gated by this specific mechanism, the same safe-fail-open direction
///         <see cref="Progression.AutoHuntEnableGate" /> already documents for its own deferred level-band term.
///         A follow-up legacy-behavior-translator contract must supply the concrete bands (and the flag-clearing
///         side effect, which belongs in the zone-entry completion path, not this gate) before zones 251-266 can
///         be added here.
///     </para>
/// </remarks>
public static class WarZoneEntryCatalog
{
    public static readonly FrozenDictionary<short, WarZoneEntryRule> Rules =
        new Dictionary<short, WarZoneEntryRule>
        {
            [164] = new(164, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 0, 6),
            [295] = new(295, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 0, 6),
            [296] = new(296, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 7,
                RebirthProgression.MaxRebirthGeneration),
            [322] = new(322, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 0, 6),
            [323] = new(323, RebirthProgression.CombinedLevelCap, RebirthProgression.CombinedLevelCap, 7,
                RebirthProgression.MaxRebirthGeneration),
            [335] = new(335, 145, RebirthProgression.CombinedLevelCap, 0, 12)
        }.ToFrozenDictionary();

    public static bool TryGetRule(short zoneNumber, out WarZoneEntryRule rule)
    {
        return Rules.TryGetValue(zoneNumber, out rule);
    }
}

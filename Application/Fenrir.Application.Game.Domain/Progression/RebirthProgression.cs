namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Shared constants/lookups for the G1-G12 rebirth-advancement mechanism (aRebirthNum), consumed by both
///     of its two independent trigger paths: the CP-funded "Max Rebirth" tribe-work sub-command (Path B,
///     <c>Fenrir.Application.Game.Services.Tribes.TribeActionService.RebirthAsync</c>, hard-capped at
///     generation 6) and the Rebirth Pill item-consumption path (Path A,
///     <c>Fenrir.Application.Game.Services.ZoneLifecycle.UseInventoryItemService</c>'s own Rebirth-Pill
///     branch, the only path that reaches generations 7-12).
///     <para>
///         Not covered here -- flagged as a distinct, unimplemented prerequisite subsystem per the
///         originating behavior contract: the mechanism by which <c>PlayerRuntimeState.Level2</c> itself
///         climbs from 0 to <see cref="MaxHighLevel" /> (legacy <c>ProcessForExperience2</c>, gated on
///         ordinary level already being at its own cap and granting skill/stat points along the way) is a
///         separate subsystem that supplies this type's own <see cref="MaxHighLevel" /> precondition, not
///         part of the rebirth transition itself. A character cannot reach the Level2==12 precondition
///         through any existing Fenrir kill-XP path today.
///     </para>
///     <para>
///         Also not covered here -- a separate, distinct feature reverses rebirth entirely (a consumable
///         that resets Level2/RebirthCount/base stats and refunds a stat-point pool at a skill-point/empty-
///         equipment cost); this is the only code path anywhere that ever reduces RebirthCount/Level2 back
///         down. Out of scope for this type; flagged for its own follow-up contract.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/Protocol/DEFINE.h:604-606 (MAX_LIMIT_HIGH_LEVEL_NUM=12, MAX_REBIRTH_LIMIT=12,
///     LV_M33=145) ; Server/ts25zone/GameSystem/GameSystem_01_Level.cpp:319-330
///     (LEVELSYSTEM::ReturnHighExpValue's mRangeForHigh table, consulted here only at index MaxHighLevel-1,
///     the "already at cap" 100% threshold both rebirth paths require) ;
///     Server/ts25zone/S04_MyWork03.cpp:5471-5536 (Path A's full handler) ;
///     Server/ts25zone/S04_MyWork02.cpp:10800,11342-11390 (Path B's full handler, TRIBE_WORK_SEND case 11).
/// </remarks>
public static class RebirthProgression
{
    /// <summary>MAX_LIMIT_HIGH_LEVEL_NUM -- Level2's own cap, the "G12 climb" gate both rebirth paths require.</summary>
    public const int MaxHighLevel = 12;

    /// <summary>
    ///     MAX_REBIRTH_LIMIT -- the absolute RebirthCount cap. Only reachable via Path A (the Rebirth-Pill
    ///     item-consumption path): Path B's own app-enforced cap of 6 (see
    ///     <c>TribeActionService.RebirthAsync</c>'s own remarks) never lets it advance past generation 6.
    /// </summary>
    public const int MaxRebirthGeneration = 12;

    /// <summary>
    ///     LV_M33 (145) + <see cref="MaxHighLevel" /> (12) -- the combined-level value (aLevel1+aLevel2) Path
    ///     B's own precondition requires to equal exactly, on top of (not instead of) each of the two terms
    ///     already being individually checked against its own cap elsewhere.
    /// </summary>
    public const int CombinedLevelCap = 157;

    /// <summary>
    ///     LEVELSYSTEM::ReturnHighExpValue's mRangeForHigh table: the XP needed to be considered "100%" at
    ///     Level2 N is HighLevelExpTable[N-1]. Both rebirth paths only ever consult this at N=
    ///     <see cref="MaxHighLevel" /> (Level2 already at its cap is a shared precondition for either path),
    ///     so this is a plain lookup, not a data-driven catalog.
    /// </summary>
    public static readonly int[] HighLevelExpTable =
    [
        962_105_896, 1_000_590_131, 1_040_613_736, 1_082_238_285, 1_125_527_816, 1_170_548_928,
        1_217_370_885, 1_266_065_720, 1_316_708_348, 1_369_376_681, 1_424_151_748, 1_481_117_817
    ];

    /// <summary>
    ///     Whether Level2 is already at its cap AND its own XP bar reads 100% -- the shared "already earned
    ///     this high level" gate both rebirth paths require before advancing (Path A preconditions 3-4, Path
    ///     B precondition 3 combined with its own separate Level2==cap check).
    /// </summary>
    public static bool IsHighLevelExperienceFull(short level2, int exp2)
    {
        return level2 == MaxHighLevel && exp2 >= HighLevelExpTable[MaxHighLevel - 1];
    }
}

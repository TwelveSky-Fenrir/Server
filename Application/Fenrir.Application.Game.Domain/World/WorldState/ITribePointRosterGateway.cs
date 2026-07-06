using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     One persisted character's roster fields the level-based tribe-point recompute needs to evaluate the
///     level-145 gate and the three-term point formula -- deliberately the genuine character/avatar roster's
///     own tribe/level/rebirth columns, NEVER the experience/rank-tracking table the legacy's live (but broken)
///     query actually targets. See <see cref="ITribePointRosterGateway" />'s own remarks for that bug and why
///     this record's contract exists specifically to not reproduce it.
/// </summary>
public readonly record struct TribeRosterCharacterSnapshot(
    byte TribeId,
    short Level1,
    short Level2,
    short RebirthCount);

/// <summary>
///     The persisted character/avatar roster read <see cref="TribePointLevelRecomputeService" /> needs (contract
///     input: "the full persisted roster of characters, each carrying tribe, primary level, secondary level,
///     rebirth count"). This is the seam: the real implementation must scan the genuine character/avatar roster
///     store (legacy AvatarInfo, <c>aTribe</c>/<c>aLevel1</c>/<c>aLevel2</c>/<c>aRebirthNum</c>) for every
///     character -- designing the backing repository method/stored procedure is fenrir-database-engineer work,
///     not done in this batch -- see <see cref="LoggingOnlyTribePointRosterGateway" /> for the safe placeholder
///     wired in until then.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S08_MyDB.cpp:107-136 (<c>GetTribePoint(int tTribePoint[MAX_TRIBE_NUM])</c>,
///     <c>#ifdef OLD_TRIBE_POINT</c>) -- the recompute this gateway feeds, including the active table-index bug
///     (queries <c>mSERVER_INFO.mDBTable[9]</c> at line 114, inside the compiling <c>#ifdef __GOD__</c> branch)
///     ; Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:229-240 (<c>GL_DB_UPDATE_EXP</c>, writing to
///     <c>mSERVER_INFO.mDBTable[9]</c>) -- corroborates table 9 (RankInfo) only carries
///     <c>uCharIdx</c>/<c>aExp1</c>/<c>aExp1Update</c>/<c>aExp2</c>/<c>aExp2Update</c>, none of the fields this
///     recompute needs ; Server/BuildEU33/ServerInfo.ini:16-25 (<c>Table02=AvatarInfo</c>, <c>Table09=RankInfo</c>)
///     ; Server/BuildEU33/DB/nxtserver.sql:24-41,118 (<c>avatarinfo</c> carries <c>aTribe</c>/<c>aLevel1</c>/
///     <c>aLevel2</c>/<c>aRebirthNum</c> -- the table this gateway's real implementation must target instead).
/// </remarks>
public interface ITribePointRosterGateway
{
    /// <summary>
    ///     Every persisted character's tribe/level/rebirth snapshot, or null if the read could not be completed
    ///     -- <see cref="TribePointLevelRecomputeService" /> treats null exactly like the contract's "the
    ///     per-tribe query cannot execute or returns nothing" case: abort without writing any totals for that
    ///     run, previous totals left exactly as they were.
    /// </summary>
    public Task<IReadOnlyList<TribeRosterCharacterSnapshot>?> GetRosterAsync(CancellationToken ct);
}

/// <summary>
///     Logs that a recompute was due but cannot run yet, and always returns null -- the safe placeholder until
///     the real roster-scan repository method exists (see <see cref="ITribePointRosterGateway" />'s own
///     remarks). Wiring this in production reproduces the legacy's own observed failure mode almost exactly:
///     the recompute is attempted every 6th tick but never succeeds, so the four tribe-point totals stay frozen
///     at whatever they held before (zero at first boot) -- not a regression from today's behavior, since
///     nothing in the shipped legacy cluster ever durably produced a value here either. Same posture as
///     <see cref="LoggingOnlyTribeBankTaxSweepGateway" /> for its own not-yet-wired seam.
/// </summary>
public sealed class LoggingOnlyTribePointRosterGateway(ILogger<LoggingOnlyTribePointRosterGateway> logger)
    : ITribePointRosterGateway
{
    public Task<IReadOnlyList<TribeRosterCharacterSnapshot>?> GetRosterAsync(CancellationToken ct)
    {
        logger.LogWarning(
            "TribePointLevelRecompute due but no character/avatar roster gateway is wired yet -- skipping this run, previous tribe-point totals left unchanged");
        return Task.FromResult<IReadOnlyList<TribeRosterCharacterSnapshot>?>(null);
    }
}

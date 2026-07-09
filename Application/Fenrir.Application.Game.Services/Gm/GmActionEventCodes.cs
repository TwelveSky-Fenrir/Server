namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     Locally-scoped game.EventLog.EventCode catalog for <see cref="EventLogCategory.GmAction" />, coordinated
///     across every Fenrir.Application.Game.Services.Gm caller that logs to this category so two unrelated GM
///     commands (Admin-tier or Elevated-tier alike) can never share the same (Category, EventCode) pair by
///     accident within this project. EventCode has no cross-project central catalog (see game.EventLog.sql's
///     own "app-owned numbering scheme, not FK'd to a lookup table" comment) -- this is the local one for
///     Fenrir.Application.Game.Services.Gm, mirroring
///     Fenrir.Application.Login.Services.AccountSecurity.AccountSecurityEventCodes's own convention. Every
///     Admin-tier and Elevated-tier command under this folder is required to write a game.EventLog row for its
///     effect (project decision, not a legacy-parity requirement -- most of these source commands' own legacy
///     bodies write no audit row at all; see each service's own remarks for where this diverges from the cited
///     Server/ path).
/// </summary>
internal static class GmActionEventCodes
{
    /// <summary>
    ///     The Admin-tier "MAX" stat-cheat command (tSort 509, <see cref="GmMaxStatService" />) unconditionally
    ///     reassigning VIT/STR/INT/DEX/SkillPoints on the invoking GM's own avatar. Legacy's own case 509 body
    ///     writes no audit-log call at all (Server/ts25zone/S04_MyWork04.cpp:1188-1202); this event exists
    ///     purely as a Fenrir-authored audit trail for an otherwise-invisible privileged self-mutation.
    /// </summary>
    public const short MaxStatCheat = 1;

    /// <summary>
    ///     The Admin-tier grant-pet-experience command (tSort 700, <see cref="GmPetExperienceGrantService" />)
    ///     being invoked against the GM's own equipped pet, regardless of whether an eligible pet was actually
    ///     found (see that service's own remarks for the outcome byte's meaning). Legacy's own case 700 body
    ///     writes no audit-log call either (Server/ts25zone/S04_MyWork04.cpp:2062-2083); same Fenrir-authored
    ///     rationale as <see cref="MaxStatCheat" />.
    /// </summary>
    public const short PetExperienceGrant = 2;

    /// <summary>Elevated-tier "[GM]-EXP" (grant experience to self), PROCESS_DATA_SEND tSort 503.</summary>
    public const short ExpGrant = 3;

    /// <summary>Elevated-tier "grant money" (legacy dead code, no live effect), PROCESS_DATA_SEND tSort 504.</summary>
    public const short GrantMoney = 4;

    /// <summary>Elevated-tier zone-wide FFA-start, PROCESS_DATA_SEND tSort 333.</summary>
    public const short FfaEventStart = 5;

    /// <summary>Elevated-tier "moncall" (summon monster), PROCESS_DATA_SEND tSort 506.</summary>
    public const short SummonMonster = 6;

    /// <summary>
    ///     The Basic-tier (<c>GmCommandTier.Basic</c>) DIE command (tSort 508,
    ///     <see cref="GmBasicCommandService" />) force-invalidating a live monster instance by raw table index.
    ///     Legacy's own case 508 body DOES write a real audit-log call (Server/ts25zone/S04_MyWork04.cpp:
    ///     1165-1187) before the mutation -- unlike <see cref="MaxStatCheat" />/<see cref="PetExperienceGrant" />
    ///     above, this is legacy parity, not a Fenrir-authored addition.
    /// </summary>
    public const short MonsterForceKill = 7;

    /// <summary>
    ///     The Basic-tier CALL command (tSort 514, <see cref="GmBasicCommandService" />) summoning a named
    ///     target to the invoker's own position (the normal-server branch only -- see
    ///     <see cref="Fenrir.Application.Game.Abstractions.Gm.IGmBasicCommandService" />'s own remarks for the
    ///     special-server mass-summon branch this project does not implement). Legacy parity: Server/ts25zone/
    ///     S04_MyWork04.cpp:1324-1384 writes a real audit-log call on this branch.
    /// </summary>
    public const short Call = 8;

    /// <summary>
    ///     The Basic-tier KICK command (tSort 518, <see cref="GmBasicCommandService" />) disconnecting a named
    ///     target's session. Legacy parity: Server/ts25zone/S04_MyWork04.cpp:1469-1486 writes a real audit-log
    ///     call before the disconnect.
    /// </summary>
    public const short Kick = 9;

    /// <summary>
    ///     The Basic-tier NCHAT (tSort 516, value 2)/YCHAT (tSort 517, value 0) commands
    ///     (<see cref="GmBasicCommandService" />), sharing ONE EventCode for both -- legacy itself uses the
    ///     same single log entry point for both, distinguishing them only by the resulting SpecialState value
    ///     (2 vs. 0), never by which log call fired (Server/ts25zone/S04_MyWork04.cpp:1411-1468). The written
    ///     <c>outcome</c> byte carries that SpecialState value so the two remain distinguishable in the audit
    ///     row itself.
    /// </summary>
    public const short Chat = 10;
}

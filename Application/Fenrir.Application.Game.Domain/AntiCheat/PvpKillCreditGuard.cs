namespace Fenrir.Application.Game.Domain.AntiCheat;

/// <summary>
///     Why a PvP kill earned no CP/exp/drop/rank credit, for observability/audit at the call site.
///     <see cref="None" /> = credit allowed.
/// </summary>
public enum KillCreditDenial : byte
{
    None = 0,
    NotReady = 1,
    SameOrigin = 2,
    LevelGap = 3
}

/// <summary>
///     The server-authoritative pre-reward guards on <c>MyUtil::ProcessForKillOtherTribe</c>
///     (<c>Server/ts25zone/S07_MyGame03.cpp:2602</c> onward) — the checks that decide whether a completed kill
///     earns reward credit. All are silent no-ops on failure in legacy: the kill itself stands, only the
///     reward is withheld.
/// </summary>
/// <remarks>
///     This guard covers the two checks NOT already modeled elsewhere in Fenrir:
///     <list type="number">
///         <item>
///             <description>
///                 <b>Both parties ready</b> — legacy returns immediately if either killer or victim is not in
///                 <c>mReadyState</c> (<c>:2604-2611</c>). Map <c>mReadyState</c> to the caller's own
///                 fully-in-world signal (e.g. <c>PlayerRuntimeState.ConnectTime is not null</c>); for
///                 <c>Zone.ApplyPvpKillRewards</c> both parties are already resident in <c>_players</c> (InWorld) by
///                 construction, so this is largely a defense-in-depth restatement there.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Same-origin</b> — legacy denies credit when the killer's and victim's captured source-IP
///                 strings are exactly equal (<c>:2725-2731,2827-2830</c>), its guard against farming CP off an alt
///                 on the same machine. See <see cref="IsSameOrigin" /> for the hardening applied.
///             </description>
///         </item>
///     </list>
///     The other two <c>ProcessForKillOtherTribe</c> guards are already implemented and wired in
///     <c>Zone.ApplyPvpKillRewards</c>, so they are NOT re-owned here (calling <see cref="Evaluate" /> alongside
///     them is idempotent for the level gap and deliberately excludes the stateful repeat-kill window):
///     <list type="bullet">
///         <item>
///             <description>
///                 the level-gap guard (<c>Zone.CombinedLevelGapCap == 13</c>, <c>:2660-2662</c>) — restated here
///                 as <see cref="ExceedsLevelGap" /> for a self-contained composite/test, same constant/direction; and
///             </description>
///         </item>
///         <item>
///             <description>
///                 the 10-minute repeat-kill window (<c>:2658,2832-2836</c>), owned by
///                 <c>Combat.KillCooldownTracker</c> (process-wide, stateful) — never duplicate it into this stateless
///                 guard.
///             </description>
///         </item>
///     </list>
/// </remarks>
public static class PvpKillCreditGuard
{
    /// <summary>
    ///     Legacy <c>tKillLevel</c> gap: a killer whose combined level exceeds the victim's by MORE than this
    ///     earns no credit (<c>Server/ts25zone/S07_MyGame03.cpp:2660-2662</c>). Mirrors the existing
    ///     <c>Zone.CombinedLevelGapCap</c> — the same literal, kept local so this guard is self-contained.
    /// </summary>
    public const int MaxKillerCombinedLevelAdvantage = 13;

    /// <summary>Legacy <c>mReadyState</c> precondition: both parties must be ready, else the routine returns doing nothing.</summary>
    public static bool AreBothReady(bool killerReady, bool victimReady)
    {
        return killerReady && victimReady;
    }

    /// <summary>
    ///     Whether the two parties look like the same real-world actor and so must not earn kill credit off each
    ///     other. <b>Hardened, never a verbatim replay of legacy.</b> Legacy's sole signal is exact source-IP
    ///     equality (<c>:2827-2830</c>), which two clients on different IPs (separate machines, NAT, VPN)
    ///     trivially defeat. This treats same-IP as ONE signal and adds account identity: an equal
    ///     <paramref name="killerAccountId" />/<paramref name="victimAccountId" /> (two characters of the same
    ///     account) is a strictly stronger same-origin signal and is checked first. Account ids are optional —
    ///     pass null to fall back to legacy IP-only behavior. IP comparison is via
    ///     <see cref="SessionSourceIp.AreSameHost" /> (IPv4-mapped-IPv6 normalized), not a raw string equality.
    ///     A hardware/session-fingerprint signal (the contract's further suggestion) is intentionally left as a
    ///     future input rather than invented here.
    /// </summary>
    public static bool IsSameOrigin(string? killerIp, string? victimIp, int? killerAccountId = null,
        int? victimAccountId = null)
    {
        if (killerAccountId is { } killer && victimAccountId is { } victim && killer == victim)
            return true;

        return SessionSourceIp.AreSameHost(killerIp, victimIp);
    }

    /// <summary>
    ///     Directional level-gap guard: only a killer OVER the victim by more than the cap is blocked; a lower-level
    ///     killer is never affected.
    /// </summary>
    public static bool ExceedsLevelGap(int killerCombinedLevel, int victimCombinedLevel)
    {
        return killerCombinedLevel - victimCombinedLevel > MaxKillerCombinedLevelAdvantage;
    }

    /// <summary>
    ///     Runs readiness → same-origin → level-gap in legacy order and returns the first failing reason, or
    ///     <see cref="KillCreditDenial.None" /> when credit is allowed. Deliberately excludes the stateful
    ///     repeat-kill window (see type remarks) — the caller composes this with
    ///     <c>Combat.KillCooldownTracker</c>.
    /// </summary>
    public static KillCreditDenial Evaluate(in PvpKillCreditRequest request)
    {
        if (!AreBothReady(request.KillerReady, request.VictimReady))
            return KillCreditDenial.NotReady;

        if (IsSameOrigin(request.KillerSourceIp, request.VictimSourceIp, request.KillerAccountId,
                request.VictimAccountId))
            return KillCreditDenial.SameOrigin;

        if (ExceedsLevelGap(request.KillerCombinedLevel, request.VictimCombinedLevel))
            return KillCreditDenial.LevelGap;

        return KillCreditDenial.None;
    }

    /// <summary>Convenience predicate over <see cref="Evaluate" />.</summary>
    public static bool IsCreditAllowed(in PvpKillCreditRequest request)
    {
        return Evaluate(request) == KillCreditDenial.None;
    }
}

/// <summary>
///     Inputs to <see cref="PvpKillCreditGuard.Evaluate" />. Combined levels are <c>aLevel1 + aLevel2</c> per party
///     (NOT rebirth count).
/// </summary>
public readonly record struct PvpKillCreditRequest(
    bool KillerReady,
    bool VictimReady,
    string? KillerSourceIp,
    string? VictimSourceIp,
    int? KillerAccountId,
    int? VictimAccountId,
    int KillerCombinedLevel,
    int VictimCombinedLevel);

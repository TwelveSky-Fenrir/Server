using Fenrir.Application.Game.Domain.AntiCheat;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     The twelve named cadence timers (<see cref="PlayerCadenceTimers" />). A mutable value field (not a
    ///     property — so <c>Cadences.ResetAll(...)</c>/<c>Cadences.Restamp(...)</c> mutate in place rather than a
    ///     copy), consistent with this type's single-writer-per-zone-tick contract. No wired consumer yet.
    /// </summary>
    public PlayerCadenceTimers Cadences;

    /// <summary>
    ///     Canonical source-IP string captured from the accepted socket at world entry — legacy <c>mIP</c>
    ///     (<c>Server/ts25zone/S07_MyGame03.cpp:2727,2827</c>), the input the PvP same-origin kill guard
    ///     (<see cref="AntiCheat.PvpKillCreditGuard.IsSameOrigin" />) compares between killer and victim. Null
    ///     for a session with no accepted socket (test transports). Populate via
    ///     <see cref="AntiCheat.SessionSourceIp.Normalize(System.Net.IPEndPoint?)" /> from the client session's
    ///     remote endpoint — see wiringManifest for the entry-path hop that must set it (the domain deliberately
    ///     does not reach into <c>IPacketSession</c> for a raw endpoint).
    /// </summary>
    public string? SourceIp { get; init; }

    // ---- Volatile anti-cheat counters (legacy H07_MyGame.h:813-909) --------------------------------------
    // Modeled here so the "clean slate on entry" invariant (side effect #1 of the D2 contract) is explicit and
    // testable, and so no counter can carry stale cross-session state. THE DETECTION ALGORITHMS THAT CONSUME
    // THESE ARE NOT IN ANY CITED LEGACY LINE and are intentionally NOT modeled — do not invent thresholds or
    // cadences against them. They exist as reset-on-entry state only until a consumption site is cited.

    /// <summary>
    ///     Legacy <c>mTickCountForUseDarkAttack</c> — dark-attack abuse detector state (consumer unobserved). Reset to 0
    ///     on entry.
    /// </summary>
    public int DarkAttackUseTick { get; set; }

    /// <summary>Legacy <c>mTickCountForActiveDarkAttack</c> — reset to 0 on entry.</summary>
    public int DarkAttackActiveTick { get; set; }

    /// <summary>Legacy <c>iKindDarkAttack</c> — reset to 0 on entry.</summary>
    public int DarkAttackKind { get; set; }

    /// <summary>Legacy <c>mTickCountForHitRate</c> — hit-rate abuse detector state (consumer unobserved). Reset to 0 on entry.</summary>
    public int HitRateTick { get; set; }

    /// <summary>Legacy <c>iKindHitRate</c> — reset to 0 on entry.</summary>
    public int HitRateKind { get; set; }

    /// <summary>
    ///     Legacy <c>mTickCountForDodgeRate</c> — dodge-rate abuse detector state (consumer unobserved). Reset to 0 on
    ///     entry.
    /// </summary>
    public int DodgeRateTick { get; set; }

    /// <summary>Legacy <c>iKindDodgeRate</c> — reset to 0 on entry.</summary>
    public int DodgeRateKind { get; set; }

    /// <summary>Legacy last-used-skill index (<c>S04_MyWork02.cpp:857-860</c> group) — reset to 0 on entry.</summary>
    public int LastUsedSkillIndex { get; set; }

    /// <summary>Legacy last-used-skill tick — reset to 0 on entry.</summary>
    public int LastUsedSkillTick { get; set; }

    /// <summary>Legacy last-used-skill warning counter — reset to 0 on entry.</summary>
    public int LastUsedSkillWarning { get; set; }

    /// <summary>Legacy last-used-skill total-warning counter — reset to 0 on entry.</summary>
    public int LastUsedSkillTotalWarning { get; set; }

    /// <summary>
    ///     Legacy <c>mSameSpot</c> (<c>H07_MyGame.h:894</c>) — same-position repeat counter (consumer unobserved). Reset
    ///     to 0 on entry.
    /// </summary>
    public int SameSpotCount { get; set; }

    /// <summary>Legacy bottle tick (<c>S04_MyWork02.cpp:876</c>) — reset to 0 on entry.</summary>
    public int BottleTick { get; set; }

    /// <summary>Legacy anti-AHS-hack flag — reset to false on entry.</summary>
    public bool AntiAhsHackFlag { get; set; }

    /// <summary>
    ///     Legacy <c>mAutoCheckState</c> (<c>H07_MyGame.h:850</c>) — bot challenge-response state (consumer unobserved).
    ///     Reset to 0 on entry.
    /// </summary>
    public int AutoCheckState { get; set; }

    /// <summary>
    ///     Legacy <c>mAutoCheckAnswer1</c> (<c>H07_MyGame.h:851</c>). Legacy leaves this OUT of the registration
    ///     reset block; Fenrir deliberately resets it (see <see cref="ResetVolatileAntiCheatCountersOnEntry" />).
    /// </summary>
    public int AutoCheckAnswer1 { get; set; }

    /// <summary>
    ///     Legacy <c>mAutoCheckAnswer2</c> (<c>H07_MyGame.h:852</c>). Legacy leaves this out of the reset block; Fenrir
    ///     resets it deliberately.
    /// </summary>
    public int AutoCheckAnswer2 { get; set; }

    /// <summary>
    ///     Legacy <c>mAutoCheckTime</c> (<c>H07_MyGame.h:853</c>). Legacy leaves this out of the reset block; Fenrir
    ///     resets it deliberately.
    /// </summary>
    public int AutoCheckTime { get; set; }

    /// <summary>Legacy first attack-hit-time counter (<c>S04_MyWork02.cpp:863-876</c> group) — reset to 0 on entry.</summary>
    public int AttackHitTime1 { get; set; }

    /// <summary>Legacy second attack-hit-time counter — reset to 0 on entry.</summary>
    public int AttackHitTime2 { get; set; }

    /// <summary>
    ///     Legacy CP-exchange tick (<c>S04_MyWork02.cpp:870-878</c> group) — re-baselined to the zone-clock at
    ///     entry, not zeroed. Zone-clock instant, matching every other timer on this type.
    /// </summary>
    public TimeSpan CpExchangeTick { get; set; }

    /// <summary>Legacy CP-RFC tick — re-baselined to the zone-clock at entry.</summary>
    public TimeSpan CpRfcTick { get; set; }

    /// <summary>
    ///     Re-baselines every volatile anti-cheat counter to its clean-slate value — side effect #1 of the D2
    ///     contract (<c>Server/ts25zone/S04_MyWork02.cpp:818-878</c>): "clean slate on entry" so no counter
    ///     carries stale cross-session state. Call once, at avatar registration, with the current zone clock —
    ///     see wiringManifest for the <c>Zone.HandleEnter</c> hook.
    /// </summary>
    /// <remarks>
    ///     Because Fenrir constructs a FRESH <see cref="PlayerRuntimeState" /> on every zone entry/transfer, the
    ///     integer/flag counters already start at their defaults; this method's load-bearing job is baselining
    ///     the <c>TimeSpan</c> timers (<see cref="Cadences" />, <see cref="CpExchangeTick" />,
    ///     <see cref="CpRfcTick" />) to the zone clock, which only <c>Zone.HandleEnter</c> knows. It is written to
    ///     be correct for object reuse too (an explicit, testable clean-slate), matching legacy's own
    ///     reset-on-registration intent.
    ///     <para>
    ///         <b>
    ///             Reset-vs-persist decisions made deliberately (the contract flags these as "not observed —
    ///             decide per counter"):
    ///         </b>
    ///         Fenrir resets the fields legacy leaves stale at registration —
    ///         <see cref="AntiCheat.AntiCheatCadence.TenSecond" />, <see cref="AutoCheckAnswer1" />/
    ///         <see cref="AutoCheckAnswer2" />/<see cref="AutoCheckTime" /> — because a stale anti-cheat baseline
    ///         is a (minor) weakness, not a feature. The legacy <c>aPvPKillNum</c> counter is not modeled here
    ///         (no observed consumer; the mission-kill counter with a real consumer is
    ///         <c>PlayerRuntimeState.MissionKillOtherTribe</c>, reset/handled on its own path).
    ///     </para>
    ///     <para>
    ///         Fields the contract lists in the reset block that are ALREADY reset by fresh construction and
    ///         their own initializers (owned by other partials — not re-touched here to respect partial
    ///         ownership): the attack-packet budget group (<c>AttackBudgetEnforced=true</c>,
    ///         <c>AttackFamilyTag</c>, <c>AttackSubPacketCeiling</c>, <c>AttackSubPacketsUsed=0</c> —
    ///         <c>PlayerRuntimeState.Combat.cs</c>, the legacy <c>mCheckMaxAttackPacketNum=1</c>/
    ///         <c>mMaxAttackPacketNum</c>/<c>mNowAttackPacketNum</c>/<c>mAttackPacketSort</c>), the move-zone
    ///         result (<c>IsMovingZone</c>), the heartbeat "none yet" sentinels
    ///         (<c>LastSentHeartbeat</c>/<c>PrevSentHeartbeat</c> null — <c>PlayerRuntimeState.Session.cs</c>),
    ///         and the inventory-item/enchant ticks
    ///         (<c>LastItemUseUtc</c>/<c>LastEnchantAttemptUtc</c> — <c>PlayerRuntimeState.cs</c>). The three
    ///         chat-protect ticks (with a small negative bias in legacy) are owned by the Chat area, not modeled
    ///         here — see notes.
    ///     </para>
    /// </remarks>
    public void ResetVolatileAntiCheatCountersOnEntry(TimeSpan zoneClockNow)
    {
        DarkAttackUseTick = 0;
        DarkAttackActiveTick = 0;
        DarkAttackKind = 0;
        HitRateTick = 0;
        HitRateKind = 0;
        DodgeRateTick = 0;
        DodgeRateKind = 0;
        LastUsedSkillIndex = 0;
        LastUsedSkillTick = 0;
        LastUsedSkillWarning = 0;
        LastUsedSkillTotalWarning = 0;
        SameSpotCount = 0;
        BottleTick = 0;
        AntiAhsHackFlag = false;
        AutoCheckState = 0;
        AutoCheckAnswer1 = 0;
        AutoCheckAnswer2 = 0;
        AutoCheckTime = 0;
        AttackHitTime1 = 0;
        AttackHitTime2 = 0;

        CpExchangeTick = zoneClockNow;
        CpRfcTick = zoneClockNow;
        Cadences.ResetAll(zoneClockNow);
    }
}

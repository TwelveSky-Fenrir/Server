using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;

namespace Fenrir.Application.Game.Abstractions.Gm;

/// <summary>
///     Business logic for the sixteen GM "Basic"-tier (<c>GmCommandTier.Basic</c>, legacy <c>uUserSort &gt;= 1</c>)
///     sub-operations of legacy PROCESS_DATA_SEND (opcode 19, <c>GenericActionRequest</c>): HIDE(501)/SHOW(502)/
///     self-teleport-to-coordinate MOVE(507)/DIE(508)/TRIBE(510)/EQUIP(511)/UNEQUIP(512)/FIND(513)/CALL(514)/
///     self-teleport-to-target MOVE(515)/NCHAT(516)/YCHAT(517)/KICK(518)/TRIBEBANK(520)/LEVEL(521)/
///     STR-DEX-VIT-INT-edit(522), Server/ts25zone/S04_MyWork04.cpp:290-339,925-1622,2105-2123. There is no
///     dedicated legacy wire opcode for any of these -- each is multiplexed inside the same generic envelope
///     every other <see cref="GenericActionRequest" /> tSort uses; <c>GenericActionHandler</c>'s own per-tSort
///     branch is the only caller of each method here.
///     <para>
///         <b>Shared framing</b> (applies to every method below): processing begins by assuming failure; only an
///         explicit success rule inside that specific method changes the outcome. Below-tier-gate failure is
///         identical for all sixteen -- the invoking session is forcibly disconnected
///         (<see cref="Fenrir.Network.Dispatch.Sessions.DisconnectReason.Faulted" />), no acknowledgment of any
///         kind. Most methods finish by sending the shared <c>GenericActionResponse</c> (opcode 23) generic
///         acknowledgment to the invoker's own connection only, carrying the (possibly still-default-failure)
///         outcome, the sub-operation code, and the entire original <paramref name="data" /> region echoed back
///         verbatim -- a few (TRIBE, NCHAT, YCHAT) finish without it, matching their own legacy
///         <c>return</c>-not-<c>break</c> control flow. Every per-account "X enabled" capability flag (Move/
///         Equip/Find/Call/Nchat/Ychat) legacy inspects immediately after the tier check is inert dead code in
///         the shipped build (its enforcement branch is commented out) and is deliberately NOT reproduced by any
///         method here.
///     </para>
///     <para>
///         <b>Unimplemented by design, not by oversight:</b> TRIBEBANK(520) and the STR/DEX/VIT/INT-edit
///         command(522) are dead code behind a live tier gate in the shipped legacy binary -- every statement
///         that would validate input and apply an effect exists only as inactive commentary in the source and
///         never executes. This project does not port that commented-out intended behavior as if it were
///         verified legacy parity; both methods here only reproduce the live gate-check-then-always-fail shape.
///         FIND(513)'s legacy behavior is a genuinely cluster-wide lookup via a separate upstream process, using
///         a blocking retry-until-success round trip with no observed timeout -- Fenrir has no equivalent
///         upstream process to call synchronously, and blocking a zone's own tick thread indefinitely would
///         violate this project's real-time architecture, so <see cref="HandleFindAsync" /> instead performs the
///         same process-local (this GameServer shard's own hosted maps), non-blocking by-name lookup every other
///         by-name command in this family already uses -- a deliberate, flagged simplification, not a legacy
///         citation. CALL(514)'s legacy behavior additionally branches into a wholly different, party-wide
///         mass-summon mode on one specific hard-coded server-instance number; that branch is a product decision
///         this contract does not presuppose an answer for and is not implemented here -- only the ordinary
///         single-target branch is.
///     </para>
/// </summary>
public interface IGmBasicCommandService
{
    /// <summary>
    ///     HIDE(501, <paramref name="sort" />==501)/SHOW(502) -- self-only visibility toggle. Sends the
    ///     dedicated visibility-change notification (<c>AvatarStatUpdateResponse</c>, self-only) immediately
    ///     followed by the shared generic acknowledgment -- two packets for one request. No other failure
    ///     branch exists once the tier gate passes.
    /// </summary>
    public ValueTask HandleVisibilityAsync(int sort, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    /// <summary>
    ///     MOVE self-teleport-to-coordinate (507) -- unconditional absolute position overwrite of the invoking
    ///     GM's own character, no bounds/map-validity/collision check. Shared generic acknowledgment only, no
    ///     dedicated response.
    /// </summary>
    public ValueTask HandleSelfTeleportAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    /// <summary>
    ///     DIE (508) -- force-invalidates a live monster instance by raw world-monster-table index (0-2999), not
    ///     a player. An out-of-range or already-invalid index is silently dropped (default-failure outcome, no
    ///     mutation); an in-range live instance is audit-logged then force-killed with no attacker credit (so no
    ///     loot/experience grant), which also arms that instance's ordinary respawn timer from "now" via the
    ///     same death pipeline every other monster death already uses.
    /// </summary>
    public ValueTask HandleForceKillMonsterAsync(byte[] data, ZoneClientSession zoneSession, Zone zone,
        CancellationToken cancellationToken);

    /// <summary>
    ///     TRIBE (510) -- self-only tribe change (selector 0-3, no target-character parameter exists). A
    ///     selector equal to the current tribe, or outside 0-3, is silently dropped: no mutation, no disconnect,
    ///     no acknowledgment of any kind. A successful change mutates Tribe (and PreviousTribe, unless the
    ///     selector is the special value 3) plus clears the 5-slot quest state, writes NO audit-log entry, sends
    ///     NO acknowledgment of any kind, and forcibly disconnects the invoking GM's own session as the normal
    ///     signal that the command succeeded (<see cref="Fenrir.Network.Dispatch.Sessions.DisconnectReason.GmCommandLogout" />
    ///     ).
    ///     <para>
    ///         Persistence gap, flagged not silently assumed correct: unlike the sibling op37 (Fourth-tribe
    ///         conversion) command this method's own mutation shape resembles, no existing repository call
    ///         durably writes a plain Tribe+PreviousTribe update for this command -- the in-memory
    ///         <see cref="PlayerRuntimeState" /> mutation is real and immediately observable for the remainder
    ///         of this (about to end) session, but does not survive the character's next login. Closing this
    ///         gap needs a new stored procedure from <c>fenrir-database-engineer</c>, not an ad hoc SQL write
    ///         from this method.
    ///     </para>
    /// </summary>
    public ValueTask HandleTribeChangeAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    /// <summary>
    ///     EQUIP(511, <paramref name="sort" />==511)/UNEQUIP(512) -- self-only, writes the character's own
    ///     "special state" marker to 1/0. Despite the command's name, no item identifier is present in the
    ///     request and no actual inventory/equipment item is touched; the field itself has no confirmed
    ///     server-side read/branch consumer anywhere in the zone-server source tree (display/persisted value
    ///     only). Shared generic acknowledgment only.
    /// </summary>
    public ValueTask HandleSelfSpecialStateAsync(int sort, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken);

    /// <summary>
    ///     FIND (513) -- locates a named character and reports back a coordinate/zone-number
    ///     (<c>GmCommandResponse</c>, opcode 97) immediately followed by the shared generic acknowledgment. See
    ///     this interface's own class remarks for why this is a deliberately simplified, process-local (not
    ///     cluster-wide-via-blocking-upstream-call) lookup, and for the unresolved "what value is sent when no
    ///     character matches" ambiguity this method resolves by writing a zero-filled coordinate/zone-number
    ///     region rather than a confirmed legacy sentinel. The dedicated response's own <c>Sort</c> field
    ///     carries the legacy literal tag 1 (<c>B_GM_COMMAND_INFO(1, ...)</c>), NOT the outer switch's tSort
    ///     513 -- see S05_MyTransfer.cpp:1159-1164 and S04_MyWork04.cpp:1319.
    /// </summary>
    public ValueTask HandleFindAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken);

    /// <summary>
    ///     CALL (514) -- summons a named target (process-local to this GameServer shard, self-exclusion applies)
    ///     to the invoking GM's own current position. Only the ordinary single-target branch is implemented --
    ///     see this interface's own class remarks for the special-server mass-summon branch this project does
    ///     not implement. On a match: the target is warped, audit-logged, and receives the dedicated
    ///     coordinate response (<c>GmCommandResponse</c>) plus an AOI-neighbor relocation broadcast on its own
    ///     zone; the invoker receives only the shared generic acknowledgment. Target not found (or is the
    ///     invoker itself): shared generic acknowledgment only, default-failure outcome, nothing sent to anyone
    ///     else. The dedicated response's own <c>Sort</c> field carries the legacy literal tag 2 (
    ///     <c>B_GM_COMMAND_INFO(2, ...)</c>), NOT the outer switch's tSort 514 -- see
    ///     S05_MyTransfer.cpp:1159-1164 and S04_MyWork04.cpp:1348 (shared with <see cref="HandleMoveToTargetAsync" />'s
    ///     own tag below).
    /// </summary>
    public ValueTask HandleCallAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken);

    /// <summary>
    ///     MOVE self-teleport-to-target's-position (515) -- the reverse direction of <see cref="HandleCallAsync" />:
    ///     the invoking GM is warped to a named target's (process-local, self-exclusion applies) current
    ///     position. On a match: the invoker's own position is overwritten and the invoker receives the
    ///     dedicated coordinate response (<c>GmCommandResponse</c>) on its own connection, immediately followed
    ///     by the shared generic acknowledgment -- no broadcast is issued for this variant, unlike CALL. Target
    ///     not found (or is the invoker itself): shared generic acknowledgment only, default-failure outcome.
    ///     The dedicated response's own <c>Sort</c> field carries the same legacy literal tag 2 CALL uses (
    ///     <c>B_GM_COMMAND_INFO(2, ...)</c>), NOT the outer switch's tSort 515 -- see
    ///     S05_MyTransfer.cpp:1159-1164 and S04_MyWork04.cpp:1406.
    /// </summary>
    public ValueTask HandleMoveToTargetAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone, CancellationToken cancellationToken);

    /// <summary>
    ///     NCHAT(516, <paramref name="sort" />==516)/YCHAT(517) -- marks a named target's (process-local,
    ///     self-exclusion applies) own "special state" marker to 2/0, audit-logged through one shared log point
    ///     for both (distinguishable only by the outcome byte, not by which log call fired). This family is
    ///     asymmetric, not silent on every path: target not found (or is the invoker itself) falls through to
    ///     the shared closing step and sends the shared generic acknowledgment carrying the default-failure
    ///     outcome, matching legacy's own bare <c>break</c> out of the switch for that case
    ///     (S04_MyWork04.cpp:1429-1432/1458-1461). Target found: sends NO acknowledgment of any kind -- legacy's
    ///     own case body ends in an explicit <c>return</c> before ever reaching the shared closing step
    ///     (S04_MyWork04.cpp:1433-1439/1462-1468).
    /// </summary>
    public ValueTask HandleTargetSpecialStateAsync(int sort, byte[] data, ZoneClientSession zoneSession,
        PlayerRuntimeState state, CancellationToken cancellationToken);

    /// <summary>
    ///     KICK (518) -- disconnects a named target's (process-local, self-exclusion applies) session
    ///     (<see cref="Fenrir.Network.Dispatch.Sessions.DisconnectReason.GmKicked" />), audit-logged before the
    ///     disconnect. No dedicated response to anyone; the invoker receives only the shared generic
    ///     acknowledgment. Target not found: shared generic acknowledgment only, default-failure outcome.
    /// </summary>
    public ValueTask HandleKickAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        CancellationToken cancellationToken);

    /// <summary>
    ///     TRIBEBANK (520) -- dead code behind a live tier gate; see this interface's own class remarks. Always
    ///     sends the shared generic acknowledgment carrying the default-failure outcome, regardless of input, no
    ///     mutation of any kind.
    /// </summary>
    public ValueTask HandleTribeBankAsync(byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);

    /// <summary>
    ///     LEVEL (521) -- self-only total-level set, decomposed into base level (up to 145) / "high level" (a
    ///     further 12) / rebirth count (a further 12), 169 combined capacity. No lower bound is enforced (a
    ///     zero/negative request is applied as-is to the base tier, matching legacy's own "less than or equal"
    ///     comparisons); a request above 169 is rejected with no mutation. On success, derived combat stats are
    ///     recomputed from the new level/equipment and life/mana are unconditionally reset to the newly computed
    ///     maximum (this command always fully heals the invoker). No audit-log entry is written, matching
    ///     legacy. Shared generic acknowledgment only.
    ///     <para>
    ///         The "high level"/rebirth tiers' own secondary-experience-component (Exp2) is recomputed from the
    ///         same hardcoded 12-entry high-level XP table already ported to
    ///         <see cref="Fenrir.Application.Game.Domain.Progression.RebirthProgression.HighLevelExpTable" />
    ///         (legacy <c>LEVELSYSTEM::ReturnHighExpValue</c>'s <c>mRangeForHigh</c>, GameSystem_01_Level.cpp:
    ///         712-719,319-330): index Level2-1 for the high-level tier, index
    ///         <see cref="Fenrir.Application.Game.Domain.Progression.RebirthProgression.MaxHighLevel" />-1 for
    ///         the rebirth tier (Level2 pinned at its own cap there), matching
    ///         <c>wAvatar.aExp2 = mLEVEL.ReturnHighExpValue(...)</c> at S04_MyWork04.cpp:1566-1580. Tier 1
    ///         (plain base level, no high-level or rebirth component) always clears Exp2 to 0.
    ///         Level/Level2/RebirthCount/base Experience/Life/Mana/MaxLife/MaxMana are all confirmed and applied
    ///         per the contract.
    ///     </para>
    /// </summary>
    public ValueTask HandleLevelSetAsync(byte[] data, ZoneClientSession zoneSession, PlayerRuntimeState state,
        Zone zone,
        CancellationToken cancellationToken);

    /// <summary>
    ///     STR/DEX/VIT/INT stat edit (522) -- dead code behind a live tier gate; see this interface's own class
    ///     remarks. Always sends the shared generic acknowledgment carrying the default-failure outcome,
    ///     regardless of input, no mutation of any kind.
    /// </summary>
    public ValueTask HandleStatEditAsync(byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken);
}

using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     The per-character party-buff cast-lifecycle marker (<c>mParty_Buff_Act</c>, H07_MyGame.h:1002 -- a
///     single byte of session state). Its three values and their ordering are the legacy <c>PartyBuffAction</c>
///     enum (Server/Header/Protocol/STRUCT.h:1417-1421). A cast can only ever reach <see cref="Done" /> by first
///     being set to <see cref="Cast" /> (there is no direct <see cref="None" />-to-<see cref="Done" /> path --
///     see <see cref="FormationSkillCatalog.NextPartyBuffMarker" />).
/// </summary>
public enum PartyBuffAction : byte
{
    /// <summary>No party-buff cast is in flight. Reset value on (re-)init and on zone/state-change events.</summary>
    None = 0,

    /// <summary>The "arm" step ran (a party-buff skill with action sort 64 was accepted).</summary>
    Cast = 1,

    /// <summary>
    ///     The "confirm" step ran (action sort 65 while already <see cref="Cast" />) -- the gate the formation broadcast
    ///     reads.
    /// </summary>
    Done = 2
}

/// <summary>
///     Pure, static classification of the six Formation skills (76-81) that drive the party-buff cast lifecycle:
///     which of them are silently dropped in a war/duel-type zone, which advance the <see cref="PartyBuffAction" />
///     cast-lifecycle marker, which hit the empty case, and which skill/sort pairs bypass the default-case grade
///     bound check. This is the read-only lookup half of Fenrir's B14 skill-action-validation slice; the actual
///     marker mutation and zone-lock drops are applied by <see cref="Zone" /> (Zone.SkillValidation.cs), and the
///     grade bound-check <b>enforcement</b> itself is workstream D2's -- this catalog only classifies which skills
///     reach or bypass it.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:1298-1310 (124-type-zone drop of skills 76-81, at the very
///     top of the primary <c>AVATAR_ACTION_SEND</c> handler), :1438-1450 (049-type-zone drop of skills 76-81, a
///     second guard later in the same handler, mirrored at :1801-1813 of the update-action handler),
///     :1684-1700 (party-buff marker: skills 76/77/79/81, sort 64 sets <c>CAST</c>, sort 65 advances to
///     <c>DONE</c> only from <c>CAST</c>), :1701-1703 (empty-case bypass for skills 78 and 80), :1706-1710
///     (Bottle exemption: action sort 16 with skill number 1 skips the default-case bound check, primary handler
///     only -- absent from the update-action mirror), :1712-1721 (default-case bound check whose enforcement is
///     workstream D2). Both the 124-type and 049-type drops are separate legacy guards that produce the identical
///     outward behavior (silent, no mana/marker/broadcast/response/disconnect), so
///     <see cref="IsFormationSkillZoneLocked" />
///     collapses them into one predicate.
/// </remarks>
public static class FormationSkillCatalog
{
    /// <summary>
    ///     Legacy server number of the single "124-type" zone process (<c>mCheckZone124TypeServer</c> is true only
    ///     on the process assigned this server number -- H07_MyGame.h:239, S07_MyGame01.cpp:513-521). In Fenrir's
    ///     one-process-per-map model this is <see cref="Zone.MapId" /> == 124, the same constant
    ///     <c>Zone.ApplySkillEffectConfirm</c> already keys the Holy-Shield reapplication cooldown on.
    /// </summary>
    public const short Zone124TypeMapId = 124;

    /// <summary>The "Bottle"/recover skill referenced by the grade-check exemption below.</summary>
    public const int BottleSkillNumber = 1;

    /// <summary>
    ///     Action sort that (only together with <see cref="BottleSkillNumber" />) triggers the Bottle grade-check
    ///     exemption.
    /// </summary>
    public const int BottleExemptionActionSort = 16;

    /// <summary>Party-buff <b>arm</b> step -- sets the marker to <see cref="PartyBuffAction.Cast" />.</summary>
    public const int PartyBuffArmActionSort = 64;

    /// <summary>
    ///     Party-buff <b>confirm</b> step -- advances the marker to <see cref="PartyBuffAction.Done" /> only from
    ///     <see cref="PartyBuffAction.Cast" />.
    /// </summary>
    public const int PartyBuffConfirmActionSort = 65;

    /// <summary>The contiguous Formation skill set 76-81, dropped wholesale in a war/duel-type zone.</summary>
    private static readonly FrozenSet<int> FormationSkillNumbers = new[] { 76, 77, 78, 79, 80, 81 }.ToFrozenSet();

    /// <summary>
    ///     The four Formation skills that drive the party-buff marker (76, 77, 79, 81). Skills 78 and 80 are the
    ///     two non-buff Formation skills -- see <see cref="EmptyCaseSkillNumbers" />.
    /// </summary>
    private static readonly FrozenSet<int> PartyBuffSkillNumbers = new[] { 76, 77, 79, 81 }.ToFrozenSet();

    /// <summary>
    ///     The two Formation skills (78, 80) that match a case whose body is empty -- they neither touch the
    ///     party-buff marker nor reach the default-case bound check, and never broadcast a party buff.
    /// </summary>
    private static readonly FrozenSet<int> EmptyCaseSkillNumbers = new[] { 78, 80 }.ToFrozenSet();

    /// <summary>True for any Formation skill number 76-81.</summary>
    public static bool IsFormationSkill(int skillNumber)
    {
        return FormationSkillNumbers.Contains(skillNumber);
    }

    /// <summary>True for the four party-buff Formation skills (76, 77, 79, 81) -- the ones that advance the marker.</summary>
    public static bool IsPartyBuffSkill(int skillNumber)
    {
        return PartyBuffSkillNumbers.Contains(skillNumber);
    }

    /// <summary>True for the two empty-case Formation skills (78, 80).</summary>
    public static bool IsEmptyCaseSkill(int skillNumber)
    {
        return EmptyCaseSkillNumbers.Contains(skillNumber);
    }

    /// <summary>
    ///     Whether the whole action must be silently dropped because <paramref name="skillNumber" /> is a
    ///     Formation skill (76-81) and this zone process is a war/duel-type zone. A "124-type" zone is
    ///     <paramref name="mapId" /> == <see cref="Zone124TypeMapId" />; a "049-type" zone is signalled by
    ///     <paramref name="isRegularWarMap" /> (the caller resolves it from
    ///     <c>RegularWarMapCatalog</c> membership, the same fixed 11-map set <c>mCheckZone049TypeServer</c>
    ///     collapses to in Fenrir). On a true result the cast is completely ignored: no mana charged, no marker
    ///     change, no broadcast, no response, no disconnect.
    /// </summary>
    public static bool IsFormationSkillZoneLocked(int skillNumber, short mapId, bool isRegularWarMap)
    {
        if (!IsFormationSkill(skillNumber))
            return false;

        return mapId == Zone124TypeMapId || isRegularWarMap;
    }

    /// <summary>
    ///     The party-buff cast-lifecycle marker's next value after processing one accepted action.
    ///     <list type="bullet">
    ///         <item>
    ///             A non-party-buff skill number leaves <paramref name="current" /> untouched (78/80, and every skill
    ///             outside 76-81).
    ///         </item>
    ///         <item>
    ///             Party-buff skill (76/77/79/81) with action sort 64 sets the marker to <see cref="PartyBuffAction.Cast" />
    ///             unconditionally (a fresh arm).
    ///         </item>
    ///         <item>
    ///             Party-buff skill with action sort 65 advances to <see cref="PartyBuffAction.Done" /> only if it currently
    ///             equals <see cref="PartyBuffAction.Cast" />; from any other state it is left unchanged (there is no direct
    ///             None-to-Done path).
    ///         </item>
    ///         <item>Any other action sort leaves the marker unchanged.</item>
    ///     </list>
    ///     A single accepted action carries exactly one action sort, so it advances the marker by at most one step.
    /// </summary>
    public static PartyBuffAction NextPartyBuffMarker(PartyBuffAction current, int skillNumber, int actionSort)
    {
        if (!IsPartyBuffSkill(skillNumber))
            return current;

        return actionSort switch
        {
            PartyBuffArmActionSort => PartyBuffAction.Cast,
            PartyBuffConfirmActionSort when current == PartyBuffAction.Cast => PartyBuffAction.Done,
            _ => current
        };
    }

    /// <summary>
    ///     Whether <paramref name="skillNumber" />/<paramref name="actionSort" /> is exempt from the default-case
    ///     grade bound check (the check whose <b>enforcement</b> -- disconnect on the primary handler, silent
    ///     return on the update-action handler -- is workstream D2). This method only records which skills bypass
    ///     it:
    ///     <list type="bullet">
    ///         <item>
    ///             The four party-buff skills (76/77/79/81) reach the marker case, not the default -- exempt in both
    ///             handlers.
    ///         </item>
    ///         <item>The two empty-case skills (78/80) reach the empty case, not the default -- exempt in both handlers.</item>
    ///         <item>
    ///             The Bottle skill (1) with action sort 16 breaks out of the default case early -- exempt in the
    ///             <b>primary</b> handler only; the update-action mirror has no such exemption.
    ///         </item>
    ///     </list>
    /// </summary>
    /// <param name="isPrimaryHandler">
    ///     True for the primary <c>AVATAR_ACTION_SEND</c> handler, false for the <c>UPDATE_AVATAR_ACTION</c>
    ///     mirror -- only the primary handler carries the Bottle exemption.
    /// </param>
    public static bool IsExemptFromGradeBoundCheck(int skillNumber, int actionSort, bool isPrimaryHandler)
    {
        if (IsPartyBuffSkill(skillNumber) || IsEmptyCaseSkill(skillNumber))
            return true;

        return isPrimaryHandler && skillNumber == BottleSkillNumber && actionSort == BottleExemptionActionSort;
    }
}

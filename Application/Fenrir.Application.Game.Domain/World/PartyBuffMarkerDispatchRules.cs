namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     B14 reconciliation (wave9 "B14-partybuff-sort" contract): the corrected opcode/Sort reachability rules
///     that decide WHEN <see cref="Zone.AdvanceCasterPartyBuffMarker" /> and the party-buff DONE-state reset
///     should actually be called from <see cref="Zone" />'s op15/op16 dispatch (<c>Zone.PlayerLifecycle.HandleMove</c>
///     / <c>ApplySkillEffectConfirm</c>). <see cref="FormationSkillCatalog.NextPartyBuffMarker" />'s own state
///     machine (sort 64 arms unconditionally, sort 65 advances CAST-&gt;DONE only from CAST) was already
///     correct and needed NO fix -- this class exists purely to fix the CALL-SITE PLACEMENT bug the wave9
///     contract flagged, not the state-machine logic itself. See wiringManifest (the accompanying task report)
///     for the exact <c>Zone.PlayerLifecycle.cs</c> edits this class's methods are meant to be called from.
/// </summary>
/// <remarks>
///     <para>
///         <b>Corrected opcode-reachability table</b> (per the wave9 contract, which itself corrects an
///         earlier, less precise source finding):
///     </para>
///     <list type="bullet">
///         <item>
///             CAST (Sort 64) is reachable via EITHER opcode 15 (CZ_AVATAR_ACTION_SEND) or opcode 16
///             (CZ_UPDATE_AVATAR_ACTION) -- both <c>CheckValidCharacterMotionForSend</c>'s own case 64
///             (Server/ts25zone/S04_MyWork05.cpp:4518-4524) and op16's top-level switch case 64
///             (Server/ts25zone/S04_MyWork02.cpp:1843-1849) accept it, given <c>aType==0</c> on either path.
///         </item>
///         <item>
///             DONE (Sort 65) is reachable via opcode 15 ONLY. Opcode 16's own top-level switch
///             (Server/ts25zone/S04_MyWork02.cpp:1815-1878) carries no case for 65 at all -- it falls straight
///             to the disconnecting <c>default:</c> (:1875-1877). Fenrir already enforces this correctly and
///             needs no fix here: <see cref="Movement.AvatarActionResumeWhitelist" />'s own
///             <c>TypeMasksBySort</c> table has no entry for 65, so
///             <see cref="Movement.AvatarActionResumeWhitelist.IsLegal" /> already returns false for it and
///             <c>Zone.PlayerLifecycle.HandleMove</c>'s existing resume-path guard already disconnects the
///             session before this class would ever be consulted for a Sort-65 op16 packet. This class does
///             not re-derive that gate -- <see cref="ShouldAdvancePartyBuffMarker" /> below simply never
///             returns true for (op16, Sort 65), matching that already-correct disconnect.
///         </item>
///         <item>
///             CONFIRM (Sort 1, the wire-level "create effect value" trigger,
///             <c>Zone.PlayerLifecycle.SkillEffectConfirmActionSort</c>) is reachable via opcode 16 ONLY, and
///             is an ENTIRELY SEPARATE value space from this mechanism's own CAST/DONE Sorts (64/65) despite
///             both loosely being called "confirm" in prose -- see the naming-collision note below. On a
///             successful confirm (buff actually written), the DONE state is consumed exactly once via
///             <see cref="ShouldResetPartyBuffMarkerOnConfirmSuccess" />.
///         </item>
///     </list>
///     <para>
///         <b>The bug this reconciles, precisely:</b> going into this pass, <c>Zone.PlayerLifecycle.HandleMove</c>
///         never actually reached <c>AdvanceCasterPartyBuffMarker</c> for a real CAST(64)/DONE(65) action on
///         either opcode:
///     </para>
///     <list type="bullet">
///         <item>
///             On the op15 (<c>!isResumeAction</c>) path, the only call site lived inside
///             <c>ApplySkillCastManaCharge</c>, itself gated on
///             <c>motion.SkillCategoryCode == SkillCastEffectCategoryCode</c> (2). But
///             <see cref="Movement.CharacterMotionWhitelist" />'s own rows for Sort 64
///             (<c>[64] = new(Type0, 0, true, 0, 0)</c>) and Sort 65 (<c>[65] = new(Type0, 0, false, 5, 0)</c>)
///             both resolve <c>SkillCategoryCode</c> to 0, never 2 -- so that call was unreachable dead code
///             for this mechanism specifically (it could only ever fire for an unrelated category-2 Sort,
///             where <see cref="FormationSkillCatalog.NextPartyBuffMarker" />'s own Sort-64/65 match already
///             makes it a guaranteed no-op).
///         </item>
///         <item>
///             On the op16 (<c>isResumeAction</c>) path, the only call site lived inside
///             <c>ApplySkillEffectConfirm</c>, which is entered only when
///             <c>
///                 action.Sort ==
///                 SkillEffectConfirmActionSort
///             </c>
///             (1) -- and passed that same <c>action.Sort</c> (1) straight
///             into <c>AdvanceCasterPartyBuffMarker</c>. Since
///             <see cref="FormationSkillCatalog.NextPartyBuffMarker" /> only recognizes Sort 64/65, passing
///             Sort 1 was always a no-op too.
///         </item>
///     </list>
///     <para>
///         <b>Naming-collision root cause (flagged by the contract itself, confirmed in this codebase):</b>
///         <c>FormationSkillCatalog.PartyBuffConfirmActionSort</c> (65, this mechanism's own DONE step) and
///         <c>Zone.PlayerLifecycle.SkillEffectConfirmActionSort</c> (1, the wire-level CONFIRM/
///         "create effect value" trigger) both use the word "confirm" in their own names for two entirely
///         unrelated Sort values -- almost certainly why the op16 call site above ended up passing the wrong
///         Sort. This class's own method names deliberately avoid the word "confirm" for the 65 case (using
///         "DONE", matching the contract's own three-step CAST/DONE/CONFIRM vocabulary) to remove that
///         ambiguity going forward; <c>FormationSkillCatalog.PartyBuffConfirmActionSort</c> itself is not
///         renamed here (this class only ADDS files, it does not edit existing ones) -- see wiringManifest for
///         the recommended rename.
///     </para>
///     <para>
///         Not addressed by this reconciliation (left exactly as previously deferred, per its own existing
///         TODO in <c>ApplySkillEffectConfirm</c>): whether a CONFIRM(1) should also be GATED on
///         <c>PartyBuffAct == Done</c> before the buff is written at all (the legacy precondition,
///         Server/ts25zone/S07_MyGame04.cpp:1348-1351) rather than relying on the existing
///         HasFullPartyPresent-gated behavior as a structural stand-in. That is a genuine behavior change
///         beyond "which Sort/opcode advances the marker," so it stays an open question for adjudication by
///         <c>Zone.PlayerLifecycle.cs</c>'s owner, not something this reconciliation resolves unilaterally.
///     </para>
/// </remarks>
public static class PartyBuffMarkerDispatchRules
{
    /// <summary>
    ///     Whether an accepted action on the given opcode should call
    ///     <see cref="Zone.AdvanceCasterPartyBuffMarker" /> for its own <paramref name="actionSort" />.
    ///     <see cref="Zone.AdvanceCasterPartyBuffMarker" /> (via
    ///     <see cref="FormationSkillCatalog.NextPartyBuffMarker" />) already no-ops for any non-party-buff
    ///     skill number (76/77/79/81 only) or any Sort other than 64/65, so this predicate only needs to
    ///     encode the OPCODE-level reachability asymmetry between CAST and DONE -- it is safe, and intended,
    ///     for the caller to invoke the call unconditionally whenever this returns true, without re-checking
    ///     the skill number at the call site.
    /// </summary>
    /// <param name="isResumeAction">
    ///     True for CZ_UPDATE_AVATAR_ACTION (op16, Fenrir's own <c>isResumeAction</c> flag in
    ///     <c>Zone.PlayerLifecycle.HandleMove</c>), false for CZ_AVATAR_ACTION_SEND (op15).
    /// </param>
    /// <param name="actionSort">The accepted action's own <c>ActionInfo.Sort</c>.</param>
    public static bool ShouldAdvancePartyBuffMarker(bool isResumeAction, int actionSort)
    {
        if (!isResumeAction)
            return actionSort is FormationSkillCatalog.PartyBuffArmActionSort
                or FormationSkillCatalog.PartyBuffConfirmActionSort;

        // Op16: DONE (65) is not reachable here at all -- a real op16 packet carrying Sort 65 never reaches
        // this call in the first place, since AvatarActionResumeWhitelist.IsLegal already returns false for
        // it and the caller already disconnects the session before HandleMove's own dispatch tail runs. Only
        // CAST (64) needs to be recognized on this opcode.
        return actionSort == FormationSkillCatalog.PartyBuffArmActionSort;
    }

    /// <summary>
    ///     Whether a successful CONFIRM(1) effect resolution should reset the caster's party-buff marker back
    ///     to <see cref="PartyBuffAction.None" /> -- the single-use, anti-replay consumption of the DONE state
    ///     (Server/ts25zone/S07_MyGame03.cpp:9595-9622; the reset itself at :9600 for skill 76, :9607 for 77,
    ///     :9614 for 79, :9621 for 81). True only for the four party-buff Formation skills; every other skill
    ///     number's confirm never touched the marker in the first place, so resetting it would be meaningless
    ///     (not harmful -- <see cref="Zone.ResetPartyBuffMarker" /> is unconditional either way -- but callers
    ///     should still gate the call on this so the intent, "this effect resolution was the marker's own
    ///     consumer," stays legible at the call site rather than resetting on every successful confirm of any
    ///     kind).
    /// </summary>
    public static bool ShouldResetPartyBuffMarkerOnConfirmSuccess(int skillNumber)
    {
        return FormationSkillCatalog.IsPartyBuffSkill(skillNumber);
    }
}

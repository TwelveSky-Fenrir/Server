using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     B17-rebirth-hook: applies the post-general-level-cap experience routine (legacy
///     <c>MyUtil::ProcessForExperience</c>'s own Stage A/Stage B fork, and <c>ProcessForExperience2</c>'s
///     rebirth-tier ladder) once a character is already at the general-level cap (
///     <see cref="LevelProgressionCalculator.MaxLevel" />).
///     See <see cref="HighLevelExperienceResolver" /> for the pure resolver this orchestrates, and
///     <see cref="HighLevelExperienceInputFactory" />/<see cref="HighLevelExperienceOutcomeApplier" /> for the
///     two adapter halves either side of it.
///     <para>
///         <b>Not yet wired in.</b> This file only ADDS a new method (<see cref="ApplyHighLevelExperienceGain" />)
///         -- it does not call it from anywhere, and it never edits <c>Zone.Combat.cs</c>'s own
///         <c>ApplyCharacterExperienceGain</c> (the single shared level-up cascade both the monster-kill and
///         PvP-kill award chokepoints already funnel through). The one-line fork a later Domain-combat
///         integration pass needs to add there is documented as this workstream's own wiringManifest entry,
///         not applied here.
///     </para>
/// </summary>
public sealed partial class Zone
{
    /// <summary>
    ///     Sort=11 on <c>AvatarStateFlagResponse</c> -- the rebirth-tier (<c>aLevel2</c>) level-up
    ///     notification, the high-level-track counterpart to <c>Zone.Combat.cs</c>'s own
    ///     <c>LevelUpAvatarChangeInfoSort</c> (Sort=1, the ordinary Level1 level-up). Per this workstream's own
    ///     prior-session notes this discriminator was independently confirmed; re-verify against a live
    ///     STRUCT.h citation before treating it as load-bearing beyond this codebase's own record of that
    ///     confirmation.
    /// </summary>
    private const int RebirthTierLevelUpAvatarChangeInfoSort = 11;

    /// <summary>
    ///     S018ZONE_101_TIME -- the same <c>AvatarStatUpdateResponse</c> sub-code
    ///     <see cref="Simulation.TimedBuffCountdownSystem" />'s own per-minute zone-101 paid-zone countdown
    ///     already uses (see that class's own remarks) -- reused here for the one-shot +10,000 rebirth-tier
    ///     0-to-1 bonus this method applies (<see cref="HighLevelExperienceResolver.Zone101TimeGrantOnFirstRebirthTier" />).
    /// </summary>
    private const int Zone101TimeStatSort = 18;

    /// <summary>
    ///     Applies one post-general-level-cap experience award to <paramref name="target" />: resolves it via
    ///     <see cref="HighLevelExperienceResolver" /> and applies whichever of Stage A (general-pool fill) or
    ///     Stage B (rebirth-tier ladder: level-up or proportional accrual) fires. Intended to be called from
    ///     <c>Zone.Combat.cs</c>'s <c>ApplyCharacterExperienceGain</c> in place of its ordinary level-up branch
    ///     whenever <see cref="HighLevelExperienceResolver.AppliesAt" /> is true for <paramref name="target" />
    ///     -- see this file's own wiring note.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ (B17-rebirth-hook contract) : Server/ts25zone/S07_MyGame03.cpp:161-424.
    ///     <para>
    ///         <b>NOT sent here -- open question, not invented.</b> The client-sync <c>AvatarStatUpdateResponse</c>
    ///         sub-codes for <see cref="HighLevelExperienceOutcomeKind.MainPoolFill" /> (Stage A pool-fill) and
    ///         <see cref="HighLevelExperienceOutcomeKind.RebirthTierAccrual" /> (aExp2 proportional growth) were
    ///         not recoverable from the source contract, which cites them only by MEANING
    ///         (S07_MyGame03.cpp:217,300 for Stage A; :417,420 for Stage-B accrual), never by numeric Sort
    ///         value. State is still mutated and marked dirty (<see cref="DirtyFlags.Progression" />) for both,
    ///         so the next login/zone-transfer snapshot is correct -- only the LIVE push is missing until a
    ///         wire-protocol finding confirms the sub-code. One promising, UNVERIFIED lead for that finding:
    ///         <c>Zone.Combat.cs</c>'s own <c>ExperienceStatSort</c> (Sort=13) constant cites its STRUCT.h
    ///         macro as <c>S013CHARACTER_EXP1_2</c> -- the "_2" suffix may mean this already-live Sort=13 code
    ///         carries Exp2 in its own <c>Value2</c> field (today hardcoded to 0 by
    ///         <c>ApplyCharacterExperienceGain</c>), which would make a dedicated new sub-code unnecessary for
    ///         at least the accrual case. Do not act on this without a cpp-protocol-header-analyst confirmation
    ///         against the live STRUCT.h line -- flagged here, not applied.
    ///     </para>
    /// </remarks>
    /// <param name="target">The awarding character.</param>
    /// <param name="gain">The original, un-clamped experience amount this award is granting.</param>
    /// <param name="antiCheatExperienceFlagged">See <see cref="HighLevelExperienceInputFactory.Build" />.</param>
    public void ApplyHighLevelExperienceGain(PlayerRuntimeState target, int gain,
        bool antiCheatExperienceFlagged = false)
    {
        var input = HighLevelExperienceInputFactory.Build(target, gain, antiCheatExperienceFlagged,
            worldData.LevelsByLevel);
        var outcome = HighLevelExperienceResolver.Resolve(input);

        HighLevelExperienceOutcomeApplier.Apply(target, outcome);

        switch (outcome.Kind)
        {
            case HighLevelExperienceOutcomeKind.None:
                return;

            case HighLevelExperienceOutcomeKind.MainPoolFill:
            case HighLevelExperienceOutcomeKind.RebirthTierAccrual:
                // Counters already mutated above; live client-sync sub-code unresolved -- see this method's
                // own <remarks/>. Still durable: mark dirty so the write-behind flush persists the new totals.
                target.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                return;

            case HighLevelExperienceOutcomeKind.RebirthTierLevelUp:
                ApplyRebirthTierLevelUp(target, outcome);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Kind,
                    "Unhandled HighLevelExperienceOutcomeKind.");
        }
    }

    /// <summary>
    ///     The <see cref="HighLevelExperienceOutcomeKind.RebirthTierLevelUp" /> branch's own zone-scoped tail:
    ///     full derived-stat recompute from currently-equipped gear, heal-to-new-maximums (gated on being
    ///     alive), dirty-mark, and the two broadcasts (discriminator-11 state-flag always; the one-shot
    ///     zone-101 bonus only on the 0-to-1 transition). Mirrors <c>Zone.Combat.cs</c>'s own
    ///     <c>ApplyCharacterExperienceGain</c> ordinary level-up branch's recompute/heal shape verbatim --
    ///     duplicated rather than shared via <see cref="RecomputeStatsAndBroadcastBuffs" />, since that helper
    ///     unconditionally emits an unrelated buff-view broadcast this routine must not send.
    /// </summary>
    private void ApplyRebirthTierLevelUp(PlayerRuntimeState target, HighLevelExperienceOutcome outcome)
    {
        var equipmentContainer = target.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, target.PetGrowth, target.PetActivity,
            worldData.ItemsById);
        var attributes = new CharacterBaseAttributes(target.StatVit, target.StatStr, target.StatInt,
            target.StatDex, target.Level, target.Tribe, target.PreviousTribe, target.Title, target.Halo,
            target.RebirthCount, target.Level2);
        var stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, target.Buffs,
            petContribution);

        target.Stats = stats;
        target.MaxLife = stats.MaxLife;
        target.MaxMana = stats.MaxMana;

        // SetBasicAbilityFromEquip's cache-write above happens unconditionally; the heal itself is gated on
        // the character being alive -- same convention ApplyCharacterExperienceGain's own level-up branch
        // follows (S07_MyGame03.cpp:285-289, cited there).
        if (target.Life > 0)
        {
            target.Life = stats.MaxLife;
            target.Mana = stats.MaxMana;
        }

        target.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression | DirtyFlags.Vitals);

        BroadcastAvatarStateFlag(target, RebirthTierLevelUpAvatarChangeInfoSort, target.Level2,
            target.StatPoints, target.SkillPoints);

        if (outcome.Zone101TimeBonus > 0)
            target.Session.Send(new AvatarStatUpdateResponse
                { Sort = Zone101TimeStatSort, Value = target.Zone101Time, Value2 = 0 });
    }
}

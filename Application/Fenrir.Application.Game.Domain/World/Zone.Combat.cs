using System.Threading.Channels;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>
    ///     "One specific server number/tribe combination" etc. from the CP-formula's residual terms are NOT
    ///     modeled here -- see <see cref="PvpKillContributionPointCalculator" />'s own remarks.
    /// </summary>
    private const int CombinedLevelGapCap = 13;

    /// <summary>
    ///     War Point stat-update code for <c>AvatarStatUpdateResponse</c> (the "S9xxUPDATE_..." family already
    ///     documented on that packet's own <c>Sort</c> field, e.g. <c>S904UPDATE_HERO_POINT</c>) --
    ///     <c>S905UPDATE_WAR_POINT</c> per the source behavior contract's own naming, extrapolated to the literal
    ///     905 by that exact same numeric-prefix convention rather than independently re-verified against a
    ///     <c>DEFINE.h</c> line; flag for re-verification if byte-exact parity on this one code is required.
    /// </summary>
    private const int WarPointStatSort = 905;

    /// <summary>
    ///     DS/Blood Point avatar-change-info sort (<c>SendDSPoint</c>, <c>Server/ts25zone/S07_MyGame03.cpp:2354-2361</c>)
    ///     -- confirmed 300.
    /// </summary>
    private const int BloodPointAvatarChangeInfoSort = 300;

    /// <summary>Raw, unvalidated CZ_PROCESS_ATTACK_SEND requests, resolved entirely on the tick thread (zero-SQL combat).</summary>
    private readonly Channel<CombatCommand> _combatInbox = Channel.CreateBounded<CombatCommand>(
        new BoundedChannelOptions(4096) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>
    ///     Independent 2-minute same-victim-pair cooldown for the FFA-335 flat CP override (step 5 of the
    ///     source contract) -- deliberately a SEPARATE table from <see cref="_killCooldownTracker" /> (the main
    ///     C05 anti-farm gate) and from <see cref="_regularWarCpOverrideCooldown" />'s own table, matching
    ///     "using its own separate cooldown-state table" in the source contract.
    /// </summary>
    private readonly KillCooldownTracker _ffaCpOverrideCooldown = new();

    /// <summary>
    ///     Independent 2-minute same-victim-pair cooldown for the Regular-War-host flat CP/War Point/Blood Point
    ///     override -- its own table, entirely separate from <see cref="_ffaCpOverrideCooldown" /> and
    ///     <see cref="_killCooldownTracker" />: a pair's cooldown state in one override never affects the others.
    /// </summary>
    private readonly KillCooldownTracker _regularWarCpOverrideCooldown = new();

    /// <summary>
    ///     Process-wide PvP anti-farm gate (C05) -- shared across every <see cref="Zone" /> via
    ///     <see cref="ZoneRegistry" /> in production; defaults to a private instance in tests so each test zone
    ///     starts with a clean cooldown state.
    /// </summary>
    private readonly KillCooldownTracker _killCooldownTracker = killCooldownTracker ?? new KillCooldownTracker();

    /// <summary>
    ///     Null (production default) builds a private one from <paramref name="worldData" /> instead of the
    ///     process-wide singleton <see cref="ZoneRegistry" /> owns, so pre-existing test call sites that
    ///     construct a <see cref="Zone" /> directly keep compiling unchanged.
    /// </summary>
    private readonly QuestCatalog _questCatalog = questCatalog ?? new QuestCatalog(worldData);

    public bool PostCombatCommand(in CombatCommand command)
    {
        return _combatInbox.Writer.TryWrite(command);
    }

    private void DrainCombatCommands()
    {
        while (_combatInbox.Reader.TryRead(out var command))
            try
            {
                ApplyCombatCommand(in command);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} combat command from character {CharacterId} failed", MapId,
                    command.AttackerCharacterId);
            }
    }

    /// <summary>
    ///     Resolves one CZ_PROCESS_ATTACK_SEND request (<c>mCase</c> 1-6 dispatch) entirely on this zone's own
    ///     tick thread. <c>mCase</c> 2 (Avatar -&gt; Avatar, enemy tribe), 3 (Avatar -&gt; Monster), 5 (Stun,
    ///     <see cref="ApplyStunAttack" />) and 6 (UnStun, <see cref="ApplyUnstunAttack" />) are implemented --
    ///     see <see cref="CombatResolver" />'s remarks for what else is/isn't modeled and why.
    ///     Every unimplemented case is a silent no-op, not a disconnect: <c>AttackHandler</c> already rejected
    ///     any <c>mCase</c> outside 1-6, so an in-range-but-unwired value is an in-progress feature, not a hostile packet.
    /// </summary>
    private void ApplyCombatCommand(in CombatCommand command)
    {
        // mCase 4 is deliberately not handled here even though a client could send it: the legacy itself only
        // ever reaches ProcessAttack04 from the monster's own AI (S07_MyGame05.cpp:3961) -- see ResolveMonsterAttack.
        if (command.AttackInfo.Case == 3)
        {
            ApplyPvmAttack(command);
            return;
        }

        if (command.AttackInfo.Case == 5)
        {
            ApplyStunAttack(command);
            return;
        }

        if (command.AttackInfo.Case == 6)
        {
            ApplyUnstunAttack(command);
            return;
        }

        if (command.AttackInfo.Case != 2)
            return; // mCase 1/4 -- deliberately unimplemented, see method remarks.

        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;
        if (!_players.TryGetValue(command.AttackInfo.ServerIndex2, out var defenderState))
            return;

        var attackerSnapshot = ToCombatantSnapshot(attackerState);
        var defenderSnapshot = ToCombatantSnapshot(defenderState);

        var attackSkill = command.AttackInfo.AttackActionValue1 == 2 &&
                          worldData.SkillsById.TryGetValue(command.AttackInfo.AttackActionValue2,
                              out var skillDef)
            ? skillDef
            : null;

        var outcome = CombatResolver.ResolveEnemyTribeAttack(attackerSnapshot, defenderSnapshot,
            command.AttackInfo, _clock, attackSkill, _random,
            ZonePvpZoneCatalog.AllowsEnemyTribeAttack(MapId));

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            attackerState.Buffs.Buff[8 * 2] = 0; // charge buff slot 8, value half -- single-use

        // "1 + attacker's weapon ItemId" on a hit (client picks the swing animation/effect from this), 0 on a miss.
        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = outcome.Hit ? 1 + attackerWeaponItemId : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.DamageApplied,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        var recipients = CombatRecipients(attackerState, defenderState);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit)
            return;

        defenderState.Life -= outcome.DamageApplied;
        defenderState.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        if (defenderState.Life <= 0)
        {
            ApplyPvpKillRewards(attackerState, defenderState);
            ApplyDeath(defenderState.CharacterId, DeathCause.PlayerKill);
        }
    }

    /// <summary>
    ///     PvP-kill reward pipeline (<c>MyUtil::ProcessForKillOtherTribe</c>, S07_MyGame03.cpp:2602-3248), gated
    ///     end-to-end by <see cref="KillCooldownTracker" /> (C05): repeatedly farming the same victim within
    ///     <see cref="KillCooldownTracker.DefaultCooldown" /> (10 min) only ever grants this reward once per
    ///     window for that ordered attacker/defender pair. Unlocks
    ///     <see cref="PlayerRuntimeState.MissionKillOtherTribe" /> (clamped at
    ///     <see cref="KillCooldownTracker.MissionKillOtherTribeCap" />, and now gated per-zone -- see
    ///     <see cref="PvpKillRewardZoneCatalog" />), the tower CP-for-PvP bonus (<see cref="ApplyTowerCpForPvpBonus" />),
    ///     the CP formula and its Regular-War-host/FFA-335 flat-amount overrides
    ///     (<see cref="ApplyPvpKillContributionPointFormula" />), hero-rank points (<see cref="ApplyPvpKillHeroPoints" />),
    ///     and character EXP (<see cref="ApplyPvpKillExperience" />).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         NOT modeled: item-drop payload (eligibility is real via <see cref="PvpKillZoneRewardProfile.GrantDrop" />,
    ///         but the drop routine's actual production behavior needs item ids/rates this agent's source
    ///         contract didn't have -- see <see cref="PvpKillRewardZoneCatalog" />'s remarks), same-IP gating
    ///         (no session-level IP address is exposed through <see cref="Fenrir.Network.Abstractions.IPacketSession" />
    ///         today), the event-popup kill counter (a separate reward channel this contract's source describes
    ///         but no popup-event state exists in Fenrir yet), the guild-level counter (no guild-membership
    ///         field existed on <see cref="PlayerRuntimeState" /> when this contract's numeric constants were
    ///         authored), and the double-kill-charge CP top-up / double-EXP-charge / warrior-scroll-buff /
    ///         premium-status bonuses (none of these buffs have an identified slot in Fenrir yet -- see
    ///         <see cref="PvpKillContributionPointCalculator" />/<see cref="PvpKillExperienceCalculator" />, which
    ///         accept them as parameters for when they do). The Regular-War-host CP override, including its War
    ///         Point/Blood Point grants, is fully wired (see <see cref="ApplyRegularWarCpOverride" />).
    ///     </para>
    ///     <para>
    ///         <paramref name="isStunTrigger" /> is the "stun vs. not" collapse of the legacy's three-value
    ///         kill-type marker -- always false from this method's only current caller (the ordinary HP-death
    ///         path in <see cref="ApplyCombatCommand" />). A future stun-chain integration point (the
    ///         non-death, party-wide trigger the source contract also describes) should call this method with
    ///         <c>isStunTrigger: true</c> once per present party member instead of duplicating this pipeline.
    ///     </para>
    /// </remarks>
    private void ApplyPvpKillRewards(PlayerRuntimeState attackerState, PlayerRuntimeState defenderState,
        bool isStunTrigger = false)
    {
        if (attackerState.CharacterId == defenderState.CharacterId)
            return;

        var attackerCombinedLevel = attackerState.Level + attackerState.RebirthCount;
        var defenderCombinedLevel = defenderState.Level + defenderState.RebirthCount;
        if (attackerCombinedLevel - defenderCombinedLevel > CombinedLevelGapCap)
            return;

        if (!_killCooldownTracker.TryRegisterKill(attackerState.CharacterId, defenderState.CharacterId,
                DateTime.UtcNow))
            return;

        var profile = PvpKillRewardZoneCatalog.Resolve(MapId, isStunTrigger);

        if (profile.GrantDailyMissionProgress)
        {
            attackerState.MissionKillOtherTribe =
                Math.Min(attackerState.MissionKillOtherTribe + 1, KillCooldownTracker.MissionKillOtherTribeCap);
            attackerState.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        }

        if (profile.GrantContributionPoints)
            ApplyTowerCpForPvpBonus(attackerState);

        ApplyPvpKillContributionPointFormula(attackerState, defenderState, profile);
        ApplyPvpKillHeroPoints(attackerState, profile, attackerCombinedLevel);
        ApplyPvpKillExperience(attackerState, profile, attackerCombinedLevel, defenderCombinedLevel);
    }

    /// <summary>
    ///     Tower CP-for-PvP consumption hook (<c>MyUtil::ProcessForKillOtherTribe</c> folding its tower-bonus
    ///     term into <c>tCPAddNum1</c>, <c>Server/ts25zone/S07_MyGame03.cpp:2602-2668</c>): awards the killer's
    ///     tribe's flat CP-for-PvP tower bonus on a qualifying enemy-tribe kill credit. Gated by the same C05
    ///     anti-farm cooldown as <see cref="PlayerRuntimeState.MissionKillOtherTribe" /> (both are the same
    ///     underlying "kill credit" event, and Fenrir has already chosen to anti-farm-gate rewards on this
    ///     path) rather than an ungated per-kill hook, and now also by
    ///     <see cref="PvpKillZoneRewardProfile.GrantContributionPoints" />
    ///     (its only caller, <see cref="ApplyPvpKillRewards" />, already resolves the FFA-335 zone's own
    ///     "CP disabled here, use the dedicated override instead" flag -- this closes exactly the FFA half of
    ///     this method's own previously-documented gap).
    /// </summary>
    /// <remarks>
    ///     Legacy additionally bypasses this whole award path during the Regular War RvR event
    ///     (<c>mGAME.mCheckZone049TypeServer</c>). <see cref="regularWarActiveMapTracker" /> now exposes that
    ///     state to <see cref="Zone" /> (see <see cref="ApplyRegularWarCpOverride" />), but this specific bypass
    ///     was outside the source contract this method's own gap note was written from -- not applying it here
    ///     is still a documented gap, no longer because the state is unavailable, but because reproducing the
    ///     bypass itself needs its own separately-cited behavior contract. Only the tower term itself is
    ///     reproduced: the other additive CP modifiers legacy folds into the same <c>tCPAddNum1</c> accumulator
    ///     are handled separately by
    ///     <see cref="ApplyPvpKillContributionPointFormula" />/<see cref="PvpKillContributionPointCalculator" />.
    /// </remarks>
    private void ApplyTowerCpForPvpBonus(PlayerRuntimeState attackerState)
    {
        var bonus = towerWar?.GetTribeBonus(attackerState.Tribe).CpForPvpBonus ?? 0;
        if (bonus > 0)
            GrantContributionPoints(attackerState.CharacterId, bonus);
    }

    /// <summary>
    ///     CP-formula grant (step 1/6 of the source contract) and its two dedicated flat-amount overrides
    ///     (Regular-War-host and FFA-335, step 5) -- <see cref="PvpKillContributionPointCalculator" /> owns every
    ///     constant/formula piece; this method only resolves which path applies and performs the actual grant
    ///     through <see cref="GrantContributionPoints" />.
    /// </summary>
    private void ApplyPvpKillContributionPointFormula(PlayerRuntimeState attackerState,
        PlayerRuntimeState defenderState, PvpKillZoneRewardProfile profile)
    {
        // Mutually exclusive with the FFA-335 branch below by construction: MapId can never equal both
        // PvpKillRewardZoneCatalog.FfaMapNumber and one of RegularWarMapCatalog's 11 configured maps.
        if (regularWarActiveMapTracker?.IsBattleInProgress(MapId) == true)
        {
            ApplyRegularWarCpOverride(attackerState, defenderState);
            return; // always suppresses the generic grant below for this kill, same as the FFA branch does.
        }

        if (MapId == PvpKillRewardZoneCatalog.FfaMapNumber)
        {
            // Own independent 2-minute same-pair cooldown, separate from the C05 tracker above -- always
            // disables the generic formula-based grant below (there is no zone-335 case in that path anyway,
            // since PvpKillRewardZoneCatalog.Resolve already returns GrantContributionPoints=false for it).
            if (_ffaCpOverrideCooldown.TryRegisterKill(attackerState.CharacterId, defenderState.CharacterId,
                    DateTime.UtcNow, PvpKillContributionPointCalculator.FlatOverrideCooldown))
            {
                var granted = PvpKillContributionPointCalculator.ClampGrant(attackerState.ContributionPoints,
                    PvpKillContributionPointCalculator.FfaOverrideFlatAmount,
                    PvpKillContributionPointCalculator.PlaceholderHardCap);
                GrantContributionPoints(attackerState.CharacterId, granted);
            }

            return;
        }

        if (!profile.GrantContributionPoints)
            return;

        // Premium status/warrior-scroll buff are not modeled in Fenrir yet -- always false until a buff slot
        // for each is identified (see PvpKillContributionPointCalculator's own remarks for the other three
        // formula terms this deliberately omits entirely).
        var baseAmount = PvpKillContributionPointCalculator.ComputeBaseAmount(
            false,
            false);

        var grantedAmount = PvpKillContributionPointCalculator.ClampGrant(attackerState.ContributionPoints,
            baseAmount, PvpKillContributionPointCalculator.PlaceholderHardCap);
        GrantContributionPoints(attackerState.CharacterId, grantedAmount);

        // Double-kill-charge top-up (a second helping of the same computed amount, consuming one charge) is
        // deliberately not modeled: no buff slot for a "double-kill charge" is identified in Fenrir yet.
    }

    /// <summary>
    ///     Regular-War-host flat CP/War Point/Blood Point kill-reward override (<c>RegularWar_ProcessKillReward</c>,
    ///     S07_MyGame03.cpp:2402-2457): fires only while <see cref="regularWarActiveMapTracker" /> reports this
    ///     zone's Regular War schedule as actively in its capture/score window
    ///     (<see cref="Fenrir.Application.Game.Domain.World.ZoneWar.RegularWarPhase.Active" />). Its only caller
    ///     already guarantees attacker/defender are non-null, distinct, and resolved from <see cref="_players" />
    ///     -- which, by construction (see <see cref="HandleEnter" />), means both are already fully entered and
    ///     "ready," matching the source contract's ready-state precondition without a separate flag to check.
    ///     Fenrir also has no bounded per-session slot array to range-check (<see cref="_players" /> is keyed by
    ///     <see cref="PlayerRuntimeState.CharacterId" />, not a fixed 0-999 slot index), so the source contract's
    ///     slot-range guard has no unmodeled Fenrir equivalent to reproduce.
    /// </summary>
    /// <remarks>
    ///     War Point and Blood Point/DS Point are now granted here via <see cref="GrantWarPoints" />/
    ///     <see cref="GrantBloodPoints" /> (previously undeliverable: <see cref="PlayerRuntimeState" /> had no
    ///     backing counter for either currency -- the wire protocol already reserved <c>AvatarInfo.WarPoint</c>/
    ///     <c>AvatarInfo.BloodCoin</c>, but <c>AvatarInfoTemplates</c> still hardcodes both to 0, since neither is
    ///     hydrated from a persisted source yet -- see <see cref="PlayerRuntimeState.WarPoint" />'s own remarks).
    ///     <see cref="PvpKillContributionPointCalculator.RegularWarOverrideWarPointAmount" />/
    ///     <see cref="PvpKillContributionPointCalculator.RegularWarOverrideBloodPointAmount" /> are the confirmed
    ///     +2/+2 legacy amounts. Per the source contract, both grants are unconditional -- never gated by the CP
    ///     cap-clamp below, and (unlike the CP grant) not re-clamped by <see cref="PvpKillContributionPointCalculator.ClampGrant" />
    ///     either, since no comparable War Point/Blood Point cap was ever given by that contract.
    /// </remarks>
    private void ApplyRegularWarCpOverride(PlayerRuntimeState attackerState, PlayerRuntimeState defenderState)
    {
        if (!_regularWarCpOverrideCooldown.TryRegisterKill(attackerState.CharacterId, defenderState.CharacterId,
                DateTime.UtcNow, PvpKillContributionPointCalculator.FlatOverrideCooldown))
            return;

        var granted = PvpKillContributionPointCalculator.ClampGrant(attackerState.ContributionPoints,
            PvpKillContributionPointCalculator.RegularWarOverrideFlatCpAmount,
            PvpKillContributionPointCalculator.PlaceholderHardCap);
        GrantContributionPoints(attackerState.CharacterId, granted);

        GrantWarPoints(attackerState.CharacterId, PvpKillContributionPointCalculator.RegularWarOverrideWarPointAmount);
        GrantBloodPoints(attackerState.CharacterId,
            PvpKillContributionPointCalculator.RegularWarOverrideBloodPointAmount);
    }

    /// <summary>
    ///     Hero-rank point dispatch (step 8 of the source contract, <c>MyCenterCom::AddHeroRankPoint</c>,
    ///     S06_MyUpperCom02.cpp:774-820): only ever nonzero via the FFA-335 zone profile today (see
    ///     <see cref="PvpKillRewardZoneCatalog" />). Gated on <paramref name="attackerCombinedLevel" /> being at
    ///     least <see cref="PvpKillRewardZoneCatalog.HeroPointMinimumCombinedLevel" /> -- silently dropped below
    ///     that floor, matching the source contract's "not queued or deferred" wording.
    /// </summary>
    private void ApplyPvpKillHeroPoints(PlayerRuntimeState attackerState, PvpKillZoneRewardProfile profile,
        int attackerCombinedLevel)
    {
        if (profile.HeroPointAmount <= 0)
            return;
        if (attackerCombinedLevel < PvpKillRewardZoneCatalog.HeroPointMinimumCombinedLevel)
            return;

        attackerState.HeroRankPoints += profile.HeroPointAmount;
        _heroRankPointAccumulator.AddPending(attackerState.CharacterId, profile.HeroPointAmount, attackerState.Tribe,
            attackerState.Level);
    }

    /// <summary>
    ///     EXP grant (step 10 of the source contract) -- reuses <see cref="ApplyCharacterExperienceGain" />, the
    ///     same level-up cascade <see cref="GrantMonsterKillExperience" /> already runs, so a PvP kill's level-up
    ///     behaves identically to a monster kill's. Pet-experience and mount-activity-experience grants are
    ///     deliberately not modeled: neither a pet-experience counter nor a mount-experience counter exists on
    ///     <see cref="PlayerRuntimeState" /> today (<see cref="PetGrowthCalculator" />'s own growth counter is
    ///     a stat-contribution input, not an experience track;
    ///     <see cref="Fenrir.Application.Game.Domain.Mounts.MountStateResolver" />'s own
    ///     remarks note the per-slot experience arrays this would need don't exist yet either).
    /// </summary>
    private void ApplyPvpKillExperience(PlayerRuntimeState attackerState, PvpKillZoneRewardProfile profile,
        int attackerCombinedLevel, int defenderCombinedLevel)
    {
        if (!profile.GrantExperience)
            return;

        // Warrior-scroll/double-EXP-charge buffs are not modeled in Fenrir yet -- see
        // PvpKillExperienceCalculator's own remarks for why the base amount and zone multiplier are also
        // placeholders rather than the real per-defender-level table/per-zone table the source contract calls for.
        var gain = PvpKillExperienceCalculator.ComputeGain(
            PvpKillExperienceCalculator.PlaceholderBaseAmountPerKill,
            attackerCombinedLevel,
            defenderCombinedLevel,
            false,
            false);

        if (gain > 0)
            ApplyCharacterExperienceGain(attackerState, gain);
    }

    /// <summary>
    ///     <c>ProcessForCP</c>'s floor clamp (the legacy primitive both <c>S07_MyGame02.cpp:2788</c>'s CP-for-PvM
    ///     milestone and <c>S07_MyGame03.cpp:2653</c>'s CP-for-PvP award funnel through): adds
    ///     <paramref name="amount" /> (may be negative in principle, though every current caller only ever
    ///     passes a positive tower bonus) to a character's Contribution Points, never letting the result go
    ///     below 0.
    /// </summary>
    public void GrantContributionPoints(int characterId, int amount)
    {
        if (amount == 0 || !_players.TryGetValue(characterId, out var state))
            return;

        state.ContributionPoints = Math.Max(0, state.ContributionPoints + amount);
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    /// <summary>
    ///     War Point grant -- shared by <see cref="ApplyRegularWarCpOverride" /> (<c>RegularWar_AddWarPoint</c>,
    ///     <c>Server/ts25zone/S07_MyGame03.cpp:2453</c>) and <c>MonsterSpawnScheduler</c>'s boss/event drop tier
    ///     (<c>World.Loot.BossEventDropResolver</c>, identifiers 746/9001) -- the first two callers of what had
    ///     been, until now, an entirely unmodeled currency (no backing counter existed on
    ///     <see cref="PlayerRuntimeState" />). Pushes a single <see cref="WarPointStatSort" />-coded
    ///     <c>AvatarStatUpdateResponse</c> to the granted character's own client only, never AOI-broadcast --
    ///     matching every other <c>AvatarStatUpdateResponse</c> emission in this codebase
    ///     (<c>DrinkBottleHandler</c>/<c>RankBuffHandler</c>/<c>PlaytimeBuffHandler</c>).
    /// </summary>
    public void GrantWarPoints(int characterId, int amount)
    {
        if (amount == 0 || !_players.TryGetValue(characterId, out var state))
            return;

        state.WarPoint += amount;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        state.Session.Send(new AvatarStatUpdateResponse
            { Sort = WarPointStatSort, Value = state.WarPoint, Value2 = 0 });
    }

    /// <summary>
    ///     DS/Blood Point grant (legacy <c>aBloodCoin</c>) -- <c>SendDSPoint</c>
    ///     (<c>Server/ts25zone/S07_MyGame03.cpp:2354-2361</c>): increments <c>aBloodCoin</c>, pushes
    ///     <see cref="BloodPointAvatarChangeInfoSort" /> to the granted character's own client only -- a single
    ///     <c>AvatarStateFlagResponse</c> send, NOT the AOI-wide <see cref="BroadcastAvatarStateFlag" /> fan-out
    ///     every other sort on that packet type in this codebase uses; "Send", not "Broadcast", matches the
    ///     legacy function's own name and this behavior's own source contract ("pushed to the killer's own
    ///     client").
    /// </summary>
    public void GrantBloodPoints(int characterId, int amount)
    {
        if (amount == 0 || !_players.TryGetValue(characterId, out var state))
            return;

        state.BloodCoin += amount;
        state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        state.Session.Send(new AvatarStateFlagResponse
        {
            ServerIndex = state.CharacterId,
            UniqueNumber = state.UniqueNumber,
            Sort = BloodPointAvatarChangeInfoSort,
            Value01 = state.BloodCoin,
            Value02 = 0,
            Value03 = 0
        });
    }

    /// <summary>
    ///     mCase 3, Avatar -&gt; Monster. Reuses <see cref="TryDamageMonster" /> for the HP mutation/death
    ///     handoff so there is exactly one code path that ever decides "this monster just died."
    /// </summary>
    /// <remarks>
    ///     Tower-guardian sub-branch (legacy <c>mSpecialSortNumber</c> == 10, S07_MyGame02.cpp:2107-2158):
    ///     identified here by the target's reserved negative <see cref="MonsterEntity.ServerIndex" /> (see
    ///     <see cref="TowerWarState.GuardianServerIndex" />) rather than a stored sort-number field, since only
    ///     a tower's own guardian ever occupies that reserved range (<see cref="Monsters.MonsterSpawnScheduler" />'s
    ///     regular per-region pool starts at index 1). See <see cref="TowerFriendlyFireGate" /> for the
    ///     authorization rule itself.
    /// </remarks>
    private void ApplyPvmAttack(in CombatCommand command)
    {
        if (!_players.TryGetValue(command.AttackerCharacterId, out var attackerState))
            return;
        if (!_monsters.TryGetValue(command.AttackInfo.ServerIndex2, out var monster))
            return;
        if (monster.UniqueNumber != command.AttackInfo.UniqueNumber2)
            return;

        var towerIndex = TowerZoneIndexTable.GetTowerIndex(MapId);
        var isTowerGuardian = towerIndex >= 0 && monster.ServerIndex == TowerWarState.GuardianServerIndex(towerIndex);

        if (isTowerGuardian && !CanAttackTowerGuardian(attackerState.Tribe, towerIndex))
            return; // silent no-op -- same as every other ProcessAttack03 rejection path

        var attackerSnapshot = ToCombatantSnapshot(attackerState);
        var outcome = MonsterCombatResolver.ResolvePvmAttack(attackerSnapshot, monster, command.AttackInfo, _clock,
            _random);

        if (outcome.Rejected)
            return;

        if (outcome.ChargeConsumed)
            attackerState.Buffs.Buff[8 * 2] =
                0; // charge buff slot 8, value half -- single-use, same convention as mCase 2

        var attackerWeaponItemId = attackerState.Inventory.GetSlot(ContainerMatrix.Equipment, 7)?.ItemId ?? 0;
        var response = new AttackResponse
        {
            AttackInfo = command.AttackInfo with
            {
                AttackResultValue = outcome.Hit ? 1 + attackerWeaponItemId : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                AttackViewDamageValue = outcome.DamageApplied,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        var recipients = new HashSet<int> { attackerState.CharacterId };
        foreach (var id in _grid.Neighbors(attackerState.CurrentCell)) recipients.Add(id);
        foreach (var id in NeighborsOfPosition(monster.PosX, monster.PosZ)) recipients.Add(id);
        BroadcastAttackResult(recipients, response);

        if (!outcome.Hit)
            return;

        TryDamageMonster(monster.ServerIndex, outcome.DamageApplied, attackerState.CharacterId, out _, out _);

        if (isTowerGuardian)
            ApplyTowerGuardianHitSideEffects(towerIndex, attackerState);
    }

    /// <summary>
    ///     Legacy <c>CanAttackTower</c> call + <c>towerTribe</c> resolution (S07_MyGame01.cpp:13575-13615,
    ///     S07_MyGame02.cpp:2119-2143) -- see <see cref="TowerFriendlyFireGate" /> for the actual rule this
    ///     assembles the inputs for.
    /// </summary>
    private bool CanAttackTowerGuardian(byte attackerTribe, int towerIndex)
    {
        var owningTribe = TowerZoneIndexTable.GetOwningTribe(MapId);
        var towerActivelyBuilt = towerWar?.GetPhase(towerIndex) == TowerSiegePhase.Active;
        var allyOfOwningTribe = owningTribe is { } owner ? worldState?.GetAllyOf(owner) : null;

        return TowerFriendlyFireGate.CanAttackGuardian(attackerTribe, owningTribe, towerActivelyBuilt,
            allyOfOwningTribe);
    }

    /// <summary>
    ///     Legacy <c>SetAttackTower(0)</c>/<c>mTowerPostTick</c> refresh + the one-shot Center notification and
    ///     full tower-state rebroadcast (S07_MyGame02.cpp:2146-2153). The Center hop (broadcast code 754) has
    ///     no receiving process in Fenrir's two-executable topology -- the same collapse
    ///     <see cref="WorldStateService" />'s own remarks describe for the RvR hub -- so it is
    ///     logged rather than sent; no further modeled consequence exists for this behavior's own contract. The
    ///     client-facing rebroadcast is real: <see cref="TowerStatusResponse" /> to every player currently in
    ///     this zone (Fenrir has no separate "ready"/"mid-transfer" flag to filter on -- a player only ever
    ///     occupies <see cref="_players" /> once fully entered, see <see cref="HandleEnter" />).
    /// </summary>
    private void ApplyTowerGuardianHitSideEffects(int towerIndex, PlayerRuntimeState attackerState)
    {
        if (towerWar is null)
            return;

        var isFirstHit = towerWar.RecordGuardianHit(towerIndex, DateTime.UtcNow);
        if (!isFirstHit)
            return;

        var packedState = towerWar.GetPackedState(towerIndex);
        logger.LogInformation(
            "Tower siege started (Center broadcast 754): towerType={TowerType} attackerTribe={AttackerTribe} zoneServerNumber={ZoneServerNumber} attackerName={AttackerName}",
            TowerWarState.DecodeType(packedState), attackerState.Tribe, MapId, attackerState.Name);

        BroadcastTowerStatus();
    }

    private void BroadcastTowerStatus()
    {
        if (towerWar is null)
            return;

        var state1 = new int[TowerWarState.TowerCount];
        var state2 = new int[TowerWarState.TowerCount];
        for (var i = 0; i < TowerWarState.TowerCount; i++)
        {
            state1[i] = towerWar.GetPackedState(i);
            // Legacy mState2Tower is filled -1 on every DB read and never appears in any SQL query of its own
            // (ServerDocs/10_ts25center/02_HeroRank_Guilde_Discord_Votes.md:296-298) -- dead, always -1.
            state2[i] = -1;
        }

        var response = new TowerStatusResponse { State1Tower = state1, State2Tower = state2 };
        foreach (var player in _players.Values)
            player.Session.Send(response);
    }

    private CombatantSnapshot ToCombatantSnapshot(PlayerRuntimeState state)
    {
        return new CombatantSnapshot(
            state.CharacterId,
            state.Tribe,
            state.IsDead,
            state.Life,
            state.MaxLife,
            state.PosX,
            state.PosY,
            state.PosZ,
            state.ZoneEntryAtZoneClock,
            state.Stats ?? default,
            state.Buffs.Buff[8 * 2]);
    }

    /// <summary>
    ///     Attacker + defender + both their AOI neighbors, deduplicated -- matches the legacy's own "AOI broadcast +
    ///     unicast to the attacker" (contract doc on <c>AttackResponse</c>).
    /// </summary>
    private HashSet<int> CombatRecipients(PlayerRuntimeState attacker, PlayerRuntimeState defender)
    {
        var recipients = new HashSet<int> { attacker.CharacterId, defender.CharacterId };
        foreach (var id in _grid.Neighbors(attacker.CurrentCell)) recipients.Add(id);
        foreach (var id in _grid.Neighbors(defender.CurrentCell)) recipients.Add(id);
        return recipients;
    }

    private void BroadcastAttackResult(IEnumerable<int> recipientCharacterIds, in AttackResponse response)
    {
        foreach (var id in recipientCharacterIds)
            try
            {
                if (_players.TryGetValue(id, out var recipient))
                    recipient.Session.Send(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Zone {MapId} attack-result send to character {RecipientId} failed", MapId,
                    id);
            }
    }

    /// <summary>
    ///     XP hook called once a monster's death resolves its killer. Folds in the killer's tribe's tower XP
    ///     bonus (see the in-method remarks) -- the tower bonus never reaches the party bonus below.
    /// </summary>
    /// <param name="partyMemberIds">
    ///     Killer's full party roster; null/empty for solo. The base gain above is always solo/killer-only; a
    ///     separate flat party bonus (10/20/30/50% of raw XP by 2-5 present members) goes to every present,
    ///     non-dead member in this same zone, including the killer again (verified: the killer's own record
    ///     also matches the source's party-name filter, which does not exclude it).
    /// </param>
    public void GrantMonsterKillExperience(int killerCharacterId, int monsterLevel, int monsterGeneralExperience,
        IReadOnlyList<int>? partyMemberIds = null, int monsterPatExperience = 0, int monsterLifeValue = 0)
    {
        if (!_players.TryGetValue(killerCharacterId, out var state))
            return;

        var fixedLevel = ExperienceFormulas.ReturnFixedLevel(state.Level);
        var rawGain = ExperienceFormulas.ComputeMonsterKillExperience(fixedLevel, monsterLevel,
            monsterGeneralExperience);

        // Tower XP-bonus consumption hook (MONSTER_OBJECT::ProcessForExp, S07_MyGame05.cpp:3810-3814): added on
        // top of the already gap-scaled running gain, computed from the monster's own raw general-experience
        // value again (not rawGain above) -- the killer's own personal gain only, never the party bonus below.
        // Guarded the same way ComputeMonsterKillExperience's own first check is: an XP-less monster
        // contributes nothing regardless of tribe bonus.
        var xpBonusRatio = towerWar?.GetTribeBonus(state.Tribe).XpRatio ?? 0f;
        if (xpBonusRatio > 0f && monsterGeneralExperience >= 1)
            rawGain += (int)(monsterGeneralExperience * xpBonusRatio);

        var finalGain = ExperienceFormulas.ApplyRebirthDivisor(rawGain, state.Level);

        // MyUtil::ProcessForExperience's outer guard + health-value gain + pet-experience dispatch
        // (S07_MyGame03.cpp:161-322): all three gated together. isReady/isTransferringZone are always true/false
        // here -- a PlayerRuntimeState resolved from _players is by construction fully in-world and not mid
        // zone-transfer in Fenrir's session model, so this call site can never observe the legacy's other two
        // gate values.
        if (MonsterKillExperienceGate.ShouldProcess(true, false, finalGain, monsterPatExperience))
        {
            if (finalGain > 0)
                ApplyCharacterExperienceGain(state, finalGain);

            CreditPetGrowthFromMonsterKill(state, monsterPatExperience);

            var healthGain = MonsterKillHealthGainCalculator.ComputeHealthValueGain(monsterLifeValue);
            var newLife = MonsterKillHealthGainCalculator.ComputeNewLife(state.Life, state.MaxLife, healthGain);
            if (newLife != state.Life)
            {
                state.Life = newLife;
                state.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
            }
        }

        if (partyMemberIds is not { Count: > 0 })
            return;

        List<PlayerRuntimeState>? present = null;
        foreach (var memberId in partyMemberIds)
            if (_players.TryGetValue(memberId, out var member) && !member.IsDead)
                (present ??= []).Add(member);

        if (present is not { Count: >= 2 })
            return;

        var bonus = ExperienceFormulas.ComputePartyBonusExperience(present.Count, monsterGeneralExperience);
        if (bonus <= 0)
            return;

        foreach (var member in present)
            ApplyCharacterExperienceGain(member, bonus);
    }

    /// <summary>
    ///     Shared level-up cascade: mirrors <c>MyUtil::ProcessForExperience</c>'s own per-recipient logic
    ///     (S07_MyGame05.cpp:3916 for the monster-kill/party-bonus path; <c>ApplyPvpKillExperience</c> is this
    ///     method's other caller for the PvP-kill path). Hoisted out of <see cref="GrantMonsterKillExperience" />'s
    ///     own former local closure so both reward pipelines apply an identical level-up.
    /// </summary>
    private void ApplyCharacterExperienceGain(PlayerRuntimeState target, int gain)
    {
        var previousExperience = target.Experience;
        target.Experience += gain;
        target.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);

        var levelUp = LevelProgressionCalculator.ResolveLevelUp(previousExperience, gain, worldData.LevelsByLevel);
        if (!levelUp.LeveledUp)
            return;

        target.Level = levelUp.NewLevel;
        target.StatPoints += levelUp.StatPointsGranted;
        target.SkillPoints += levelUp.SkillPointsGranted;

        var equipmentContainer = target.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, target.PetGrowth, target.PetActivity,
            worldData.ItemsById);
        var attributes = new CharacterBaseAttributes(target.StatVit, target.StatStr, target.StatInt,
            target.StatDex, target.Level, target.Tribe, target.Title, target.Halo, target.RebirthCount);
        var stats = EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, target.Buffs,
            petContribution);

        target.Stats = stats;
        target.MaxLife = stats.MaxLife;
        target.MaxMana = stats.MaxMana;

        // SetBasicAbilityFromEquip's aMaxLifeValue/aMaxManaValue cache-write above happens unconditionally;
        // only the actual heal is gated on the character being alive (S07_MyGame03.cpp:285-289).
        if (target.Life > 0)
        {
            target.Life = stats.MaxLife;
            target.Mana = stats.MaxMana;
        }

        target.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);
    }

    /// <summary>
    ///     Monster-kill-only pet-growth credit (<see cref="PetExperienceCreditResolver" />, port of
    ///     <c>PETSYSTEM::ProcessForExperience</c>) -- deliberately not called from <see cref="ApplyPvpKillExperience" />;
    ///     see that method's own remarks for why PvP kills never grant pet experience. Takes the monster's raw
    ///     <c>PatExperience</c> (no global/personal-rate/double-time/premium scaling modeled yet, see
    ///     <see cref="PetExperienceCreditResolver" />'s own remarks). The reactivation/tier-crossing ability-recalc
    ///     broadcast the legacy fires here has no Fenrir equivalent wired yet -- the growth/activity counters are
    ///     mutated and flushed, but no client notification is sent, a documented gap.
    /// </summary>
    private void CreditPetGrowthFromMonsterKill(PlayerRuntimeState target, int monsterPatExperience)
    {
        if (monsterPatExperience < 1)
            return;

        var equipmentContainer = target.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;

        var credited = PetExperienceCreditResolver.Resolve(petItemId, target.PetGrowth, target.PetActivity,
            monsterPatExperience, worldData.ItemsById);

        if (!credited.IsEligible || (credited.CreditedAmount == 0 && !credited.ReactivationApplied))
            return;

        target.PetGrowth = credited.NewGrowth;
        target.PetActivity = (byte)credited.NewActivity;
        target.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
    }

    /// <summary>
    ///     Quest kill-hook alongside <see cref="GrantMonsterKillExperience" />, from the same kill. Increments
    ///     <see cref="PlayerRuntimeState.QuestKillCounter" /> by 1 when the killer's active quest is qSort 1 or
    ///     5 and <paramref name="monsterId" /> matches <see cref="PlayerRuntimeState.QuestTargetPhase" />, but
    ///     only while the counter is still below its target (qSort 1: below Solution2; qSort 5: still 0) --
    ///     this is the clamp, not an unbounded increment. Party propagation is not modeled here.
    /// </summary>
    public void ApplyQuestKillProgress(int killerCharacterId, int monsterId)
    {
        if (!_players.TryGetValue(killerCharacterId, out var state))
            return;

        if (state.QuestActiveFlag != 1 || state.QuestTargetPhase != monsterId)
            return;

        switch (state.QuestSort)
        {
            case 1:
                var quest = _questCatalog.TryGet(state.Tribe, state.QuestStepPermanent);
                if (quest is null)
                    return;
                if (state.QuestKillCounter < (quest.Quest.Solution2 ?? 0))
                {
                    state.QuestKillCounter++;
                    state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                }

                break;
            case 5:
                if (state.QuestKillCounter < 1)
                {
                    state.QuestKillCounter++;
                    state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
                }

                break;
        }
    }
}

using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     PvP attack resolution for both <c>mCase</c> 2 (enemy-tribe, <see cref="ResolveEnemyTribeAttack" />) and
///     <c>mCase</c> 1 (duel, <see cref="ResolveDuelAttack" />) -- both are a pure port of the same shared
///     <c>AttackPlayer</c> routine (S07_MyGame02.cpp:886-1416), differing only in which authorization gate runs
///     before the identical damage math (factored out into the shared private <c>ResolveDamage</c>). Never
///     mutates state itself; caller applies the outcome.
/// </summary>
/// <remarks>
///     Not implemented: Holy Shield (reflect-kill crediting the reflecting side is not modeled -- only the
///     direct kill in <c>Zone.ApplyCombatCommand</c>/<c>Zone.ApplyDuelAttack</c> reaches a reward pipeline).
///     PvM/MvP/stun are handled elsewhere (<c>Zone.ApplyPvmAttack</c>/<c>Zone.Stun.cs</c>). A qualifying kill
///     via <see cref="ResolveEnemyTribeAttack" /> does reach the PvP-kill reward pipeline
///     (<c>Zone.ApplyPvpKillRewards</c>: CP formula/FFA-override, hero-rank points, EXP,
///     <c>MissionKillOtherTribe</c>), gated end-to-end behind <see cref="KillCooldownTracker" /> (C05) --
///     item-drop payload and the event-popup counter remain unimplemented (see
///     <c>PvpKillRewardZoneCatalog</c>'s own remarks). A qualifying kill via <see cref="ResolveDuelAttack" />
///     deliberately does NOT reach that same reward pipeline -- see <c>Zone.ApplyDuelAttack</c>'s own remarks
///     for why (the source contract this method was authored from left kill-reward parity between the two
///     kill paths an explicitly open question, not something to assume).
///     Also not implemented: the tribe "Formation Skill" x1.1 ATK/DEF modifier gated on
///     <c>mWorldInfo->mTribeMasterCallAbility[tribe]</c> (S07_MyGame02.cpp:1071-1079) -- that world-scope buff state isn't
///     wired up anywhere yet (see <see cref="Fenrir.Application.Game.Handlers.Tribes.TribeActionHandler" /> tSort 5, which
///     always aborts because its own gating flag is never set); this modifier is contrasted with, not shared by,
///     <see cref="ResolveDuelAttack" />'s own flat (non-formation-adjusted) critical rule, which duel combat
///     never evaluates either way.
///     Also not implemented on the duel side: the zone124 unconditional x3-damage/forced-crit override in the
///     final 10s of a duel countdown (S07_MyGame02.cpp:1146-1150) -- see <c>Zone.ApplyDuelAttack</c>'s own
///     remarks for why.
///     PRESERVED VERBATIM: after the min-5 floor and crit doubling, damage is divided by
///     <see cref="MinimumDamageAgainstAvatar" /> (5) -- verified at two call sites, absent from PvM. Makes PvP damage ~5x
///     lower than raw ATK-DEF suggests; do not "fix".
///     Also deliberately absent, confirmed NOT a gap: no server-side attack-rate/minimum-inter-attack-delay
///     enforcement of any kind (weapon-type-keyed or otherwise) runs here or anywhere else in the combat
///     pipeline (<c>Zone.Combat.cs</c>'s <c>ApplyCombatCommand</c>/<c>ApplyPvmAttack</c>,
///     <c>Zone.Duel.cs</c>'s <c>ApplyDuelAttack</c>). This is exact parity with production ts25zone: the one
///     legacy mechanism that would have enforced this, <c>CheckAttackSpeed</c>/<c>CheckAttackSpeed3Hit</c>
///     (S07_MyGame02.cpp:1503-1646, a ~100-entry per-skill-number minimum-delay table), is dead code in
///     every shipped build variant -- its sole gating macro <c>FIX_ATK_SPD</c> is commented out at its only
///     definition site (H01_MainApplication.h:7) and is never injected via <c>ts25zone.vcxproj</c> either,
///     and no call site to the function exists anywhere in <c>Server/</c> regardless. Do NOT add a
///     weapon-type-based or skill-based attack-speed rate-limit here on the assumption legacy has one live
///     -- it doesn't, in any build variant. (<c>AttackPacketBudget</c>/<c>CheckAttackPacket</c>,
///     S07_MyGame02.cpp:1718-1761, is an unrelated, live sub-packet-count/replay guard -- see its own
///     remarks -- not an attack-speed timer, and must not be conflated with this note.)
/// </remarks>
public static class CombatResolver
{
    public const float MaxAttackDistance = 185.0f;

    /// <summary>Also the PvP-only final divisor -- see class remarks.</summary>
    public const int MinimumDamageAgainstAvatar = 5;

    /// <summary>20 legacy ticks = 10s anti-chain-attack window after either side last took damage.</summary>
    public const int ProtectTickLegacyTicks = 20;

    /// <summary>Skill 78 is excluded from the crit roll; unexplained in the source.</summary>
    private const int SkillNumberExcludedFromCritical = 78;

    /// <summary>
    ///     "No action yet" placeholder action-state (<c>CheckPossibleAttackTarget</c>'s avatar-target rule) --
    ///     see <see cref="AttackRejectReason.DefenderActionStateBlocksTargeting" />. Same value
    ///     <see cref="StunResolver" /> uses for its own copy of this same shared check.
    /// </summary>
    private const int NoActionYetSort = 0;

    /// <summary>Death-pose action-state (matches <c>Zone.ApplyDeath</c>'s own broadcast Sort).</summary>
    private const int DeathPoseSort = 12;

    public static readonly TimeSpan ProtectDuration = SimulationClock.ToTimeSpan(ProtectTickLegacyTicks);

    /// <param name="zoneAllowsEnemyTribeAttack">
    ///     Whether the zone/map this attack is occurring in has open-tribe PvP enabled. Legacy stores this as a
    ///     per-zone flag in a 350-entry table (Server/Header/S18_MyZoneInfo.cpp:9-393, defaulted to 0/disabled for
    ///     any zone id absent from the table, e.g. zone 39) and reads it once from a process-wide zone id fixed at
    ///     boot (Server/ts25zone/S01_MainApplication.cpp:236) since one legacy process serves exactly one zone;
    ///     Fenrir shards a disjoint *set* of maps per process, so callers must resolve this per the specific
    ///     zone/map the attack occurs in, not once per shard -- that lookup is this parameter's caller's
    ///     responsibility, not this method's. The legacy flag is tri-state (0/1/2) but both real call sites
    ///     (S07_MyGame02.cpp:947, :3579) test only equality-to-zero, so 1 and 2 collapse to the same "enabled"
    ///     outcome -- reduced to a bool here since no observed behavior distinguishes them. Defaults to
    ///     <c>true</c> so existing/test callers that don't yet source real per-zone data keep prior behavior.
    /// </param>
    /// <param name="sameTribeAttackExempt">
    ///     Whether the current zone is one of the two "open PvP" maps (zone 324 or FFAMAPNUM/335) where the
    ///     same-tribe/allied-tribe rejection immediately below is skipped entirely, so any tribe may attack any
    ///     tribe (Server/ts25zone/S07_MyGame02.cpp:952-958, the <c>!= 324 &amp;&amp; != FFAMAPNUM</c> guard
    ///     wrapping that check inside <c>AttackPlayer</c>'s non-duel branch). Resolved via
    ///     <see cref="ZonePvpZoneCatalog.IsSameTribeAttackExempt" /> -- caller's responsibility, same pattern as
    ///     <paramref name="zoneAllowsEnemyTribeAttack" />. Defaults to <c>false</c> (the ordinary, non-exempt
    ///     behavior) so existing/test callers that don't source this per-zone fact keep prior behavior.
    /// </param>
    /// <param name="newbieProtectionZone">
    ///     Whether the current zone is one of the nine home-tribe-district sub-zones (2, 3, 4, 7, 8, 9, 12, 13,
    ///     14 -- the three "capital plaza" zones 1/6/11 are deliberately excluded) where <c>AttackPlayer</c>
    ///     enforces an additional "newbie protection" level gate immediately after the same-tribe/alliance check
    ///     above: an attacker whose <see cref="CombatantSnapshot.Level" /> is &gt;= 90 may not attack a defender
    ///     whose <see cref="CombatantSnapshot.Level" /> is &lt; 90 (Server/ts25zone/S07_MyGame02.cpp:960-976).
    ///     Resolved via <see cref="ZonePvpZoneCatalog.IsNewbieProtectionZone" /> -- caller's responsibility, same
    ///     pattern as <paramref name="zoneAllowsEnemyTribeAttack" />/<paramref name="sameTribeAttackExempt" />.
    ///     Defaults to <c>false</c> (gate inactive) so existing/test callers that don't source this per-zone fact
    ///     keep prior behavior -- correct for the vast majority of zones, since only these nine are gated.
    /// </param>
    /// <param name="defenderPshopOpen">
    ///     <c>AttackPlayer</c>'s shared shop-open precondition (S07_MyGame02.cpp:901-933, specifically :917-920)
    ///     -- see <see cref="AttackRejectReason.DefenderShopOpen" />. Same input, same gate, as
    ///     <see cref="ResolveDuelAttack" />'s own parameter of the same name. Defaults to <c>false</c> (shop
    ///     closed) so existing/test callers that don't source this per-defender fact keep prior behavior; the
    ///     production call site (<c>Zone.ApplyCombatCommand</c>) always passes the defender's real live value.
    /// </param>
    /// <param name="defenderActionSort">
    ///     The defender's last-accepted avatar action Sort (<c>PlayerRuntimeState.ActionSort</c>) -- see
    ///     <see cref="AttackRejectReason.DefenderActionStateBlocksTargeting" />. Defaults to 1 (an ordinary,
    ///     already-acting pose -- neither <see cref="NoActionYetSort" /> nor <see cref="DeathPoseSort" />) so
    ///     existing/test callers that don't source this per-defender fact keep prior behavior; the production
    ///     call site (<c>Zone.ApplyCombatCommand</c>) always passes the defender's real live value instead.
    /// </param>
    /// <param name="allyOfAttackerTribe">
    ///     The tribe currently allied with <paramref name="attacker" />'s own tribe, or null if the attacker's
    ///     tribe is in no active alliance -- the live, world-scoped RvR alliance state
    ///     (<c>mGAME.ReturnAllianceTribe</c>) read fresh at attack time, not cached per-character: an alliance
    ///     that forms or dissolves mid-session changes who this rejects with no delay or grace window
    ///     (Server/ts25zone/S07_MyGame02.cpp:954). Resolved via
    ///     <see cref="Fenrir.Application.Game.Domain.World.WorldState.WorldStateService.GetAllyOf" /> against the
    ///     ATTACKER's own tribe specifically -- the underlying lookup is non-reflexive (never returns the tribe
    ///     passed to it), and this call site never reflects it back against the attacker's own tribe, so passing
    ///     the defender's ally here instead of the attacker's would not reproduce this gate. Caller's
    ///     responsibility, same pattern as <paramref name="zoneAllowsEnemyTribeAttack" />/
    ///     <paramref name="sameTribeAttackExempt" />/<paramref name="newbieProtectionZone" />. Defaults to
    ///     <c>null</c> (no active alliance) so existing/test callers that don't source this per-tribe fact keep
    ///     prior (same-tribe-only) behavior.
    /// </param>
    public static AttackOutcome ResolveEnemyTribeAttack(
        CombatantSnapshot attacker,
        CombatantSnapshot defender,
        AttackForProtocol request,
        TimeSpan zoneClock,
        SkillDefinition? attackSkill,
        IRandomSource rng,
        bool zoneAllowsEnemyTribeAttack = true,
        bool sameTribeAttackExempt = false,
        bool newbieProtectionZone = false,
        bool defenderPshopOpen = false,
        int defenderActionSort = 1,
        byte? allyOfAttackerTribe = null)
    {
        if (attacker.CharacterId == defender.CharacterId)
            return AttackOutcome.Reject(AttackRejectReason.SameCharacter);
        if (attacker.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (defender.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
        // AttackPlayer's shared precondition block (S07_MyGame02.cpp:901-933) also gates on the defender's
        // shop/action-state before the zone-wide open-PvP/same-tribe gate below -- the same precondition
        // ResolveDuelAttack already reproduces via its own identically-named parameters.
        if (defenderPshopOpen)
            return AttackOutcome.Reject(AttackRejectReason.DefenderShopOpen);
        if (defenderActionSort is NoActionYetSort or DeathPoseSort)
            return AttackOutcome.Reject(AttackRejectReason.DefenderActionStateBlocksTargeting);
        // Zone-wide open-PvP authorization gate -- legacy-faithful position is immediately before the
        // tribe/alliance check (S07_MyGame02.cpp:945-950, before :952-958); duels never evaluate this gate
        // (see ResolveDuelAttack's own duel-specific gate instead).
        if (!zoneAllowsEnemyTribeAttack)
            return AttackOutcome.Reject(AttackRejectReason.ZonePvpDisabled);
        // Same-tribe OR allied-tribe rejection (S07_MyGame02.cpp:954): friendly fire is blocked both between
        // same-tribe characters and between the attacker's tribe and whichever tribe it is currently allied
        // with (allyOfAttackerTribe, resolved by the caller against the live RvR alliance state -- see this
        // method's own parameter remarks). Zone 324 and FFAMAPNUM/335 skip this guard entirely -- any tribe may
        // attack any tribe there (S07_MyGame02.cpp:952-958). See ZonePvpZoneCatalog.IsSameTribeAttackExempt's
        // own remarks.
        if (!sameTribeAttackExempt && (attacker.Tribe == defender.Tribe || defender.Tribe == allyOfAttackerTribe))
            return AttackOutcome.Reject(AttackRejectReason.SameOrAlliedTribe);
        // Newbie-protection level gate -- home-tribe district sub-zones only (S07_MyGame02.cpp:960-976),
        // positioned immediately after the same-tribe/alliance check and before any damage resolution, matching
        // the legacy AttackPlayer ordering. Compares each side's raw aLevel1 alone (CombatantSnapshot.Level's
        // own remarks) -- not a combined Level+Level2 value.
        if (newbieProtectionZone && attacker.Level >= 90 && defender.Level < 90)
            return AttackOutcome.Reject(AttackRejectReason.NewbieProtectionLevelGap);

        return ResolveDamage(attacker, defender, request, zoneClock, attackSkill, rng);
    }

    /// <summary>
    ///     PvP attack resolution (<c>mCase</c> 1, duel) -- the same shared <c>AttackPlayer</c> routine as
    ///     <see cref="ResolveEnemyTribeAttack" /> (S07_MyGame02.cpp:886-1416), entered via
    ///     <c>PVP_ATTACK_TYPE::DUEL</c> instead of <c>::ENEMY</c>: the zone-wide open-PvP/same-tribe gate is
    ///     never evaluated for a duel attack (S07_MyGame02.cpp:935-943 vs. the contrasting :945-958) --
    ///     <paramref name="attackerAndDefenderShareActiveDuel" /> is this gate's caller-resolved replacement,
    ///     the same pattern <see cref="ResolveEnemyTribeAttack" />'s own <c>zoneAllowsEnemyTribeAttack</c>/
    ///     <c>sameTribeAttackExempt</c> parameters already use for their own per-zone facts.
    /// </summary>
    /// <param name="attackerAndDefenderShareActiveDuel">
    ///     True only when attacker and defender are both currently flagged as actively dueling, in the SAME
    ///     active duel, with opposite roles (S07_MyGame02.cpp:935-943) -- resolved by the caller
    ///     (<c>Zone.SharesActiveDuel</c>, already used identically for the stun-duel-exception gate, see
    ///     <see cref="StunAttemptRequest.AttackerAndDefenderShareActiveDuel" />), not by this pure resolver.
    /// </param>
    /// <param name="defenderPshopOpen">
    ///     <c>AttackPlayer</c>'s shared shop-open precondition (S07_MyGame02.cpp:901-933) -- see
    ///     <see cref="AttackRejectReason.DefenderShopOpen" />. Same input, same gate, as
    ///     <see cref="ResolveEnemyTribeAttack" />'s own parameter of the same name.
    /// </param>
    /// <param name="defenderActionSort">
    ///     The defender's last-accepted avatar action Sort (<c>PlayerRuntimeState.ActionSort</c>) -- see
    ///     <see cref="AttackRejectReason.DefenderActionStateBlocksTargeting" />.
    /// </param>
    /// <remarks>
    ///     NOT implemented: the zone124 unconditional x3-damage/forced-crit override in the final 10s of a duel
    ///     countdown (S07_MyGame02.cpp:1146-1150) -- see <c>Zone.ApplyDuelAttack</c>'s own remarks for why (map
    ///     124 duels are already refused outright before an <c>ActiveDuel</c> can ever exist, and the override's
    ///     own "remaining time" counter identity/owner was left an open question by this method's own source
    ///     contract).
    /// </remarks>
    public static AttackOutcome ResolveDuelAttack(
        CombatantSnapshot attacker,
        CombatantSnapshot defender,
        AttackForProtocol request,
        TimeSpan zoneClock,
        SkillDefinition? attackSkill,
        IRandomSource rng,
        bool attackerAndDefenderShareActiveDuel,
        bool defenderPshopOpen,
        int defenderActionSort)
    {
        if (attacker.CharacterId == defender.CharacterId)
            return AttackOutcome.Reject(AttackRejectReason.SameCharacter);
        if (attacker.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.AttackerDead);
        if (defender.IsDead)
            return AttackOutcome.Reject(AttackRejectReason.DefenderDead);
        // AttackPlayer's shared precondition block (S07_MyGame02.cpp:901-933) also gates on the defender's
        // shop/action-state before the duel-specific authorization gate below -- the same precondition
        // ResolveEnemyTribeAttack also reproduces via its own identically-named parameters.
        if (defenderPshopOpen)
            return AttackOutcome.Reject(AttackRejectReason.DefenderShopOpen);
        if (defenderActionSort is NoActionYetSort or DeathPoseSort)
            return AttackOutcome.Reject(AttackRejectReason.DefenderActionStateBlocksTargeting);
        // Duel-specific authorization gate -- duel attacks never evaluate ResolveEnemyTribeAttack's own
        // zone-wide open-PvP/same-tribe gate (S07_MyGame02.cpp:935-943 vs. the contrasting :945-958).
        if (!attackerAndDefenderShareActiveDuel)
            return AttackOutcome.Reject(AttackRejectReason.DuelNotAuthorized);

        return ResolveDamage(attacker, defender, request, zoneClock, attackSkill, rng);
    }

    /// <summary>
    ///     Everything <c>AttackPlayer</c> does identically regardless of <c>PVP_ATTACK_TYPE</c>, once its
    ///     type-specific authorization gate has already passed: protect-window/range/attack-success gating,
    ///     the hit-chance roll, base damage, charge/variance/crit/element adjustments, and the PvP-only /5
    ///     division (see class remarks). Shared verbatim by <see cref="ResolveEnemyTribeAttack" /> and
    ///     <see cref="ResolveDuelAttack" /> -- neither reorders nor duplicates any of this.
    /// </summary>
    private static AttackOutcome ResolveDamage(
        CombatantSnapshot attacker,
        CombatantSnapshot defender,
        AttackForProtocol request,
        TimeSpan zoneClock,
        SkillDefinition? attackSkill,
        IRandomSource rng)
    {
        if (attacker.ZoneEntryAtZoneClock is { } attackerZoneEntry &&
            zoneClock - attackerZoneEntry < ProtectDuration)
            return AttackOutcome.Reject(AttackRejectReason.AttackerProtected);
        if (defender.ZoneEntryAtZoneClock is { } defenderZoneEntry &&
            zoneClock - defenderZoneEntry < ProtectDuration)
            return AttackOutcome.Reject(AttackRejectReason.DefenderProtected);
        if (!CombatMath.IsInRange(attacker.PosX, attacker.PosY, attacker.PosZ, defender.PosX, defender.PosY,
                defender.PosZ, MaxAttackDistance))
            return AttackOutcome.Reject(AttackRejectReason.OutOfRange);

        var attackSuccess = attacker.Stats.AttackSuccess;
        if (attackSuccess < 1)
            return AttackOutcome.Reject(AttackRejectReason.AttackerHasNoAttackSuccess);

        // Spent the moment the attack is attempted, before the hit-chance roll, win or miss.
        var chargeConsumed = attacker.ChargeBuffPercent > 0;

        var attackBlock = defender.Stats.AttackBlock;
        if (attackBlock > 0)
        {
            var hitChance = CombatMath.ComputeHitChancePercent(attackSuccess, attackBlock);
            if (!CombatMath.RollHit(hitChance, rng))
                return AttackOutcome.Miss(chargeConsumed);
        }

        var isSkillAttack = request.AttackActionValue1 == 2;

        var attackPower = attacker.Stats.AttackPower;
        if (isSkillAttack && attackSkill != null)
        {
            var ratio = SkillCatalog.ReturnSkillValue(attackSkill, request.AttackActionValue3,
                SkillValueKind.AttackPowerRatio);
            if (ratio > 0f)
                attackPower = CombatMath.ApplySkillPowerRatio(attackPower, ratio);
        }

        var damage = attackPower - defender.Stats.DefensePower;
        if (damage < 1) damage = 1;

        if (chargeConsumed)
            damage = (int)(damage * (attacker.ChargeBuffPercent + 100) * 0.01f);

        damage = CombatMath.ApplyVariance(damage, rng);
        if (damage < MinimumDamageAgainstAvatar) damage = MinimumDamageAgainstAvatar;

        var critical = false;
        if (CanRollCritical(request, attackSkill))
        {
            var criticalChance = attacker.Stats.Critical - defender.Stats.CriticalDefence;
            if (criticalChance > 0 && CombatMath.RollCritical(criticalChance, rng))
            {
                damage *= 2;
                critical = true;
            }
        }

        damage /= MinimumDamageAgainstAvatar; // PvP-only division -- see class remarks

        var elementDamage = 0;
        if (attacker.Stats.ElementAttackPower > defender.Stats.ElementDefensePower)
            elementDamage = attacker.Stats.ElementAttackPower - defender.Stats.ElementDefensePower;
        damage += elementDamage;

        // "View" damage (S07_MyGame02.cpp:1361) is captured BEFORE the life-cap clamp (:1362-1365); "real"
        // damage (:1366) is the clamped value. On a killing/overkill blow the client still displays the full
        // hit size even though only the defender's remaining life is actually subtracted.
        var viewDamage = damage;
        if (damage > defender.Life)
            damage = defender.Life;

        return new AttackOutcome(false, AttackRejectReason.None, true, critical, damage, viewDamage, elementDamage,
            chargeConsumed);
    }

    /// <summary>Melee always rolls; a skill attack only rolls when the skill isn't 78 and its AttackType is 2 or 5.</summary>
    private static bool CanRollCritical(AttackForProtocol request, SkillDefinition? attackSkill)
    {
        if (request.AttackActionValue1 == 1)
            return true;
        if (request.AttackActionValue1 != 2)
            return false;
        if (request.AttackActionValue2 == SkillNumberExcludedFromCritical)
            return false;

        return attackSkill is { Skill.AttackType: 2 or 5 };
    }
}

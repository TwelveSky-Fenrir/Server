using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Kill-type classification for a lethal PvP blow (<c>KILL_CP_TYPE</c>, Server/ts25zone/H07_MyGame.h:51-55).
///     Fed into the kill-reward / combat-point-increment / enemy-tribe kill-drop pipeline; those pipelines' own
///     effects are separate slices -- this only fixes how the code is chosen (see
///     <see cref="OneShotKillClassifier" />).
/// </summary>
public enum KillCpType : byte
{
    /// <summary>The kill credit came from the team-stun sub-mechanic, not an HP-death blow.</summary>
    Stun = 0,

    /// <summary>An ordinary lethal blow whose killing skill index is not in the one-shot set.</summary>
    NormalHit = 1,

    /// <summary>A lethal blow whose killing skill index is a designated one-shot skill, or any reflect-damage kill.</summary>
    CriticalHit = 2
}

/// <summary>
///     Classifies a lethal PvP blow into <see cref="KillCpType" /> by set membership of the killing skill index
///     alone -- never by actual damage magnitude (<c>AtkGet1Hit</c>, Server/ts25zone/S07_MyGame02.cpp:811-879;
///     consumed at :899 and :1379-1381). A reflect-damage kill (the attacker dies from the target's returned
///     damage, S07_MyGame02.cpp:1300-1334) is always <see cref="KillCpType.CriticalHit" />, unconditionally.
/// </summary>
public static class OneShotKillClassifier
{
    /// <summary>
    ///     The live one-shot skill-index set (<c>AtkGet1Hit</c>). <b>GAP:</b> the live members are not recoverable
    ///     from the B14 behavior contract. The only concrete indices the contract names (the 112-120 range) sit
    ///     inside an <c>#ifndef LNW33</c> block that is dead in the shipped build (<c>LNW33</c> is force-defined,
    ///     DEFINE.h:21-23) and are explicitly excluded, along with an always-true bug that block also carries.
    ///     Until a follow-up finding enumerates the live members this set is intentionally empty, so every
    ///     non-reflect lethal blow classifies as <see cref="KillCpType.NormalHit" /> -- the safe default, since
    ///     the current reward pipeline treats NormalHit and CriticalHit identically anyway (see
    ///     <c>PvpKillRewardZoneCatalog</c>). Do not invent members here.
    /// </summary>
    private static readonly FrozenSet<int> OneShotSkillIndices = FrozenSet<int>.Empty;

    /// <summary>
    ///     True when <paramref name="skillIndex" /> is a designated one-shot skill (currently never -- see the gap on
    ///     <see cref="OneShotSkillIndices" />).
    /// </summary>
    public static bool IsOneShotSkill(int skillIndex)
    {
        return OneShotSkillIndices.Contains(skillIndex);
    }

    /// <summary>
    ///     Classifies a lethal enemy-tribe blow. <paramref name="isReflectKill" /> true (the killing damage was
    ///     the target's returned reflect damage) forces <see cref="KillCpType.CriticalHit" /> unconditionally;
    ///     otherwise the result is <see cref="KillCpType.CriticalHit" /> iff <paramref name="killingSkillIndex" />
    ///     is a one-shot skill, else <see cref="KillCpType.NormalHit" />. This method never returns
    ///     <see cref="KillCpType.Stun" /> -- that value is produced by the separate non-death team-stun credit
    ///     path, not by an HP-death blow.
    /// </summary>
    public static KillCpType Classify(int killingSkillIndex, bool isReflectKill)
    {
        if (isReflectKill)
            return KillCpType.CriticalHit;

        return IsOneShotSkill(killingSkillIndex)
            ? KillCpType.CriticalHit
            : KillCpType.NormalHit;
    }
}

/// <summary>
///     B14 skill-action-validation and party-buff cast-lifecycle hooks for <see cref="Zone" /> -- the thin
///     instance surface over <see cref="FormationSkillCatalog" /> (formation zone-lock + party-buff marker) and
///     <see cref="OneShotKillClassifier" /> (one-shot kill classification), resolving this zone process's own
///     war/duel-type flags from <see cref="MapId" />. Every method here is O(1) and touches only the passed-in
///     <see cref="PlayerRuntimeState" />, never shared zone state, so all are safe to call from inside the tick's
///     command-drain path. See the accompanying wiring manifest for the exact call sites in the op15/op16
///     avatar-action dispatch and the PvP kill path (which live in partials this slice does not own).
/// </summary>
public partial class Zone
{
    /// <summary>
    ///     True if this zone process is a "049-type" Regular-War/duel zone (its map is one of the fixed 11
    ///     Regular War maps). Mirrors the legacy <c>mCheckZone049TypeServer</c> startup flag, which Fenrir
    ///     collapses to <see cref="RegularWarMapCatalog" /> membership (see
    ///     <c>RegularWarActiveMapTracker</c>'s own remarks for that collapse). The "124-type" flag is resolved
    ///     inside <see cref="FormationSkillCatalog.IsFormationSkillZoneLocked" /> directly from <see cref="MapId" />.
    /// </summary>
    private bool IsWarZone049Type => RegularWarMapCatalog.TryGet(MapId, out _);

    /// <summary>
    ///     Whether a Formation-skill (76-81) action must be silently dropped in this zone because it is a
    ///     124-type or 049-type war/duel zone. The caller returns immediately (dropping the whole action,
    ///     including its movement/position update) when this is true, before any mana/marker work -- matching the
    ///     legacy handler's immediate return with no mana charge, marker change, broadcast, response, or
    ///     disconnect. A no-op (returns false) for any non-Formation skill number and for every non-war zone.
    /// </summary>
    internal bool IsFormationSkillZoneLocked(int skillNumber)
    {
        return FormationSkillCatalog.IsFormationSkillZoneLocked(skillNumber, MapId, IsWarZone049Type);
    }

    /// <summary>
    ///     Advances <paramref name="state" />'s party-buff cast-lifecycle marker for one accepted skill-cast
    ///     action: a party-buff Formation skill (76/77/79/81) with action sort 64 arms it
    ///     (<see cref="PartyBuffAction.Cast" />), action sort 65 confirms it
    ///     (<see cref="PartyBuffAction.Done" />, only from <see cref="PartyBuffAction.Cast" />). A no-op for every
    ///     other skill/sort combination. Call after the cast has passed the war-zone drop and the pre-charge
    ///     validation, from both the primary and the update-action handler.
    /// </summary>
    internal void AdvanceCasterPartyBuffMarker(PlayerRuntimeState state, int skillNumber, int actionSort)
    {
        state.PartyBuffAct = FormationSkillCatalog.NextPartyBuffMarker(state.PartyBuffAct, skillNumber, actionSort);
    }

    /// <summary>
    ///     Resets <paramref name="state" />'s party-buff cast-lifecycle marker to
    ///     <see cref="PartyBuffAction.None" />. Call on any in-place zone/state-change event that reuses the same
    ///     <see cref="PlayerRuntimeState" /> object (teleport, zone move, death-type transition); a fresh state
    ///     per zone entry already defaults to <see cref="PartyBuffAction.None" /> and needs no call.
    /// </summary>
    internal static void ResetPartyBuffMarker(PlayerRuntimeState state)
    {
        state.PartyBuffAct = PartyBuffAction.None;
    }

    /// <summary>
    ///     Classifies a lethal PvP blow into <see cref="KillCpType" /> for the kill-reward/kill-drop pipeline.
    ///     <paramref name="isReflectKill" /> true (the killer died from the target's returned reflect damage)
    ///     forces <see cref="KillCpType.CriticalHit" />; otherwise it is critical iff
    ///     <paramref name="killingSkillIndex" /> (the attack's recorded skill id, <c>AttackActionValue2</c> when
    ///     <c>AttackActionValue1</c> == 2) is a one-shot skill, else normal. Delegates to
    ///     <see cref="OneShotKillClassifier.Classify" />.
    /// </summary>
    internal KillCpType ClassifyPvpKillType(int killingSkillIndex, bool isReflectKill)
    {
        return OneShotKillClassifier.Classify(killingSkillIndex, isReflectKill);
    }
}

namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     Finite-state machine for <see cref="MonsterEntity" />, modeled on legacy <c>mDATA.mAction.aSort</c>
///     (<c>Server/ts25zone/S07_MyGame05.cpp</c>). Values match the legacy aSort codes exactly (not renumbered)
///     so <c>(int)state</c> can be written straight into
///     <see cref="Fenrir.Network.Serialization.Packets.Shared.ActionInfo.Sort" />
///     without a translation table.
/// </summary>
public enum MonsterAiState : byte
{
    /// <summary>A001: spawn/wait, counts to <c>mFrameInfo[0]</c> ticks then moves to <see cref="Decision" />.</summary>
    Spawning = 0,

    /// <summary>A002: detect nearby enemy, wander, or return toward <see cref="MonsterEntity.HomeX" />.</summary>
    Decision = 1,

    /// <summary>A004: walking back to spawn at <c>mWalkSpeed</c>.</summary>
    Patrol = 3,

    /// <summary>
    ///     A005: chasing locked target at <c>mRunSpeed</c>; gives up once the target's live position drifts
    ///     beyond the monster's own detection radius (<c>RadiusInfo2</c>) from the monster's own live
    ///     position -- not a distance-from-home leash (<see cref="MonsterAiSystem.RunChase" />'s remarks).
    /// </summary>
    Chase = 4,

    /// <summary>A006: attack windup, counts to <c>mFrameInfo[2]</c> ticks.</summary>
    AttackWindup = 5,

    /// <summary>
    ///     A008: <c>Zone175</c>-type boss (<c>Template.SpecialType</c> 40-44) ranged-attack windup, counts to
    ///     <c>mFrameInfo[3]</c> ticks -- the boss counterpart <see cref="MonsterAiSystem.RunChase" /> enters
    ///     instead of <see cref="AttackWindup" /> once these monsters are in range. Legacy's own A008 body has
    ///     no embedded damage-resolution call (unlike A006's <see cref="AttackWindup" />, whose windup fires
    ///     <see cref="Combat.MonsterCombatResolver.ResolveMvpAttack" /> mid-count) -- ported as a pure timed
    ///     state for the same reason: the actual bullet/projectile hit-test this state telegraphs is not
    ///     modeled.
    /// </summary>
    RangedAttackWindup = 7,

    /// <summary>
    ///     A009: hit-stagger, counts to <c>mFrameInfo[1]</c> ticks then returns to <see cref="Decision" />.
    ///     Legacy enters this (<c>S07_MyGame02.cpp</c>'s <c>ProcessAttack03</c>) when a single hit deals &gt;=10%
    ///     of the monster's max life, <c>DamageType != 1</c> (stationary/structure monsters like the tribe-symbol
    ///     stones never flinch), 50% roll, and it isn't already staggered. Wired via
    ///     <c>Zone.TryApplyPvmFlinch</c> (called from <c>Zone.ApplyPvmAttack</c> on a landed, non-killing hit) --
    ///     see that method's own remarks for the exact gate order and citation.
    /// </summary>
    Flinch = 8,

    /// <summary>
    ///     A020: forced return to <see cref="MonsterEntity.HomeX" />/<see cref="MonsterEntity.HomeY" />/
    ///     <see cref="MonsterEntity.HomeZ" /> after losing/giving-up a chase. A fixed, distance-independent
    ///     animation-frame delay counting to <c>mFrameInfo[5]</c> (<see cref="MonsterRowDto.FrameInfo6" />)
    ///     ticks, then an instantaneous teleport to the spawn point -- never a walk
    ///     (<c>Server/ts25zone/S07_MyGame05.cpp:1658-1673</c>, no movement/pathing call anywhere in that
    ///     handler).
    /// </summary>
    ReturnToSpawn = 19,

    /// <summary>
    ///     A013 "Attack Stone" (<c>aSort</c> 12): the real legacy state every monster's death enters (windup to
    ///     <c>mFrameInfo[4]</c> ticks, then the slot frees for respawn) -- not a Fenrir-only sentinel despite the
    ///     name. For the 5 "Holy Stone" tribe/monster-symbol guardians (<c>Template.SpecialType</c> 11/12/13/28/14)
    ///     specifically, the legacy windup additionally reports the winning tribe
    ///     (<see cref="MonsterSpawnScheduler.ProcessDeath" /> -&gt;
    ///     <see cref="World.ZoneWar.ZoneEventBroadcaster.AnnounceSymbolResolved" />) before the slot frees up.
    ///     Fenrir's simplified instant-removal-on-death pipeline (<see cref="Zone.TryDamageMonster" />) collapses
    ///     the windup and the removal into one broadcast instead of counting the frames out first -- do not
    ///     reuse this same numeric value for an unrelated new state; it is already exactly what legacy's own
    ///     aSort 12 means.
    /// </summary>
    Dead = 12
}

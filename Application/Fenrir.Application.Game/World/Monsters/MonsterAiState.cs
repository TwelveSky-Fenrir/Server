namespace Fenrir.Application.Game.World.Monsters;

/// <summary>
///     A simplified finite-state machine for <see cref="MonsterEntity" />, modeled directly on the legacy
///     <c>mDATA.mAction.aSort</c> values <c>MONSTER_OBJECT::Update</c> switches on (report
///     ServerDocs/30_Fenrir_ServerLogic/05_game_mechanics.md §3, verified against
///     <c>Server/ts25zone/S07_MyGame05.cpp</c>). The numeric values are kept IDENTICAL to the legacy
///     <c>aSort</c> codes on purpose (not renumbered 0..N) so a future wire-replication pass can write
///     <c>(int)state</c> straight into <see cref="Fenrir.Contracts.Packets.Shared.ActionInfo.Sort" /> without a
///     translation table, exactly like <see cref="World.Zone.ApplyDeath" /> already reuses the shared "12 =
///     death pose" convention for players.
/// </summary>
/// <remarks>
///     Only the states this pass's simplified AI actually uses are modeled -- report 05's full table also
///     lists 7/8 (A008/A009, boss special-attack/hit-reaction animations) and a symbol/guard-stone-specific
///     reading of 12 (A013) that this pass does not implement (no boss/guard/tribe-symbol content in this
///     batch -- see this task's StructuredOutput openIssues). <see cref="Dead" /> is Fenrir-only bookkeeping
///     (not a distinct legacy aSort): a dead <see cref="MonsterEntity" /> is removed from the zone's live
///     pool entirely rather than lingering in a "corpse" state, so this value only ever appears transiently
///     between <see cref="World.Zone.TryDamageMonster" /> flagging the kill and the next tick's drain
///     actually removing the entity.
/// </remarks>
public enum MonsterAiState : byte
{
    /// <summary>A001: spawn/wait -- counts up to <c>mFrameInfo[0]</c> legacy ticks, then moves to <see cref="Decision" />.</summary>
    Spawning = 0,

    /// <summary>A002: decision -- detect a nearby enemy (aggro), wander, or return toward <see cref="MonsterEntity.HomeX" />.</summary>
    Decision = 1,

    /// <summary>A004: patrol -- walking back toward the home/spawn point at <c>mWalkSpeed</c> when idle and displaced.</summary>
    Patrol = 3,

    /// <summary>A005: pursuit -- chasing the locked target at <c>mRunSpeed</c>, leashed to the spawn region.</summary>
    Chase = 4,

    /// <summary>A006: attack windup/animation -- counts up to <c>mFrameInfo[2]</c> legacy ticks before the next decision.</summary>
    AttackWindup = 5,

    /// <summary>A020: forced return to <see cref="MonsterEntity.HomeX" /> after losing/leashing off a target.</summary>
    ReturnToSpawn = 19,

    /// <summary>Fenrir-only: reuses the shared "12 = death" ActionInfo convention -- see this type's remarks.</summary>
    Dead = 12
}

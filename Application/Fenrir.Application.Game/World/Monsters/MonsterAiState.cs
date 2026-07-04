namespace Fenrir.Application.Game.World.Monsters;

/// <summary>
///     Finite-state machine for <see cref="MonsterEntity" />, modeled on legacy <c>mDATA.mAction.aSort</c>
///     (<c>Server/ts25zone/S07_MyGame05.cpp</c>). Values match the legacy aSort codes exactly (not renumbered)
///     so <c>(int)state</c> can be written straight into <see cref="Fenrir.Contracts.Packets.Shared.ActionInfo.Sort" />
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

    /// <summary>A005: chasing locked target at <c>mRunSpeed</c>, leashed to the spawn region.</summary>
    Chase = 4,

    /// <summary>A006: attack windup, counts to <c>mFrameInfo[2]</c> ticks.</summary>
    AttackWindup = 5,

    /// <summary>A020: forced return to <see cref="MonsterEntity.HomeX" /> after losing/leashing off a target.</summary>
    ReturnToSpawn = 19,

    /// <summary>Fenrir-only bookkeeping (not a legacy aSort); transient between kill and next tick's removal.</summary>
    Dead = 12
}

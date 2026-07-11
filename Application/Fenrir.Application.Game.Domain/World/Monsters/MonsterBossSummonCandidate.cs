namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     One boss-candidate record in a zone's <c>SummonBossMonster</c> pool -- the Fenrir shape of a single row
///     from a legacy per-zone <c>Z0NN_SUMMONBOSSMONSTER.WREGION</c> boss-region file
///     (<c>Server/Header/Protocol/STRUCT.h:1327-1335</c>): a monster id, a fixed spawn coordinate (three whole
///     numbers), a per-record radius, and an auxiliary "number" field.
/// </summary>
/// <remarks>
///     <see cref="Radius" /> and <see cref="Number" /> are carried for fidelity but are deliberately NOT
///     consumed by the Reload spawn path. Bosses spawn at <see cref="X" />/<see cref="Y" />/<see cref="Z" />
///     copied verbatim, with NO radius dispersion and NO ground-altitude re-clamp -- the stored-location path
///     (<c>Server/ts25zone/S10_MySummon.cpp:753-762</c>), unlike the calculated-location path
///     <see cref="MonsterSpawnScheduler.Spawn" /> uses for ordinary region monsters (which DOES scatter within
///     the radius and ground-snap). <see cref="MonsterBossSpawnMachine" /> is the state machine that consumes
///     these; <see cref="MonsterBossSummonCatalog" /> is the injected pool that supplies them.
/// </remarks>
public readonly record struct MonsterBossSummonCandidate(
    int MonsterId,
    float X,
    float Y,
    float Z,
    float Radius,
    int Number);

/// <summary>
///     The three live states of the legacy <c>SummonBossMonster</c> machine (its own state codes;
///     <c>Server/ts25zone/S10_MySummon.cpp:1081-1214</c>). Codes 0 ("summon on startup") and 4 ("check and
///     per-entry respawn") exist in the source but are entirely commented out and unreachable -- the live cycle
///     is strictly <see cref="Reload" /> -&gt; <see cref="Check" /> -&gt; <see cref="Wait" /> -&gt; back to
///     <see cref="Reload" />.
/// </summary>
public enum MonsterBossSummonState : byte
{
    /// <summary>
    ///     Legacy state 1: pick one random candidate, resolve its monster id, fill up to 5 free boss slots in the
    ///     20-slot window with that single candidate, then advance to <see cref="Check" /> (or, on an
    ///     unresolvable id, return WITHOUT advancing and re-roll next invocation). The subsystem init sets the
    ///     machine to this state exactly once (<c>S10_MySummon.cpp:61-67</c>).
    /// </summary>
    Reload = 1,

    /// <summary>
    ///     Legacy state 2: its historical liveness-check loop is commented out, so the only live effect is to
    ///     advance to <see cref="Wait" /> and overwrite the timer anchor -- and THIS anchor (written one
    ///     invocation-interval after Reload's spawn) is the one and only anchor <see cref="Wait" /> ever reads
    ///     (<c>S10_MySummon.cpp:1115-1129</c>).
    /// </summary>
    Check = 2,

    /// <summary>
    ///     Legacy state 3: hold until 3 hours have elapsed from the <see cref="Check" /> anchor, then reset to
    ///     <see cref="Reload" /> -- no spawn happens in this state (<c>S10_MySummon.cpp:1130-1136</c>).
    /// </summary>
    Wait = 3
}

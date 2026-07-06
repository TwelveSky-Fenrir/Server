using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>Per-zone bookkeeping <see cref="TribeSymbolSpawner" /> needs, tick-thread-owned.</summary>
internal sealed class SymbolZoneState
{
    public bool BootPassDone;
    public int TicksSinceFullScan;
}

/// <summary>
///     <c>MySummon::SummonTribeSymbol()</c> (<c>Server/ts25zone/S10_MySummon.cpp:1859-2101</c>): places/despawns
///     the 4 per-tribe "Holy Stone" symbols plus the neutral monster-guarded symbol. Unlike
///     <see cref="TribeGuardSpawner" />, no map allowlist gates the whole call -- every zone evaluates all 5
///     symbols on every pass, only actually spawning/despawning the ones whose designated map equals its own.
///     <para>
///         The 5 templates are identified purely by <see cref="MonsterRowDto.SpecialType" /> (11/12/13/28/14),
///         reusing the exact convention <see cref="Monsters.MonsterSpawnScheduler" />'s own
///         <c>TribeSymbolIndexOf</c> already established from the same legacy source
///         (<c>S07_MyGame05.cpp:1568-1587</c>: monsters 601-605 are the only ones that ever carry these
///         values) -- kept in sync manually since that mapping is <c>private</c> there.
///     </para>
///     <para>
///         <b>Documented schema gap:</b> for symbol indices 0-3 (one tribe's own slot), <see cref="WorldStateService" />
///         only ever records whether that tribe still owns its own symbol (<c>TribeRvrState.HasSymbol</c>, a
///         bool) -- never which rival tribe captured it. A captured symbol's new designated map can therefore
///         never be resolved from current state, so this class can only ever place a tribe's own symbol on
///         its own home territory, never relocate a lost one to its captor's territory. Closing this requires
///         a <see cref="WorldStateService" /> schema change (recording a captor tribe id, not just a bool) --
///         out of this cluster's own scope; see its final report.
///     </para>
///     <para>
///         Not modeled: the neutral-symbol attack-cooldown reset (Side effects §8 of the contract) -- it would
///         need a new field on <see cref="Monsters.MonsterEntity" /> plus a Combat-layer consumer, both outside
///         this class's own World/ZoneWar scope.
///     </para>
/// </summary>
public sealed class TribeSymbolSpawner(
    WorldDataCache worldData,
    TribeSymbolCatalog catalog,
    WorldStateService? worldState = null) : ISimulationSystem
{
    /// <summary><c>(mTickCount % 20) == 0</c> -- same periodic cadence as <see cref="TribeGuardSpawner" />.</summary>
    private const int FullEvaluationCadenceLegacyTicks = 20;

    /// <summary>
    ///     No legacy leash figure is given for a stationary symbol guardian -- same stand-in as
    ///     <see cref="TribeGuardSpawner" />.
    /// </summary>
    private const float SymbolLeashRadius = 15f;

    private const int SymbolPoolServerIndexBase = 1_002_000;
    private const int SymbolPoolSize = 100;
    private const byte NeutralSymbolIndex = 4;

    /// <summary>SpecialType per symbol index 0-4, mirroring <c>MonsterSpawnScheduler.TribeSymbolIndexOf</c>'s reverse.</summary>
    private static readonly byte[] SpecialTypeBySymbolIndex = [11, 12, 13, 28, 14];

    private readonly ConcurrentDictionary<short, SymbolZoneState> _stateByZone = new();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var state = _stateByZone.GetOrAdd(zone.MapId, static _ => new SymbolZoneState());

        if (!state.BootPassDone)
        {
            state.BootPassDone = true;
            EvaluateNow(zone);
        }

        state.TicksSinceFullScan += legacyTicksElapsed;
        if (state.TicksSinceFullScan < FullEvaluationCadenceLegacyTicks)
            return;

        state.TicksSinceFullScan = 0;
        EvaluateNow(zone);
    }

    /// <summary>
    ///     Runs a full 5-symbol pass immediately -- the forced re-evaluation the HSB-start broadcast relay and
    ///     the tribe-symbol-battle-open transition trigger (<c>S07_MyGame08.cpp:223-231</c>), wired from
    ///     <see cref="ZoneEventBroadcaster" />. Must only be called from <paramref name="zone" />'s own tick
    ///     thread (spawning is a single-writer operation), matching every other <see cref="ISimulationSystem" />.
    /// </summary>
    public void EvaluateNow(Zone zone)
    {
        for (byte symbolIndex = 0; symbolIndex < SpecialTypeBySymbolIndex.Length; symbolIndex++)
            EvaluateSymbol(zone, symbolIndex, SpecialTypeBySymbolIndex[symbolIndex]);
    }

    private void EvaluateSymbol(Zone zone, byte symbolIndex, byte specialType)
    {
        if (!TryFindTemplateBySpecialType(specialType, out var template))
            return; // no monster template carries this SpecialType -- skip silently

        var ownerState = ResolveOwnerState(symbolIndex);
        if (ownerState is null)
            return; // unresolvable this pass (captured tribe symbol -- see class remarks)

        if (!catalog.TryGetPlacement(symbolIndex, ownerState.Value, out var placement))
            return; // no configured placement for this state -- skip

        int? existingServerIndex = null;
        foreach (var monster in zone.MonstersSnapshot)
        {
            if (monster.ServerIndex < SymbolPoolServerIndexBase ||
                monster.ServerIndex >= SymbolPoolServerIndexBase + SymbolPoolSize)
                continue;

            if (monster.Template.MonsterId != template.MonsterId)
                continue;

            existingServerIndex = monster.ServerIndex;
            break;
        }

        if (existingServerIndex is { } liveIndex)
        {
            if (placement.MapId != zone.MapId)
                zone.DespawnMonsterSilently(liveIndex); // ownership moved elsewhere -- despawn locally only

            return; // either despawned above, or already correctly present -- nothing else to do this pass
        }

        if (placement.MapId != zone.MapId)
            return; // not found anywhere here, and this zone isn't responsible for it this pass

        if (!TryFindFreeSlot(zone, out var freeServerIndex))
            return; // shared pool exhausted -- skipped, retried next evaluation, no backoff

        // Underdog comeback bonus: only the symbol's OWN tribe spawning its OWN symbol while that tribe is
        // currently this zone's smallest by connected population (S07_MyGame01.cpp:3139-3142, ReturnSmallTribe).
        var doubleHp = symbolIndex < NeutralSymbolIndex && ownerState.Value == symbolIndex &&
                       IsSmallestTribe(zone, symbolIndex);
        var spawnTemplate = doubleHp ? template with { Life = template.Life * 2 } : template;

        var entity = MonsterEntity.Create(freeServerIndex, zone.NextMonsterUniqueNumber(), spawnTemplate,
            freeServerIndex, placement.X, placement.Y, placement.Z, SymbolLeashRadius);
        zone.SpawnMonster(entity);
    }

    /// <summary>
    ///     Symbol 0-3: resolvable only while that tribe still owns its own slot (see class remarks for why a
    ///     lost symbol's new location cannot be determined). Symbol 4 (neutral): <c>World.MonsterSymbol</c>
    ///     maps directly onto its 5 states (null -&gt; <see cref="TribeSymbolCatalog.NeutralUnclaimedOwnerState" />,
    ///     0-3 -&gt; the capturing tribe).
    /// </summary>
    private byte? ResolveOwnerState(byte symbolIndex)
    {
        if (worldState is null)
            return null;

        if (symbolIndex == NeutralSymbolIndex)
            return worldState.World.MonsterSymbol ?? TribeSymbolCatalog.NeutralUnclaimedOwnerState;

        return worldState.GetTribe(symbolIndex).HasSymbol ? symbolIndex : null;
    }

    private static bool IsSmallestTribe(Zone zone, byte tribeId)
    {
        var counts = new int[WorldStateService.TribeCount];
        foreach (var player in zone.Players)
            if (player.Tribe < WorldStateService.TribeCount)
                counts[player.Tribe]++;

        byte smallest = 0;
        for (byte i = 1; i < WorldStateService.TribeCount; i++)
            if (counts[i] < counts[smallest])
                smallest = i;

        return smallest == tribeId;
    }

    private static bool TryFindFreeSlot(Zone zone, out int serverIndex)
    {
        for (var i = 0; i < SymbolPoolSize; i++)
        {
            var candidate = SymbolPoolServerIndexBase + i;
            if (!zone.TryGetMonster(candidate, out _))
            {
                serverIndex = candidate;
                return true;
            }
        }

        serverIndex = 0;
        return false;
    }

    private bool TryFindTemplateBySpecialType(byte specialType, [NotNullWhen(true)] out MonsterRowDto? template)
    {
        foreach (var definition in worldData.MonstersById.Values)
            if (definition.Monster.SpecialType == specialType)
            {
                template = definition.Monster;
                return true;
            }

        template = null;
        return false;
    }
}

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

internal sealed class SymbolZoneState
{
    public bool BootPassDone;
    public int TicksSinceFullScan;
}

public sealed class TribeSymbolSpawner(
    WorldDataCache worldData,
    TribeSymbolCatalog catalog,
    WorldStateService? worldState = null) : ISimulationSystem
{

        private const int FullEvaluationCadenceLegacyTicks = 20;

        private const float SymbolLeashRadius = 15f;

    private const int SymbolPoolServerIndexBase = 1_002_000;
    private const int SymbolPoolSize = 100;
    private const byte NeutralSymbolIndex = 4;

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

        public void EvaluateNow(Zone zone)
    {
        for (byte symbolIndex = 0; symbolIndex < SpecialTypeBySymbolIndex.Length; symbolIndex++)
            EvaluateSymbol(zone, symbolIndex, SpecialTypeBySymbolIndex[symbolIndex]);
    }

    private void EvaluateSymbol(Zone zone, byte symbolIndex, byte specialType)
    {
        if (!TryFindTemplateBySpecialType(specialType, out var template))
            return;

        var ownerState = ResolveOwnerState(symbolIndex);
        if (ownerState is null)
            return;

        if (!catalog.TryGetPlacement(symbolIndex, ownerState.Value, out var placement))
            return;

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
                zone.DespawnMonsterSilently(liveIndex);

            return;
        }

        if (placement.MapId != zone.MapId)
            return;

        if (!TryFindFreeSlot(zone, out var freeServerIndex))
            return;

        var doubleHp = symbolIndex < NeutralSymbolIndex && ownerState.Value == symbolIndex &&
                       IsSmallestTribe(zone, symbolIndex);
        var spawnTemplate = doubleHp ? template with { Life = template.Life * 2 } : template;

        var entity = MonsterEntity.Create(freeServerIndex, zone.NextMonsterUniqueNumber(), spawnTemplate,
            freeServerIndex, placement.X, placement.Y, placement.Z, SymbolLeashRadius);
        zone.SpawnMonster(entity);
    }

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

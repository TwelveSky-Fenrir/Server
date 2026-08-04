using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World.WorldState;

public static class WorldStateProjection
{
    public static WorldInfo Apply(WorldInfo template, WorldStateService worldState)
    {
        var world = worldState.World;
        var tribes = worldState.GetAllTribes();

        var tribeSymbol = new int[WorldStateService.TribeCount];
        var tribePoint = new int[WorldStateService.TribeCount];

        var tribeMasterCallAbility = new int[WorldStateService.TribeCount];
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            tribeMasterCallAbility[tribeId] = worldState.GetTribeFormationAbility(tribeId);

        foreach (var tribe in tribes)
        {
            if (tribe.TribeId >= WorldStateService.TribeCount)
                continue;

            tribeSymbol[tribe.TribeId] = worldState.GetTribeSymbolOwner(tribe.TribeId);
            tribePoint[tribe.TribeId] = tribe.Points;
        }

        return template with
        {
            Zone038WinTribe = world.Zone038WinTribe ?? 0,
            Zone038WinTribeTime = world.Zone038WinTribeTime ?? 0,
            TribeSymbolBattle = world.TribeSymbolBattle ? 1 : 0,
            TribeSymbol = tribeSymbol,
            MonsterSymbol = world.MonsterSymbol ?? 0,
            MonsterSymbolEndTime = world.MonsterSymbolEndTime ?? 0,
            TribePoint = tribePoint,
            TribeMasterCallAbility = tribeMasterCallAbility
        };
    }
}

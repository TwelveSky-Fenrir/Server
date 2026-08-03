using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int ValleyWarBossWinRandomAnimalSlot = 5;

    private static readonly int[] ValleyWarBossWinRewardItemIds = [1072, 1103, 1449, 1422, 1145, 0, 2249, 602];

    private void HandleGrantValleyWarRewardDrop(int characterId)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        if (state.IsMovingZone || state.IsDead)
            return;

        var animalPool = Zone200GateBreachBossCatalog.BattleWinBonusRandomAnimalPoolIds;

        for (var slot = 0; slot < ValleyWarBossWinRewardItemIds.Length; slot++)
        {
            var itemId = slot == ValleyWarBossWinRandomAnimalSlot
                ? animalPool[_random.NextInt32(animalPool.Length)]
                : ValleyWarBossWinRewardItemIds[slot];

            SpawnGroundItem(itemId, 1, state.PosX, state.PosY, state.PosZ, state.Name, "", 0);
        }
    }
}

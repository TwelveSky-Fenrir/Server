namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{

        private static readonly int[] ValleyWarBossWinRewardItemIds = [1072, 1103, 1449, 1422, 1145, 2249, 602];

        private void HandleGrantValleyWarRewardDrop(int characterId)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        if (state.IsMovingZone || state.IsDead)
            return;

        foreach (var itemId in ValleyWarBossWinRewardItemIds)
            SpawnGroundItem(itemId, 1, state.PosX, state.PosY, state.PosZ, state.Name, "", 0);
    }
}

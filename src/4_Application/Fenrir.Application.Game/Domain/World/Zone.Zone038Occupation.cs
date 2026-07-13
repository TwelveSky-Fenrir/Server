namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private void HandleZone038OccupationCredit(int characterId, byte winningTribe)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        if (state.Tribe != winningTribe || state.IsDead || state.IsMovingZone)
            return;

        CreditWaterfallOccupationQuest(state);
    }
}

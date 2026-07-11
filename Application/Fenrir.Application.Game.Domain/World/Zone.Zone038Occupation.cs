using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{

        private void HandleZone038OccupationCredit(int characterId, byte winningTribe)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        if (state.Tribe != winningTribe || state.IsDead || state.IsMovingZone)
            return;

        if (state.QuestActiveFlag == 1 && state.QuestSort == 8 && state.QuestTargetPhase == MapId &&
            state.QuestKillCounter < 1)
        {
            state.QuestKillCounter++;
            state.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
            state.Session.Send(new QuestProgressResponse
            {
                Sort = QuestWaterfallWarConclusionCreditSort, Page = 0, Index = 0, XPost = 0, YPost = 0
            });
        }
    }
}

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private readonly List<int> _zone38TribeEffectNeighborScratch = [];

    private void ApplyZone38TribeEffects(ZoneWar.Zone38TribeEffectSnapshot snapshot)
    {
        var previous = _zone38TribeEffects;
        _zone38TribeEffects = snapshot;

        foreach (var state in _players.Values)
        {
            var nextEffect = snapshot.GetEffect(state.Tribe);
            if (state.Zone38TribeEffect == nextEffect && previous.GetEffect(state.Tribe) == nextEffect)
                continue;

            state.Zone38TribeEffect = nextEffect;
            RecomputeAndPublish(state);

            if (state.IsMovingZone)
                continue;

            SendAvatarAction(state.Session, state);
            _zone38TribeEffectNeighborScratch.Clear();
            _grid.NeighborsExcludingSelf(_zone38TribeEffectNeighborScratch, state.CurrentCell, state.CharacterId,
                state.PosX, state.PosY, state.PosZ);
            BroadcastAvatarAction(_zone38TribeEffectNeighborScratch, state);
        }
    }
}

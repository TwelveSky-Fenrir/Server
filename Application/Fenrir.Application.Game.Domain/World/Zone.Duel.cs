using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     The per-participant half of ending an active duel -- called by <see cref="Simulation.DuelMaintenanceSystem" />
///     once it has resolved which end condition fired; the registry-side half (removing both participants'
///     <see cref="Social.Duel.DuelRegistry" /> entries) is that system's own responsibility via
///     <see cref="Social.Duel.DuelRegistry.TryEndActiveDuel" />.
/// </summary>
public sealed partial class Zone
{
    /// <summary>
    ///     RESET_DUEL (Server/ts25zone/S07_MyGame04.cpp:606-611): unconditionally lifts <paramref name="state" />'s
    ///     potion restriction, sends ZC_DUEL_END_RECV with the resolved <paramref name="reason" />, and
    ///     refreshes this avatar's state to nearby observers only -- unlike <see cref="GrantReviveEligibility" />,
    ///     this never self-echoes to <paramref name="state" />'s own session, since ZC_DUEL_END_RECV is already
    ///     that participant's own "this happened to you" signal.
    /// </summary>
    public void EndActiveDuel(PlayerRuntimeState state, DuelEndReason reason)
    {
        state.CanUseConsumables = true;
        state.Session.Send(new DuelEndResponse { Result = (int)reason });

        var neighbors = _grid.Neighbors(state.CurrentCell).Where(id => id != state.CharacterId).ToArray();
        BroadcastAvatarAction(neighbors, state);
    }
}

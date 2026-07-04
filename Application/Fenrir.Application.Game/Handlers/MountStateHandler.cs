using Fenrir.Application.Game.Mounts;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_ANIMAL_STATE_SEND (op87). Only Sort 1-4 (Select/Deselect/Mount/Dismount) are wired -- see
///     <see cref="MountStateResolver" />'s remarks for why Sort 5+ (Delete Mount, attribute training, tier
///     upgrade) is an out-of-scope disconnect rather than the legacy's own varied per-case behavior.
/// </summary>
public sealed class MountStateHandler : IInlinePacketHandler<MountStateRequest>
{
    public void Handle(in MountStateRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var context = new MountStateResolver.Context(state.AnimalIndex, state.AnimalTime, state.ActionSort,
            state.MountGarage);
        var result = MountStateResolver.Resolve(packet.Sort, packet.Value, in context);

        switch (result.Kind)
        {
            case MountStateResolver.ResultKind.NoReply:
                return;

            case MountStateResolver.ResultKind.Disconnect:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;

            case MountStateResolver.ResultKind.Select:
            case MountStateResolver.ResultKind.Deselect:
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = packet.Value });
                zone.PostMountCommand(new MountZoneCommand(characterId, result.NewAnimalIndex));
                return;

            case MountStateResolver.ResultKind.Mount:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = packet.Value });
                zone.PostMountCommand(new MountZoneCommand(characterId, result.NewAnimalIndex,
                    result.NewAnimalNumber, 0, maxLife, maxMana,
                    Broadcast: MountBroadcastKind.Mount));
                return;
            }

            case MountStateResolver.ResultKind.Dismount:
            {
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = 0 });
                zone.PostMountCommand(new MountZoneCommand(characterId, result.NewAnimalIndex,
                    0, 0, maxLife, maxMana,
                    Broadcast: MountBroadcastKind.Dismount));
                return;
            }
        }
    }
}

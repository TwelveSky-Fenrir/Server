using Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_ANIMAL_STATE_SEND (op87). Only Sort 1-4 (Select/Deselect/Mount/Dismount) are wired -- see
///     <see cref="Mounts.MountStateResolver" />'s remarks for why Sort 5+ (Delete Mount, attribute training, tier
///     upgrade) is an out-of-scope disconnect rather than the legacy's own varied per-case behavior.
/// </summary>
public sealed class MountStateHandler(IMountStateService service) : IInlinePacketHandler<MountStateRequest>
{
    public void Handle(in MountStateRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = service.Apply(zone, state, characterId, packet.Sort, packet.Value);

        switch (result.Outcome)
        {
            case MountStateOutcome.NoReply:
                return;

            case MountStateOutcome.Disconnect:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;

            case MountStateOutcome.Select:
            case MountStateOutcome.Deselect:
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = packet.Value });
                return;

            case MountStateOutcome.Mount:
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = packet.Value });
                return;

            case MountStateOutcome.Dismount:
                session.Send(new MountStateResponse { Sort = packet.Sort, Value = 0 });
                return;
        }
    }
}

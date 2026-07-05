using Fenrir.Application.Game.Mounts;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_ANIMAL_ABSORB_SEND (op113). No dedicated reply -- state changes broadcast via AVATAR_CHANGE_INFO_1
///     (AOI) + AVATAR_CHANGE_INFO_2 (self) instead, mirrored onto the tick through <see cref="MountZoneCommand" />.
/// </summary>
public sealed class MountAbsorbHandler : IInlinePacketHandler<MountAbsorbRequest>
{
    public void Handle(in MountAbsorbRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        switch (packet.Sort)
        {
            case 1:
                if (state.AnimalIndex < MountStateResolver.SlotCount ||
                    state.AnimalIndex > MountStateResolver.MountedMax || state.AnimalAbsorbTime < 1)
                {
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                }

                zone.PostMountCommand(new MountZoneCommand(characterId, AnimalAbsorbState: 1,
                    Broadcast: MountBroadcastKind.AbsorbToggle));
                return;

            case 2:
                var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
                var maxMana = state.Stats?.MaxMana ?? state.MaxMana;
                zone.PostMountCommand(new MountZoneCommand(characterId, AnimalAbsorbState: 0, Life: maxLife,
                    Mana: maxMana, Broadcast: MountBroadcastKind.AbsorbToggle));
                return;

            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }
}

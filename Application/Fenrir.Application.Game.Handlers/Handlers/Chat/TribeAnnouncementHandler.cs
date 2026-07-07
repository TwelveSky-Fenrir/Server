using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

/// <summary>
///     CZ_TRIBE_NOTICE_SEND (opcode 80). Allowed for any non-zero tribe role
///     (<see cref="PlayerRuntimeState.TribeRole" /> 1 = master, 2 = sub-master, or 3 = elected
///     tribe-council member seated via the tribe-vote mechanism); a regular member (role 0) is silently
///     ignored, not disconnected. Strict tribe match only, no alliance.
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:11488-11520 (empty-content disconnect, role gate,
///     broadcast/relay); Server/Header/function.h:92-114 (ReturnTribeRole three-tier resolution).
/// </summary>
public sealed class TribeAnnouncementHandler(ITribeAnnouncementService tribeAnnouncementService)
    : IInlinePacketHandler<TribeAnnouncementRequest>
{
    public void Handle(in TribeAnnouncementRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (string.IsNullOrEmpty(packet.Content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        tribeAnnouncementService.TrySendAnnouncement(sender, packet.Content);
    }
}

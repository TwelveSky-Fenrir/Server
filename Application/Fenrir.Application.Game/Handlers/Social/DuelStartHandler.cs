using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_DUEL_START_SEND (opcode 46) -- callable by either accepted side. Scope cut: no
///     ZC_AVATAR_CHANGE_INFO_1 broadcast, no countdown auto-end tick.
/// </summary>
public sealed class DuelStartHandler(IDuelService duelService) : IInlinePacketHandler<DuelStartRequest>
{
    public void Handle(in DuelStartRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var callerId = zoneSession.CharacterId!.Value;

        duelService.Start(callerId);
    }
}

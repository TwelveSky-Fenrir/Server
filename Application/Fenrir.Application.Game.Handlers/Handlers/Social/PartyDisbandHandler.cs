using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_PARTY_BREAK_SEND (opcode 71) -- leader-only, unconditional full disband. USE_PARTY_V3 is off in
///     this build, so <c>Sort</c> is always 1 and <c>AvatarName</c> always blank.
/// </summary>
public sealed class PartyDisbandHandler(ZoneRegistry zones, IPartyDisbandService partyDisbandService)
    : IInlinePacketHandler<PartyDisbandRequest>
{
    public void Handle(in PartyDisbandRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var leaderId = zoneSession.CharacterId!.Value;

        var result = partyDisbandService.Disband(leaderId);
        if (result.Members.Count == 0)
            return;

        var notice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
        foreach (var memberId in result.Members)
            if (zones.TryGetPlayer(memberId, out var member))
                member.Session.Send(notice);
    }
}

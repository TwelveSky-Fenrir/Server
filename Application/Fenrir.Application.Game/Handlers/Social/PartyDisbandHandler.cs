using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

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

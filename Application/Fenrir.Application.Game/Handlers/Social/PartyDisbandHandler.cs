using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_PARTY_BREAK_SEND (opcode 71) -- leader-only, unconditional full disband. USE_PARTY_V3 is off in this build,
///     so <c>Sort</c> is always 1 and <c>AvatarName</c> is always blank (verified, contracts/05_social.md).
/// </summary>
public sealed class PartyDisbandHandler(ZoneRegistry zones, PartyRegistry parties)
    : IInlinePacketHandler<PartyDisbandRequest>
{
    public void Handle(in PartyDisbandRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var leaderId = zoneSession.CharacterId!.Value;

        var members = parties.Disband(leaderId);
        if (members.Count == 0)
            return;

        var notice = new PartyDisbandResponse { Sort = 1, AvatarName = "" };
        foreach (var memberId in members)
            if (zones.TryGetPlayer(memberId, out var member))
                member.Session.Send(notice);
    }
}

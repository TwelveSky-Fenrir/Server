using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>CZ_GUILD_FIND_SEND (opcode 78) -- lookup is process-wide via <see cref="ZoneRegistry" />.</summary>
public sealed class FindGuildMemberHandler(IFindGuildMemberService findGuildMemberService)
    : IInlinePacketHandler<FindGuildMemberRequest>
{
    public void Handle(in FindGuildMemberRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var asker) || asker is null)
            return;

        var result = findGuildMemberService.FindZone(asker, packet.AvatarName);
        if (!result.HasGuild)
            return; // No guild: silent return, not Quit (matches legacy's bare return).

        session.Send(new FindGuildMemberResponse { Result = result.ZoneNumber });
    }
}

using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class FindGuildMemberHandler(
    IFindGuildMemberService findGuildMemberService,
    ILogger<FindGuildMemberHandler>? logger = null) : IAsyncPacketHandler<FindGuildMemberRequest>
{
    public async ValueTask HandleAsync(FindGuildMemberRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_GUILD_FIND_SEND received (character {CharacterId}, target {AvatarName})",
            session.SessionId, zoneSession.CharacterId, packet.AvatarName);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var asker) || asker is null)
            return;

        var result = await findGuildMemberService.FindZoneAsync(asker, packet.AvatarName, cancellationToken)
            .ConfigureAwait(false);
        if (!result.HasGuild)
            return;

        session.Send(new FindGuildMemberResponse { Result = result.ZoneNumber });
    }
}

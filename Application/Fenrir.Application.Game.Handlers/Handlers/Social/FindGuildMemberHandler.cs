using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_GUILD_FIND_SEND (opcode 78) -- lookup is process-wide via <see cref="ZoneRegistry" />, falling back
///     to the cross-shard character-location directory on a same-shard miss. Async (not inline): the
///     fallback is an awaited DB call on the miss branch, and both handler kinds already run on the
///     per-connection session loop, never the zone tick.
/// </summary>
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
            return; // No guild: silent return, not Quit (matches legacy's bare return).

        session.Send(new FindGuildMemberResponse { Result = result.ZoneNumber });
    }
}

using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class FriendAddHandler(IFriendService friendService, ILogger<FriendAddHandler> logger)
    : IAsyncPacketHandler<FriendAddRequest>
{
    public async ValueTask HandleAsync(FriendAddRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;

        logger.LogDebug("FriendAdd: session {SessionId} character {CharacterId} slot {Index}", session.SessionId,
            zoneSession.CharacterId, packet.Index);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = await friendService.AddAsync(state, packet.Index, cancellationToken);

        switch (result.Kind)
        {
            case FriendAddResultKind.InvalidSlot:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case FriendAddResultKind.NoPendingAccept:
                return;
            case FriendAddResultKind.Added:
                session.Send(new FriendAddResponse { Index = packet.Index, AvatarName = result.OtherName });
                return;
        }
    }
}

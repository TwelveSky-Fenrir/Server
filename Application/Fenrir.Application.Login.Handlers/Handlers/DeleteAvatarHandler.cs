using Fenrir.Application.Login.Abstractions.DeleteAvatar;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>Op18 CL_DELETE_AVATAR_SEND: deletes the character at the requested slot (wire contract §4.6).</summary>
public sealed class DeleteAvatarHandler(IDeleteAvatarService deleteAvatarService)
    : IAsyncPacketHandler<DeleteAvatarRequest>
{
    public async ValueTask HandleAsync(DeleteAvatarRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (packet.AvatarPost is < 0 or > 2)
        {
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        await deleteAvatarService.DeleteAvatarAsync(accountId, (byte)packet.AvatarPost, cancellationToken);

        session.Send(new DeleteAvatarResponse { Result = 0 });
    }
}

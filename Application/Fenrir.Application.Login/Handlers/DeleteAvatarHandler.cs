using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>Op18 CL_DELETE_AVATAR_SEND: deletes the character at the requested slot (wire contract §4.6).</summary>
public sealed class DeleteAvatarHandler(ICharacterRepository characters) : IAsyncPacketHandler<DeleteAvatarRequest>
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

        // Idempotent (usp_Character_Delete): an already-empty slot is not an error.
        await characters.DeleteAsync(accountId, (byte)packet.AvatarPost, cancellationToken);

        session.Send(new DeleteAvatarResponse { Result = 0 });
    }
}

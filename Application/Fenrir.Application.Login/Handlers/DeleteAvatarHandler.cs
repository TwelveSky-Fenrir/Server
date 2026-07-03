using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Handlers;

/// <summary>Op18 CL_DELETE_AVATAR_SEND: deletes the character at the requested slot (wire contract §4.6).</summary>
public sealed class DeleteAvatarHandler(CharacterRepository characters) : IAsyncPacketHandler<DeleteAvatarRequest>
{
    public async ValueTask HandleAsync(DeleteAvatarRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;

        // AllowedStates on DeleteAvatarRequest already gate this to Authenticated/CharSelect, both reachable only past MarkAuthenticated.
        var accountId = loginSession.AccountId!.Value;

        // Idempotent by design (Fenrir.Data phase, usp_Character_Delete): an already-empty slot is not an error, so there is nothing to branch on here.
        await characters.DeleteAsync(accountId, (byte)packet.AvatarPost, cancellationToken);

        session.Send(new DeleteAvatarResponse { Result = 0 });
    }
}

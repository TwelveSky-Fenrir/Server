using Fenrir.Application.Login.Abstractions.DeleteAvatar;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     Op18 CL_DELETE_AVATAR_SEND: deletes the character at the requested slot (wire contract §4.6), after the
///     tribe-role/guild-membership/proxy-shop refusal checks. Mode indicator 2 ("transfer") is a genuinely legal
///     wire value the legacy server never implemented -- treated the same as malformed input here: silent
///     disconnect, no response of any kind.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1188-1213 (slot-range/existence preconditions, out of this
///     handler's scope -- assumed already gated upstream) ; Server/ts25login/S04_MyWork02.cpp:1223-1235
///     (mode-indicator gate: 1=delete, 2=transfer -- transfer rejected outright as "not ready yet") ;
///     Server/ts25login/S05_MyTransfer.cpp:216-221 (B_DELETE_AVATAR_RECV wire shape: a single result code) ;
///     ServerDocs/11_ts25login/01_Flux_Authentification_Redirection.md:297-320 (corroborating refusal-code
///     table: 0=success, 1=DB delete failure [not modeled here -- DeleteAvatarService lets it throw], 2=tribe
///     role, 3=guild membership, 4=dead level-restriction code [never produced], 5=proxy shop).
/// </remarks>
public sealed class DeleteAvatarHandler(IDeleteAvatarService deleteAvatarService)
    : IAsyncPacketHandler<DeleteAvatarRequest>
{
    private const int ModeDelete = 1;

    public async ValueTask HandleAsync(DeleteAvatarRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        // Out-of-range slot, mode indicator 2 ("transfer", never implemented server-side), or any other mode
        // value are all silent-disconnect cases -- no B_DELETE_AVATAR_RECV is ever sent for any of them.
        if (packet.AvatarPost is < 0 or > 2 || packet.Unknow1 != ModeDelete)
        {
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var result =
            await deleteAvatarService.DeleteAvatarAsync(accountId, (byte)packet.AvatarPost, cancellationToken);

        switch (result.Outcome)
        {
            case DeleteAvatarOutcome.Success:
                session.Send(new DeleteAvatarResponse { Result = 0 });
                return;
            case DeleteAvatarOutcome.TribeRoleRefusal:
                session.Send(new DeleteAvatarResponse { Result = 2 });
                return;
            case DeleteAvatarOutcome.GuildMembershipRefusal:
                session.Send(new DeleteAvatarResponse { Result = 3 });
                return;
            case DeleteAvatarOutcome.ProxyShopRefusal:
                session.Send(new DeleteAvatarResponse { Result = 5 });
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, null);
        }
    }
}

using Fenrir.Application.Login.Abstractions.CreateAvatar;
using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op17 CL_CREATE_AVATAR_SEND2 -- creates a new character in the requested slot, grants the EU33 starter kit
///     (tribe equipment/inventory/skills/hotkeys, stats, pet, welcome buffs, one premium day) and returns its full
///     AVATAR_INFO payload.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:582-1183 (non-USE_CUSTOME_CREATE branch, the one EU33/LNW33
///     builds compile) ; Server/Header/mapcheck.h:298-326 (GetReturnBornInTownLocation).
/// </remarks>
public sealed class CreateAvatarHandler(ICreateAvatarService createAvatarService)
    : IAsyncPacketHandler<CreateAvatarRequest>
{
    public async ValueTask HandleAsync(CreateAvatarRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (packet.AvatarPost is < 0 or > 2 ||
            packet.Tribe is < 0 or > 3 ||
            packet.PreviousTribe is < 0 or > 2 ||
            packet.Head is < 0 or > 6 ||
            packet.Face is < 0 or > 2)
        {
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var result = await createAvatarService.CreateAvatarAsync(
            accountId,
            (byte)packet.AvatarPost,
            packet.AvatarName,
            (byte)packet.Tribe,
            (byte)packet.PreviousTribe,
            (byte)packet.Gender,
            (byte)packet.Head,
            (byte)packet.Face,
            packet.Weapon,
            cancellationToken);

        switch (result.Outcome)
        {
            case CreateAvatarOutcome.InvalidWeapon:
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            case CreateAvatarOutcome.Success:
                session.Send(new CreateAvatarResponse { Result = 0, AvatarInfo = result.AvatarInfo });
                return;
            default:
                // usp_Character_CreateWithStarterKit throws distinct codes (slot occupied/name taken), but the wire
                // contract only documents Result=1 for any failure -- the legacy client has no finer-grained handling.
                session.Send(new CreateAvatarResponse { Result = 1, AvatarInfo = AvatarInfoFactory.Zeroed });
                return;
        }
    }
}

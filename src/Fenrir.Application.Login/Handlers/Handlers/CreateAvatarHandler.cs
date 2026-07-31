using Fenrir.Application.Login.Abstractions.CreateAvatar;
using Fenrir.Application.Login.Sessions;
using Fenrir.Domain.Login.Avatars;
using Fenrir.Protocol.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class CreateAvatarHandler(ICreateAvatarService createAvatarService, ILogger<CreateAvatarHandler> logger)
    : IAsyncPacketHandler<CreateAvatarRequest>
{
    public async ValueTask HandleAsync(CreateAvatarRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (packet.AvatarPost is < 0 or > 2 ||
            packet.AvatarName.Length == 0 ||
            packet.Tribe is < 0 or > 3 ||
            packet.PreviousTribe is < 0 or > 2 ||
            packet.Gender is < 0 or > 255 ||
            packet.Head is < 0 or > 6 ||
            packet.Face is < 0 or > 2)
        {
            logger.LogWarning(
                "Create-avatar rejected: malformed request from account {AccountId} (slot {Slot}, tribe {Tribe}, " +
                "previousTribe {PreviousTribe})",
                accountId, packet.AvatarPost, packet.Tribe, packet.PreviousTribe);
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
            case CreateAvatarOutcome.InvalidWeapon or CreateAvatarOutcome.FourthFactionDisabled
                or CreateAvatarOutcome.SlotOccupied:
                logger.LogWarning(
                    "Create-avatar rejected: account {AccountId} slot {Slot} outcome {Outcome}", accountId,
                    packet.AvatarPost, result.Outcome);
                loginSession.Abort(DisconnectReason.Malformed);
                return;
            case CreateAvatarOutcome.Success:
                logger.LogInformation(
                    "Avatar created: account {AccountId} slot {Slot} name {AvatarName} tribe {Tribe}", accountId,
                    packet.AvatarPost, packet.AvatarName, packet.Tribe);
                session.Send(new CreateAvatarResponse { Result = 0, AvatarInfo = result.AvatarInfo });
                return;
            case CreateAvatarOutcome.DominantTribeBlocked:
                logger.LogWarning(
                    "Create-avatar rejected: tribe {Tribe} is currently dominant (account {AccountId})",
                    packet.Tribe, accountId);
                session.Send(new CreateAvatarResponse { Result = 3, AvatarInfo = AvatarInfoFactory.Zeroed });
                return;
            case CreateAvatarOutcome.NameTaken:
                logger.LogWarning(
                    "Create-avatar rejected: name {AvatarName} already claimed (account {AccountId} slot {Slot})",
                    packet.AvatarName, accountId, packet.AvatarPost);
                session.Send(new CreateAvatarResponse { Result = 2, AvatarInfo = result.AvatarInfo });
                return;
            default:
                logger.LogWarning(
                    "Create-avatar failed: account {AccountId} slot {Slot} (generic failure, see CreateAvatarService logs)",
                    accountId, packet.AvatarPost);
                session.Send(new CreateAvatarResponse { Result = 1, AvatarInfo = result.AvatarInfo });
                return;
        }
    }
}

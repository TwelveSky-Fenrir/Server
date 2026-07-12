using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Login.Abstractions.CreateAvatar;

public enum CreateAvatarOutcome
{
    InvalidWeapon,
    Success,
    Failure,

    DominantTribeBlocked,

    FourthFactionDisabled,

    SlotOccupied,

    NameTaken
}

public readonly record struct CreateAvatarResult(CreateAvatarOutcome Outcome, AvatarInfo AvatarInfo);

public interface ICreateAvatarService
{
    public ValueTask<CreateAvatarResult> CreateAvatarAsync(
        int accountId,
        byte avatarPost,
        string avatarName,
        byte tribe,
        byte previousTribe,
        byte gender,
        byte head,
        byte face,
        int weapon,
        CancellationToken cancellationToken);
}

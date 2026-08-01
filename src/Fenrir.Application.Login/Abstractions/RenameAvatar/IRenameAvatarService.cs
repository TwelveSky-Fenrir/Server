namespace Fenrir.Application.Login.Abstractions.RenameAvatar;

public enum RenameAvatarOutcome
{
    Success,

    NameTaken,

    ItemMismatch,

    TribeRoleRefusal,

    GuildMembershipRefusal,

    FriendListRefusal,

    TeacherBondRefusal,

    StudentBondRefusal,

    SlotMissing,

    SqlError,

    SlotEmpty,

    Malformed
}

public readonly record struct RenameAvatarResult(RenameAvatarOutcome Outcome);

public interface IRenameAvatarService
{
    public ValueTask<RenameAvatarResult> RenameAvatarAsync(int accountId, byte avatarPost, string changeAvatarName,
        int itemContainer, int itemSlot, CancellationToken cancellationToken);
}

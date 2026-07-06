namespace Fenrir.Application.Login.Abstractions.RenameAvatar;

/// <summary>
///     Op19 CL_CHANGE_AVATAR_NAME_SEND result. The wire numeric result code (or "disconnect, no response" for
///     <see cref="ItemMismatch" />) is assigned by RenameAvatarHandler, not this enum itself -- see that
///     handler's own remarks for the code table.
/// </summary>
public enum RenameAvatarOutcome
{
    Success,

    /// <summary>New name already taken by another character (or unchanged from the current one) (Result=2).</summary>
    NameTaken,

    /// <summary>
    ///     The item at the client-claimed (Page, Index) slot is not exactly item 1133 (the rename scroll) --
    ///     either the slot is empty or holds a different item. No wire response is ever sent for this outcome;
    ///     the legacy behavior is a silent disconnect.
    /// </summary>
    ItemMismatch,

    /// <summary>Tribe master, sub-master, or active election candidate (Result=3).</summary>
    TribeRoleRefusal,

    /// <summary>Still a member of a guild (Result=3).</summary>
    GuildMembershipRefusal,

    /// <summary>At least one occupied friend-list slot (Result=3).</summary>
    FriendListRefusal,

    /// <summary>Currently has a bonded teacher (Result=3).</summary>
    TeacherBondRefusal,

    /// <summary>Currently has a bonded student (Result=3).</summary>
    StudentBondRefusal,

    /// <summary>No character exists at the requested slot (Result=102).</summary>
    SlotMissing,

    /// <summary>The rename repository call faulted (Result=101).</summary>
    SqlError
}

public readonly record struct RenameAvatarResult(RenameAvatarOutcome Outcome);

public interface IRenameAvatarService
{
    /// <summary>
    ///     Runs the item gate, then the five relationship refusals (tribe role, guild, friend, teacher, student,
    ///     in that order, first failure wins), and only if none of them fire, atomically consumes the rename
    ///     scroll and renames the character.
    /// </summary>
    public ValueTask<RenameAvatarResult> RenameAvatarAsync(int accountId, byte avatarPost, string changeAvatarName,
        byte itemContainer, byte itemSlot, CancellationToken cancellationToken);
}

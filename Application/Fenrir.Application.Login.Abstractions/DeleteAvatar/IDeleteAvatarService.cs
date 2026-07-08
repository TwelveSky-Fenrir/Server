namespace Fenrir.Application.Login.Abstractions.DeleteAvatar;

/// <summary>
///     Op18 CL_DELETE_AVATAR_SEND (delete branch) result. The wire's numeric result code is assigned by
///     DeleteAvatarHandler, not this enum itself -- see that handler's own remarks for the code table.
/// </summary>
public enum DeleteAvatarOutcome
{
    Success,

    /// <summary>Tribe master, sub-master, or active election candidate (B_DELETE_AVATAR_RECV Result=2).</summary>
    TribeRoleRefusal,

    /// <summary>Still a member of a guild (B_DELETE_AVATAR_RECV Result=3).</summary>
    GuildMembershipRefusal,

    /// <summary>Pending items/state/money in an offline proxy shop (B_DELETE_AVATAR_RECV Result=5).</summary>
    ProxyShopRefusal,

    /// <summary>The underlying usp_Character_Delete call faulted (B_DELETE_AVATAR_RECV Result=1).</summary>
    SqlError
}

public readonly record struct DeleteAvatarResult(DeleteAvatarOutcome Outcome);

public interface IDeleteAvatarService
{
    /// <summary>
    ///     Runs the delete branch's three business-rule refusals (tribe role, guild membership, proxy shop, in
    ///     that order) and, only if none of them fire, deletes the character. Transfer-mode requests never reach
    ///     this method -- DeleteAvatarHandler rejects them itself (silent disconnect, no response) before calling
    ///     into this service.
    /// </summary>
    public ValueTask<DeleteAvatarResult> DeleteAvatarAsync(int accountId, byte avatarPost,
        CancellationToken cancellationToken);
}

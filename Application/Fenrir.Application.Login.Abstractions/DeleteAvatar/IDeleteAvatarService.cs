namespace Fenrir.Application.Login.Abstractions.DeleteAvatar;

public enum DeleteAvatarOutcome
{
    Success,

    TribeRoleRefusal,

    GuildMembershipRefusal,

    ProxyShopRefusal,

    SqlError
}

public readonly record struct DeleteAvatarResult(DeleteAvatarOutcome Outcome);

public interface IDeleteAvatarService
{
    public ValueTask<DeleteAvatarResult> DeleteAvatarAsync(int accountId, byte avatarPost,
        CancellationToken cancellationToken);
}

namespace Fenrir.Application.Login.Abstractions.RenameAvatar;

public interface IRenameAvatarService
{
    public ValueTask<int> RenameAvatarAsync(int accountId, byte avatarPost, string changeAvatarName,
        CancellationToken cancellationToken);
}

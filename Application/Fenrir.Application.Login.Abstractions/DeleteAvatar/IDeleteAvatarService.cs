namespace Fenrir.Application.Login.Abstractions.DeleteAvatar;

public interface IDeleteAvatarService
{
    public ValueTask DeleteAvatarAsync(int accountId, byte avatarPost, CancellationToken cancellationToken);
}

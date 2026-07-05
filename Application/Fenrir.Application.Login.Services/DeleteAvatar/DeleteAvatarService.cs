using Fenrir.Application.Login.Abstractions.DeleteAvatar;

namespace Fenrir.Application.Login.Services.DeleteAvatar;

/// <summary>Op18 CL_DELETE_AVATAR_SEND business logic: deletes the character at the requested slot.</summary>
public sealed class DeleteAvatarService(ICharacterRepository characters) : IDeleteAvatarService
{
    public async ValueTask DeleteAvatarAsync(int accountId, byte avatarPost, CancellationToken cancellationToken)
    {
        // Idempotent (usp_Character_Delete): an already-empty slot is not an error.
        await characters.DeleteAsync(accountId, avatarPost, cancellationToken);
    }
}

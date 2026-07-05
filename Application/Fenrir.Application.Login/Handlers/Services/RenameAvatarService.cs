using Fenrir.Data.Characters;

namespace Fenrir.Application.Login.Handlers.Services;

public interface IRenameAvatarService
{
    ValueTask<int> RenameAvatarAsync(int accountId, byte avatarPost, string changeAvatarName,
        CancellationToken cancellationToken);
}

/// <summary>
///     op19 CL_CHANGE_AVATAR_NAME_SEND business logic — result codes forwarded verbatim from
///     game.usp_Character_Rename (0 ok, 2 name taken/unchanged, 102 slot missing, 101 SQL error).
/// </summary>
public sealed class RenameAvatarService(ICharacterRenameRepository renames) : IRenameAvatarService
{
    private const int ResultSqlError = 101; // legacy mDB.ChangeCharacterName "SQL error"

    public async ValueTask<int> RenameAvatarAsync(int accountId, byte avatarPost, string changeAvatarName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await renames.RenameAsync(accountId, avatarPost, changeAvatarName, cancellationToken);
        }
        catch (Exception)
        {
            return ResultSqlError;
        }
    }
}

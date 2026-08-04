namespace Fenrir.Application.Game.ZoneLifecycle;

public static class BanAdmissionPolicy
{
    public static async ValueTask<bool> IsBlockedAsync(IBanRepository bans, int accountId, int characterId,
        CancellationToken cancellationToken)
    {
        if (await bans.IsActiveForAccountAsync(accountId, cancellationToken).ConfigureAwait(false))
            return true;

        return await bans.IsActiveForCharacterAsync(characterId, cancellationToken).ConfigureAwait(false);
    }
}

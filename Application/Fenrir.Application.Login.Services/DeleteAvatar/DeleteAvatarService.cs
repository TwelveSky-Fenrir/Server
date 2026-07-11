using Fenrir.Application.Login.Abstractions.DeleteAvatar;
using Fenrir.Application.Login.Domain.Avatars;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Services.DeleteAvatar;

public sealed class DeleteAvatarService(
    ICharacterRepository characters,
    ITribeRepository tribes,
    IWorldStateRepository worldState,
    IGuildRepository guilds,
    IOfflineShopRepository offlineShops,
    ILogger<DeleteAvatarService> logger) : IDeleteAvatarService
{
    public async ValueTask<DeleteAvatarResult> DeleteAvatarAsync(int accountId, byte avatarPost,
        CancellationToken cancellationToken)
    {
        var roster = await characters.GetByAccountAsync(accountId, cancellationToken);
        var character = roster.FirstOrDefault(c => c.Slot == avatarPost);

        if (character is not null)
        {
            var outcome = await CheckBusinessRulesAsync(character, cancellationToken);
            if (outcome != DeleteAvatarOutcome.Success)
                return new DeleteAvatarResult(outcome);
        }

        try
        {
            await characters.DeleteAsync(accountId, avatarPost, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Character delete failed for account {AccountId} slot {AvatarPost}", accountId,
                avatarPost);
            return new DeleteAvatarResult(DeleteAvatarOutcome.SqlError);
        }

        return new DeleteAvatarResult(DeleteAvatarOutcome.Success);
    }

        private async ValueTask<DeleteAvatarOutcome> CheckBusinessRulesAsync(CharacterSummaryDto character,
        CancellationToken ct)
    {
        var tribeRole = await tribes.GetRoleForCharacterAsync(character.CharacterId, ct);
        var ownTribeVotes = await worldState.GetTribeVotesAsync(character.Tribe, ct);
        if (AvatarDeletionGate.TribeRoleBlocksDeletion(tribeRole, character.CharacterId, ownTribeVotes))
            return DeleteAvatarOutcome.TribeRoleRefusal;

        var guildMembership = await guilds.GetByCharacterAsync(character.CharacterId, ct);
        if (AvatarDeletionGate.GuildMembershipBlocksDeletion(guildMembership))
            return DeleteAvatarOutcome.GuildMembershipRefusal;

        var (shop, items) = await offlineShops.GetByCharacterAsync(character.CharacterId, ct);
        if (AvatarDeletionGate.ProxyShopBlocksDeletion(shop, items))
            return DeleteAvatarOutcome.ProxyShopRefusal;

        return DeleteAvatarOutcome.Success;
    }
}

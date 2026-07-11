namespace Fenrir.Application.Login.Domain.Avatars;

public static class AvatarDeletionGate
{

        public static bool TribeRoleBlocksDeletion(byte tribeRole, int characterId,
        IReadOnlyList<TribeVoteDto> ownTribeVotes)
    {
        return tribeRole != 0 || ownTribeVotes.Any(vote => vote.CandidateCharacterId == characterId);
    }

        public static bool GuildMembershipBlocksDeletion(CharacterGuildMembershipDto? membership)
    {
        return membership is not null;
    }

        public static bool ProxyShopBlocksDeletion(OfflineShopRowDto? shop, IReadOnlyList<OfflineShopItemRowDto> items)
    {
        if (shop is null)
            return false;

        if (shop.ShopState != 0 || shop.Money != 0 || shop.BigMoney != 0)
            return true;

        return items.Count > 0;
    }
}

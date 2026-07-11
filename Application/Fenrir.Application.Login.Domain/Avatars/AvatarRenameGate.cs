namespace Fenrir.Application.Login.Domain.Avatars;

public static class AvatarRenameGate
{
    public const int RenameScrollItemId = 1133;

    public static bool ItemAtSlotIsRenameScroll(int? itemIdAtSlot)
    {
        return itemIdAtSlot == RenameScrollItemId;
    }

    public static bool TribeRoleBlocksRename(byte tribeRole, int characterId, IReadOnlyList<TribeVoteDto> ownTribeVotes)
    {
        return tribeRole != 0 || ownTribeVotes.Any(vote => vote.CandidateCharacterId == characterId);
    }

    public static bool GuildMembershipBlocksRename(CharacterGuildMembershipDto? membership)
    {
        return membership is not null;
    }

    public static bool FriendListBlocksRename(IReadOnlyList<CharacterFriendDto> friends)
    {
        return friends.Count > 0;
    }

    public static bool TeacherBondBlocksRename(CharacterMentorDto? mentor)
    {
        return mentor?.TeacherCharacterId is not null;
    }

    public static bool StudentBondBlocksRename(CharacterMentorDto? mentor)
    {
        return mentor?.StudentCharacterId is not null;
    }
}

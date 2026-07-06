namespace Fenrir.Application.Login.Domain.Avatars;

/// <summary>
///     Op19 CL_CHANGE_AVATAR_NAME_SEND's read-only refusal checks: the rename-scroll item gate, and the five
///     relationship refusals (tribe leadership/candidacy, guild membership, active friend, teacher, student).
///     Each predicate below answers exactly one rule in isolation, given data its caller has already fetched;
///     running the five relationship checks in the legacy-mandated order (tribe, guild, friend, teacher,
///     student) and stopping at the first failure is RenameAvatarService's job, not this gate's -- the same
///     split <see cref="AvatarDeletionGate" /> uses for op18's own three-refusal sequence.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1340-1344 (item-1133 gate) ;
///     Server/ts25login/S04_MyWork02.cpp:1358-1362 (tribe-role refusal, calling <c>ReturnTribeRole</c> at
///     Server/Header/function.h:92-114 -- master/sub-master/vote-candidate all collapse to one refusal, the
///     same encoding <see cref="AvatarDeletionGate.TribeRoleBlocksDeletion" /> already reads via the same
///     ITribeRepository/IWorldStateRepository split) ; Server/ts25login/S04_MyWork02.cpp:1363-1367
///     (guild-membership refusal) ; Server/ts25login/S04_MyWork02.cpp:1368-1375 (friend-list refusal, any one
///     of the 10 slots) ; Server/ts25login/S04_MyWork02.cpp:1376-1380 (teacher-bond refusal) ;
///     Server/ts25login/S04_MyWork02.cpp:1381-1385 (student-bond refusal).
/// </remarks>
public static class AvatarRenameGate
{
    /// <summary>Legacy item id 1133 -- the only item the claimed (Page, Index) slot may hold for the rename to proceed.</summary>
    public const int RenameScrollItemId = 1133;

    /// <summary>
    ///     True only if the item at the client-claimed slot is exactly the rename scroll (1133). A null
    ///     <paramref name="itemIdAtSlot" /> means the slot holds nothing at all -- both an empty slot and any
    ///     other item id are treated identically to a wrong item: the legacy check trusts the client-supplied
    ///     coordinates completely and never searches the inventory for item 1133 elsewhere.
    /// </summary>
    public static bool ItemAtSlotIsRenameScroll(int? itemIdAtSlot)
    {
        return itemIdAtSlot == RenameScrollItemId;
    }

    /// <summary>
    ///     True if this character currently holds tribe master or sub-master rank (<paramref name="tribeRole" />
    ///     is non-zero, matching ReturnTribeRole's own 1/2 encoding directly), or is a currently-registered Force
    ///     Leader election candidate for its own tribe (present in <paramref name="ownTribeVotes" /> under its
    ///     own <paramref name="characterId" />) -- all three ranks collapse to the same refusal.
    /// </summary>
    public static bool TribeRoleBlocksRename(byte tribeRole, int characterId, IReadOnlyList<TribeVoteDto> ownTribeVotes)
    {
        return tribeRole != 0 || ownTribeVotes.Any(vote => vote.CandidateCharacterId == characterId);
    }

    /// <summary>True if this character currently belongs to any guild.</summary>
    public static bool GuildMembershipBlocksRename(CharacterGuildMembershipDto? membership)
    {
        return membership is not null;
    }

    /// <summary>True if any of this character's (up to 10) friend-list slots is occupied.</summary>
    public static bool FriendListBlocksRename(IReadOnlyList<CharacterFriendDto> friends)
    {
        return friends.Count > 0;
    }

    /// <summary>True if this character currently has a bonded teacher.</summary>
    public static bool TeacherBondBlocksRename(CharacterMentorDto? mentor)
    {
        return mentor?.TeacherCharacterId is not null;
    }

    /// <summary>True if this character currently has a bonded student.</summary>
    public static bool StudentBondBlocksRename(CharacterMentorDto? mentor)
    {
        return mentor?.StudentCharacterId is not null;
    }
}

using Fenrir.Application.Login.Domain.Avatars;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.Social;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Login.Tests.Avatars;

// Op19 CL_CHANGE_AVATAR_NAME_SEND's item gate and five relationship refusals. Réf. C++ :
// Server/ts25login/S04_MyWork02.cpp:1340-1385.
public class AvatarRenameGateTests
{
    [Fact]
    public void ItemAtSlotIsRenameScroll_ExactMatch_True()
    {
        Assert.True(AvatarRenameGate.ItemAtSlotIsRenameScroll(1133));
    }

    [Theory]
    [InlineData(null)] // empty slot
    [InlineData(1)] // some other item
    [InlineData(0)] // legacy "empty" sentinel value, still not 1133
    public void ItemAtSlotIsRenameScroll_EmptyOrWrongItem_False(int? itemId)
    {
        Assert.False(AvatarRenameGate.ItemAtSlotIsRenameScroll(itemId));
    }

    [Fact]
    public void TribeRoleBlocksRename_RegularMemberNotCandidate_NeverBlocks()
    {
        Assert.False(AvatarRenameGate.TribeRoleBlocksRename(0, 100, []));
    }

    [Theory]
    [InlineData((byte)1)] // tribe master
    [InlineData((byte)2)] // tribe sub-master
    public void TribeRoleBlocksRename_MasterOrSubMaster_Blocks(byte role)
    {
        Assert.True(AvatarRenameGate.TribeRoleBlocksRename(role, 100, []));
    }

    [Fact]
    public void TribeRoleBlocksRename_RegisteredVoteCandidateForOwnTribe_Blocks()
    {
        var votes = new[]
        {
            new TribeVoteDto(0, 0, 100, 50, 0, 10, DateTime.UtcNow),
            new TribeVoteDto(0, 1, 200, 60, 0, 5, DateTime.UtcNow)
        };

        Assert.True(AvatarRenameGate.TribeRoleBlocksRename(0, 100, votes));
        Assert.False(AvatarRenameGate.TribeRoleBlocksRename(0, 999, votes));
    }

    [Fact]
    public void GuildMembershipBlocksRename_NoMembership_Allowed()
    {
        Assert.False(AvatarRenameGate.GuildMembershipBlocksRename(null));
    }

    [Fact]
    public void GuildMembershipBlocksRename_AnyMembership_Blocks()
    {
        var membership = new CharacterGuildMembershipDto(1, "Guild", 0, "Rookie");

        Assert.True(AvatarRenameGate.GuildMembershipBlocksRename(membership));
    }

    [Fact]
    public void FriendListBlocksRename_NoFriends_Allowed()
    {
        Assert.False(AvatarRenameGate.FriendListBlocksRename([]));
    }

    [Fact]
    public void FriendListBlocksRename_SingleOccupiedSlotAnywhere_Blocks()
    {
        var friends = new[] { new CharacterFriendDto(7, 555, "Buddy") };

        Assert.True(AvatarRenameGate.FriendListBlocksRename(friends));
    }

    [Fact]
    public void TeacherBondBlocksRename_NoMentorRow_Allowed()
    {
        Assert.False(AvatarRenameGate.TeacherBondBlocksRename(null));
    }

    [Fact]
    public void TeacherBondBlocksRename_NoTeacher_Allowed()
    {
        var mentor = new CharacterMentorDto(null, null, 42, "Student");

        Assert.False(AvatarRenameGate.TeacherBondBlocksRename(mentor));
    }

    [Fact]
    public void TeacherBondBlocksRename_HasTeacher_Blocks()
    {
        var mentor = new CharacterMentorDto(42, "Teacher", null, null);

        Assert.True(AvatarRenameGate.TeacherBondBlocksRename(mentor));
    }

    [Fact]
    public void StudentBondBlocksRename_NoMentorRow_Allowed()
    {
        Assert.False(AvatarRenameGate.StudentBondBlocksRename(null));
    }

    [Fact]
    public void StudentBondBlocksRename_NoStudent_Allowed()
    {
        var mentor = new CharacterMentorDto(42, "Teacher", null, null);

        Assert.False(AvatarRenameGate.StudentBondBlocksRename(mentor));
    }

    [Fact]
    public void StudentBondBlocksRename_HasStudent_Blocks()
    {
        var mentor = new CharacterMentorDto(null, null, 42, "Student");

        Assert.True(AvatarRenameGate.StudentBondBlocksRename(mentor));
    }
}

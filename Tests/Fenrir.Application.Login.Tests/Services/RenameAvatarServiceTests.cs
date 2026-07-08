using Fenrir.Application.Login.Abstractions.RenameAvatar;
using Fenrir.Application.Login.Services.RenameAvatar;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.Social;
using Fenrir.Data.Abstractions.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Services;

// Op19 CL_CHANGE_AVATAR_NAME_SEND: the rename-scroll item gate, then the five read-only relationship
// refusals (tribe role, guild, friend, teacher, student, in that fixed order), first failure wins, and
// only if none of them fire does the atomic rename-scroll consumption + rename itself run. Réf. C++ :
// Server/ts25login/S04_MyWork02.cpp:1340-1385.
public class RenameAvatarServiceTests
{
    private const int AccountId = 42;
    private const int CharacterId = 1000;
    private const byte Slot = 0;
    private const byte Tribe = 1;
    private const byte ItemContainer = 0;
    private const byte ItemSlot = 5;
    private const int RenameScrollItemId = 1133;

    private static CharacterSummaryDto Summary => new(CharacterId, Slot, "Hero", Tribe, 0, 0, 0, 10);

    private static RenameAvatarService BuildService(
        FakeCharacterRepository characters,
        FakeCharacterRenameRepository? renames = null,
        FakeTribeRepository? tribes = null,
        FakeWorldStateRepository? worldState = null,
        FakeGuildRepository? guilds = null,
        FakeFriendRepository? friends = null,
        FakeMentorRepository? mentors = null,
        FakeEventLogRepository? eventLog = null)
    {
        return new RenameAvatarService(
            characters,
            tribes ?? FakeTribeRepository.Empty(),
            worldState ?? FakeWorldStateRepository.Empty(),
            guilds ?? FakeGuildRepository.Empty(),
            friends ?? FakeFriendRepository.Empty(),
            mentors ?? FakeMentorRepository.Empty(),
            renames ?? FakeCharacterRenameRepository.ReturningResult(0),
            eventLog ?? new FakeEventLogRepository(),
            NullLogger<RenameAvatarService>.Instance);
    }

    [Fact]
    public async Task RenameAvatarAsync_NoRefusalsApplyAndItemMatches_RenamesAndLogsEvent()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var eventLog = new FakeEventLogRepository();
        var service = BuildService(characters, renames, eventLog: eventLog);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.Success, result.Outcome);
        Assert.NotNull(renames.LastCall);
        Assert.Equal((AccountId, Slot, "NewName", ItemContainer, ItemSlot), renames.LastCall!.Value);
        var logged = Assert.Single(eventLog.LoggedEvents);
        Assert.Contains("NewName", logged.Payload);
    }

    [Fact]
    public async Task RenameAvatarAsync_EmptySlot_ReturnsSlotMissingWithoutQueryingAnything()
    {
        var characters = FakeCharacterRepository.WithNone();
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var service = BuildService(characters, renames);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.SlotMissing, result.Outcome);
        Assert.Null(renames.LastCall);
        Assert.Empty(characters.QueriedItemSlots);
    }

    [Theory]
    [InlineData(null)] // empty slot
    [InlineData(1)] // wrong item
    public async Task RenameAvatarAsync_ItemAtSlotIsNotRenameScroll_ReturnsItemMismatchWithoutRelationshipChecks(
        int? itemIdAtSlot)
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        if (itemIdAtSlot is { } id)
            characters.WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, id);
        var guilds = FakeGuildRepository.Empty();
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var service = BuildService(characters, renames, guilds: guilds);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.ItemMismatch, result.Outcome);
        Assert.Null(renames.LastCall);
        Assert.Empty(guilds.QueriedCharacterIds);
    }

    [Theory]
    [InlineData((byte)1)] // tribe master
    [InlineData((byte)2)] // tribe sub-master
    public async Task RenameAvatarAsync_TribeMasterOrSubMaster_RefusesWithoutRenamingOrQueryingLaterChecks(byte role)
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
        var guilds = FakeGuildRepository.Empty();
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var tribes = FakeTribeRepository.WithRole(CharacterId, role);
        var service = BuildService(characters, renames, tribes, guilds: guilds);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.TribeRoleRefusal, result.Outcome);
        Assert.Null(renames.LastCall);
        Assert.Empty(guilds.QueriedCharacterIds);
    }

    [Fact]
    public async Task RenameAvatarAsync_RegisteredTribeVoteCandidate_RefusesAsTribeRole()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
        var votes = new[] { new TribeVoteDto(Tribe, 0, CharacterId, 50, 0, 10, DateTime.UtcNow) };
        var worldState = FakeWorldStateRepository.WithVotes(votes);
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var service = BuildService(characters, renames, worldState: worldState);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.TribeRoleRefusal, result.Outcome);
        Assert.Null(renames.LastCall);
    }

    [Fact]
    public async Task RenameAvatarAsync_GuildMember_RefusesWithoutQueryingFriendOrMentor()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
        var friends = FakeFriendRepository.Empty();
        var mentors = FakeMentorRepository.Empty();
        var membership = new CharacterGuildMembershipDto(1, "Guild", 0, "Rookie");
        var guilds = FakeGuildRepository.WithMembership(CharacterId, membership);
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var service = BuildService(characters, renames, guilds: guilds, friends: friends, mentors: mentors);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.GuildMembershipRefusal, result.Outcome);
        Assert.Null(renames.LastCall);
        Assert.Empty(friends.QueriedCharacterIds);
        Assert.Empty(mentors.QueriedCharacterIds);
    }

    [Fact]
    public async Task RenameAvatarAsync_HasAnyFriend_RefusesWithoutQueryingMentor()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
        var mentors = FakeMentorRepository.Empty();
        var friends = FakeFriendRepository.With(CharacterId, new CharacterFriendDto(7, 555, "Buddy"));
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var service = BuildService(characters, renames, friends: friends, mentors: mentors);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.FriendListRefusal, result.Outcome);
        Assert.Null(renames.LastCall);
        Assert.Empty(mentors.QueriedCharacterIds);
    }

    [Fact]
    public async Task RenameAvatarAsync_HasTeacher_RefusesAsTeacherBond()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
        var mentors = FakeMentorRepository.With(CharacterId, new CharacterMentorDto(42, "Teacher", null, null));
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var service = BuildService(characters, renames, mentors: mentors);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.TeacherBondRefusal, result.Outcome);
        Assert.Null(renames.LastCall);
    }

    [Fact]
    public async Task RenameAvatarAsync_HasStudent_RefusesAsStudentBond()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
        var mentors = FakeMentorRepository.With(CharacterId, new CharacterMentorDto(null, null, 42, "Student"));
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var service = BuildService(characters, renames, mentors: mentors);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.StudentBondRefusal, result.Outcome);
        Assert.Null(renames.LastCall);
    }

    [Fact]
    public async Task RenameAvatarAsync_TribeAndGuildBothApply_TribeRoleWinsBecauseItRunsFirst()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
        var membership = new CharacterGuildMembershipDto(1, "Guild", 0, "Rookie");
        var guilds = FakeGuildRepository.WithMembership(CharacterId, membership);
        var tribes = FakeTribeRepository.WithRole(CharacterId, 1);
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var service = BuildService(characters, renames, tribes, guilds: guilds);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.TribeRoleRefusal, result.Outcome);
        Assert.Empty(guilds.QueriedCharacterIds);
    }

    [Fact]
    public async Task RenameAvatarAsync_RepositoryReturnsNameTaken_ReturnsNameTakenWithoutLoggingEvent()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
        var renames = FakeCharacterRenameRepository.ReturningResult(2);
        var eventLog = new FakeEventLogRepository();
        var service = BuildService(characters, renames, eventLog: eventLog);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.NameTaken, result.Outcome);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task RenameAvatarAsync_RepositoryReturnsSlotVanished_ReturnsSlotMissing()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
        var renames = FakeCharacterRenameRepository.ReturningResult(102);
        var service = BuildService(characters, renames);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.SlotMissing, result.Outcome);
    }

    [Fact]
    public async Task RenameAvatarAsync_RepositoryReturnsItemRaceSentinel_ReturnsItemMismatch()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
        var renames = FakeCharacterRenameRepository.ReturningResult(-1);
        var service = BuildService(characters, renames);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.ItemMismatch, result.Outcome);
    }

    [Fact]
    public async Task RenameAvatarAsync_RepositoryThrows_ReturnsSqlErrorWithoutLoggingEvent()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
        var renames = FakeCharacterRenameRepository.Throwing(new InvalidOperationException("boom"));
        var eventLog = new FakeEventLogRepository();
        var service = BuildService(characters, renames, eventLog: eventLog);

        var result = await service.RenameAvatarAsync(AccountId, Slot, "NewName", ItemContainer, ItemSlot,
            CancellationToken.None);

        Assert.Equal(RenameAvatarOutcome.SqlError, result.Outcome);
        Assert.Empty(eventLog.LoggedEvents);
    }
}

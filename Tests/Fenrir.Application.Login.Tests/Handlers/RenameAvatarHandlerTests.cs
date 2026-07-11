using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.RenameAvatar;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Login.Packets.Login;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Handlers;

public class ClChangeAvatarNameSendHandlerTests
{
    private const int AccountId = 42;
    private const int CharacterId = 1000;
    private const byte Slot = 0;
    private const byte Tribe = 1;
    private const byte ItemContainer = 0;
    private const byte ItemSlot = 5;
    private const int RenameScrollItemId = 1133;

    private static CharacterSummaryDto Summary => new(CharacterId, Slot, "Hero", Tribe, 0, 0, 0, 10);

    private static RenameAvatarRequest Request(int avatarPost = Slot, string name = "NewName",
        int page = ItemContainer, int index = ItemSlot)
    {
        return new RenameAvatarRequest
        {
            AvatarPost = avatarPost,
            ChangeAvatarName = name,
            Page = page,
            Index = index
        };
    }

    private static RenameAvatarHandler BuildHandler(
        FakeCharacterRepository characters,
        FakeCharacterRenameRepository? renames = null,
        FakeTribeRepository? tribes = null,
        FakeWorldStateRepository? worldState = null,
        FakeGuildRepository? guilds = null,
        FakeFriendRepository? friends = null,
        FakeMentorRepository? mentors = null,
        FakeEventLogRepository? eventLog = null)
    {
        var service = new RenameAvatarService(
            characters,
            tribes ?? FakeTribeRepository.Empty(),
            worldState ?? FakeWorldStateRepository.Empty(),
            guilds ?? FakeGuildRepository.Empty(),
            friends ?? FakeFriendRepository.Empty(),
            mentors ?? FakeMentorRepository.Empty(),
            renames ?? FakeCharacterRenameRepository.ReturningResult(0),
            eventLog ?? new FakeEventLogRepository(),
            NullLogger<RenameAvatarService>.Instance);
        return new RenameAvatarHandler(service, NullLogger<RenameAvatarHandler>.Instance);
    }

    private static FakeCharacterRepository CharactersWithRenameScroll()
    {
        return FakeCharacterRepository.WithSummaries(Summary)
            .WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, RenameScrollItemId);
    }

    [Fact]
    public async Task HandleAsync_RenameSucceeds_RepliesResultZeroAndLogsEvent()
    {
        var characters = CharactersWithRenameScroll();
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var eventLog = new FakeEventLogRepository();
        var handler = BuildHandler(characters, renames, eventLog: eventLog);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(), session, CancellationToken.None);

        Assert.NotNull(renames.LastCall);
        Assert.Equal(AccountId, renames.LastCall.Value.AccountId);
        Assert.Equal(Slot, renames.LastCall.Value.Slot);
        Assert.Equal("NewName", renames.LastCall.Value.NewName);
        await PacketAssert.AssertSentAsync(pipe, new RenameAvatarResponse { Result = 0 });
        Assert.Null(session.DisconnectReason);
        Assert.Single(eventLog.LoggedEvents);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(102)]
    public async Task HandleAsync_ProcReturnsLegacyFailureCode_ForwardsItVerbatim(int code)
    {
        var characters = CharactersWithRenameScroll();
        var renames = FakeCharacterRenameRepository.ReturningResult(code);
        var eventLog = new FakeEventLogRepository();
        var handler = BuildHandler(characters, renames, eventLog: eventLog);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new RenameAvatarResponse { Result = code });
        Assert.Empty(eventLog.LoggedEvents);
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_RepliesResult101WithoutDisconnecting()
    {
        var characters = CharactersWithRenameScroll();
        var renames = FakeCharacterRenameRepository.Throwing(new InvalidOperationException("boom"));
        var eventLog = new FakeEventLogRepository();
        var handler = BuildHandler(characters, renames, eventLog: eventLog);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new RenameAvatarResponse { Result = 101 });
        Assert.Null(session.DisconnectReason);
        Assert.Empty(eventLog.LoggedEvents);
    }

    [Fact]
    public async Task HandleAsync_NameUnchangedAndNoScrollPresent_RepliesResultTwoWithoutDisconnecting()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var handler = BuildHandler(characters, renames);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(name: Summary.Name), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new RenameAvatarResponse { Result = 2 });
        Assert.Null(renames.LastCall);
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_NoCharacterAtSlot_RepliesResult102WithoutCallingRenameRepository()
    {
        var characters = FakeCharacterRepository.WithNone();
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var handler = BuildHandler(characters, renames);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new RenameAvatarResponse { Result = 102 });
        Assert.Null(renames.LastCall);
        Assert.Null(session.DisconnectReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(4321)]
    public async Task HandleAsync_ItemAtSlotIsNotRenameScroll_AbortsWithoutReplying(int? itemIdAtSlot)
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        if (itemIdAtSlot is { } id)
            characters.WithItemAtSlot(CharacterId, ItemContainer, ItemSlot, id);
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var handler = BuildHandler(characters, renames);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        Assert.Null(renames.LastCall);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_GuildMember_RepliesResultThreeWithoutRenaming()
    {
        var characters = CharactersWithRenameScroll();
        var membership = new CharacterGuildMembershipDto(1, "Guild", 0, "Rookie");
        var guilds = FakeGuildRepository.WithMembership(CharacterId, membership);
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var handler = BuildHandler(characters, renames, guilds: guilds);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new RenameAvatarResponse { Result = 3 });
        Assert.Null(renames.LastCall);
        Assert.Null(session.DisconnectReason);
    }

    [Theory]
    [InlineData(-1, "Name", 0, 0)]
    [InlineData(3, "Name", 0, 0)]
    [InlineData(0, "", 0, 0)]
    [InlineData(0, "Name", 2, 0)]
    [InlineData(0, "Name", 0, 64)]
    public async Task HandleAsync_StructuralViolation_AbortsWithoutCallingRepository(int avatarPost, string name,
        int page, int index)
    {
        var characters = CharactersWithRenameScroll();
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var handler = BuildHandler(characters, renames);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(avatarPost, name, page, index), session, CancellationToken.None);

        Assert.Null(renames.LastCall);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Theory]
    [InlineData("New Name")]
    [InlineData("New_Name")]
    [InlineData("New-Name")]
    [InlineData("Nômme")]
    public async Task HandleAsync_NewNameWithDisallowedCharacters_AbortsWithoutCallingRepository(string name)
    {
        var characters = CharactersWithRenameScroll();
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var handler = BuildHandler(characters, renames);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(name: name), session, CancellationToken.None);

        Assert.Null(renames.LastCall);
        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        PacketAssert.AssertNothingSent(pipe);
    }

    private static (LoginClientSession Session, FakeDuplexPipe Pipe) CreateSessionInCharSelect()
    {
        var pipe = new FakeDuplexPipe();
        var session = new LoginClientSession(1, pipe);
        session.MarkAuthenticated(AccountId);
        session.MarkCharSelect();
        return (session, pipe);
    }
}

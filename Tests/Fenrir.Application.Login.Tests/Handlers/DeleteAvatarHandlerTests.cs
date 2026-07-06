using Fenrir.Application.Login.Handlers.Handlers;
using Fenrir.Application.Login.Services.DeleteAvatar;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Login.Tests.Handlers;

// Op18 CL_DELETE_AVATAR_SEND. Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1186-1285 ;
// Server/ts25login/S05_MyTransfer.cpp:216-221 (B_DELETE_AVATAR_RECV: a single result code, no other payload).
public class DeleteAvatarHandlerTests
{
    private const int AccountId = 42;
    private const int CharacterId = 1000;
    private const byte Slot = 0;
    private const byte Tribe = 1;

    private static CharacterSummaryDto Summary => new(CharacterId, Slot, "Hero", Tribe, 0, 0, 0, 10);

    private static DeleteAvatarRequest Request(int avatarPost = Slot, int mode = 1, int unused = 0)
    {
        return new DeleteAvatarRequest
        {
            AvatarPost = avatarPost,
            Unknow1 = mode,
            Unknow2 = unused
        };
    }

    [Fact]
    public async Task HandleAsync_NoRefusalsApply_DeletesAndRepliesResultZero()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var handler = new DeleteAvatarHandler(
            new DeleteAvatarService(characters, FakeTribeRepository.Empty(),
                FakeWorldStateRepository.Empty(), FakeGuildRepository.Empty(), FakeOfflineShopRepository.Empty(),
                NullLogger<DeleteAvatarService>.Instance),
            NullLogger<DeleteAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new DeleteAvatarResponse { Result = 0 });
        Assert.Single(characters.DeleteCalls);
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_TribeMaster_RepliesResultTwoWithoutDeleting()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var handler = new DeleteAvatarHandler(
            new DeleteAvatarService(characters,
                FakeTribeRepository.WithRole(CharacterId, 1), FakeWorldStateRepository.Empty(),
                FakeGuildRepository.Empty(), FakeOfflineShopRepository.Empty(),
                NullLogger<DeleteAvatarService>.Instance),
            NullLogger<DeleteAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new DeleteAvatarResponse { Result = 2 });
        Assert.Empty(characters.DeleteCalls);
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_GuildMember_RepliesResultThreeWithoutDeleting()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var membership = new CharacterGuildMembershipDto(1, "Guild", 0, "Rookie");
        var handler = new DeleteAvatarHandler(
            new DeleteAvatarService(characters, FakeTribeRepository.Empty(),
                FakeWorldStateRepository.Empty(), FakeGuildRepository.WithMembership(CharacterId, membership),
                FakeOfflineShopRepository.Empty(), NullLogger<DeleteAvatarService>.Instance),
            NullLogger<DeleteAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new DeleteAvatarResponse { Result = 3 });
        Assert.Empty(characters.DeleteCalls);
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task HandleAsync_PendingProxyShop_RepliesResultFiveWithoutDeleting()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var shop = new OfflineShopRowDto(CharacterId, null, 1, 0, 0, 0, 0, 0, 0, "");
        var handler = new DeleteAvatarHandler(
            new DeleteAvatarService(characters, FakeTribeRepository.Empty(),
                FakeWorldStateRepository.Empty(), FakeGuildRepository.Empty(),
                FakeOfflineShopRepository.With(CharacterId, shop), NullLogger<DeleteAvatarService>.Instance),
            NullLogger<DeleteAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(), session, CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new DeleteAvatarResponse { Result = 5 });
        Assert.Empty(characters.DeleteCalls);
        Assert.Null(session.DisconnectReason);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public async Task HandleAsync_AvatarPostOutOfRange_AbortsWithoutReplying(int avatarPost)
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var handler = new DeleteAvatarHandler(
            new DeleteAvatarService(characters, FakeTribeRepository.Empty(),
                FakeWorldStateRepository.Empty(), FakeGuildRepository.Empty(), FakeOfflineShopRepository.Empty(),
                NullLogger<DeleteAvatarService>.Instance),
            NullLogger<DeleteAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(avatarPost), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        Assert.Empty(characters.DeleteCalls);
        PacketAssert.AssertNothingSent(pipe);
    }

    // Server/ts25login/S04_MyWork02.cpp:1231-1235: mode indicator 2 ("transfer") is a legal wire value the
    // legacy server never implemented -- treated identically to malformed input (silent disconnect).
    [Fact]
    public async Task HandleAsync_TransferMode_AbortsWithoutReplyingOrDeleting()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var handler = new DeleteAvatarHandler(
            new DeleteAvatarService(characters, FakeTribeRepository.Empty(),
                FakeWorldStateRepository.Empty(), FakeGuildRepository.Empty(), FakeOfflineShopRepository.Empty(),
                NullLogger<DeleteAvatarService>.Instance),
            NullLogger<DeleteAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(mode: 2), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        Assert.Empty(characters.DeleteCalls);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task HandleAsync_ModeOutsideTheKnownPair_AbortsWithoutReplyingOrDeleting()
    {
        var characters = FakeCharacterRepository.WithSummaries(Summary);
        var handler = new DeleteAvatarHandler(
            new DeleteAvatarService(characters, FakeTribeRepository.Empty(),
                FakeWorldStateRepository.Empty(), FakeGuildRepository.Empty(), FakeOfflineShopRepository.Empty(),
                NullLogger<DeleteAvatarService>.Instance),
            NullLogger<DeleteAvatarHandler>.Instance);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(Request(mode: 99), session, CancellationToken.None);

        Assert.Equal(DisconnectReason.Malformed, session.DisconnectReason);
        Assert.Empty(characters.DeleteCalls);
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

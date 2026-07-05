using Fenrir.Application.Login.Handlers;
using Fenrir.Application.Login.Tests.TestSupport;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Login.Tests.Handlers;

// op19 CL_CHANGE_AVATAR_NAME_SEND -- legacy result codes (0/2/101/102) are forwarded verbatim from
// game.usp_Character_Rename; structural bounds violations Quit().
public class ClChangeAvatarNameSendHandlerTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task HandleAsync_RenameSucceeds_RepliesResultZero()
    {
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var handler = new RenameAvatarHandler(renames);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(
            new RenameAvatarRequest { AvatarPost = 1, ChangeAvatarName = "NewName", Page = 0, Index = 5 },
            session, CancellationToken.None);

        Assert.NotNull(renames.LastCall);
        Assert.Equal(AccountId, renames.LastCall.Value.AccountId);
        Assert.Equal((byte)1, renames.LastCall.Value.Slot);
        Assert.Equal("NewName", renames.LastCall.Value.NewName);
        await PacketAssert.AssertSentAsync(pipe, new RenameAvatarResponse { Result = 0 });
        Assert.Null(session.DisconnectReason);
    }

    [Theory]
    [InlineData(2)] // name taken / new == current
    [InlineData(102)] // slot vanished
    public async Task HandleAsync_ProcReturnsLegacyFailureCode_ForwardsItVerbatim(int code)
    {
        var renames = FakeCharacterRenameRepository.ReturningResult(code);
        var handler = new RenameAvatarHandler(renames);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(
            new RenameAvatarRequest { AvatarPost = 0, ChangeAvatarName = "Name", Page = 0, Index = 0 }, session,
            CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new RenameAvatarResponse { Result = code });
    }

    [Fact]
    public async Task HandleAsync_RepositoryThrows_RepliesResult101WithoutDisconnecting()
    {
        var renames = FakeCharacterRenameRepository.Throwing(new InvalidOperationException("boom"));
        var handler = new RenameAvatarHandler(renames);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(
            new RenameAvatarRequest { AvatarPost = 0, ChangeAvatarName = "Name", Page = 0, Index = 0 }, session,
            CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new RenameAvatarResponse { Result = 101 });
        Assert.Null(session.DisconnectReason);
    }

    [Theory]
    [InlineData(-1, "Name", 0, 0)] // AvatarPost out of range
    [InlineData(3, "Name", 0, 0)] // AvatarPost out of range (> MAX_USER_AVATAR_NUM-1)
    [InlineData(0, "", 0, 0)] // empty name
    [InlineData(0, "Name", 2, 0)] // page out of range (MAX_INVENTORY_PAGE_NUM=2)
    [InlineData(0, "Name", 0, 64)] // index out of range (MAX_INVENTORY_SLOT_NUM=64)
    public async Task HandleAsync_StructuralViolation_AbortsWithoutCallingRepository(int avatarPost, string name,
        int page, int index)
    {
        var renames = FakeCharacterRenameRepository.ReturningResult(0);
        var handler = new RenameAvatarHandler(renames);
        var (session, pipe) = CreateSessionInCharSelect();

        await handler.HandleAsync(
            new RenameAvatarRequest { AvatarPost = avatarPost, ChangeAvatarName = name, Page = page, Index = index },
            session, CancellationToken.None);

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

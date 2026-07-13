using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

public enum FriendAskResultKind
{
    MapForbidden,
    TargetNotFound,
    AlreadyFriendOrFull,
    TribeMismatch,
    AskerBusy,
    TargetBusy,
    Sent,

    SentCrossShard
}

public enum FriendLocateResultKind
{
    IndexOutOfRange,
    SlotEmpty,
    Found
}

public readonly record struct FriendLocateResult(FriendLocateResultKind Kind, int ZoneNumber = -1);

public enum FriendAddResultKind
{
    InvalidSlot,
    NoPendingAccept,
    Added
}

public readonly record struct FriendAddResult(FriendAddResultKind Kind, string OtherName = "");

public enum FriendRemoveResultKind
{
    IndexOutOfRange,
    SlotEmpty,
    Removed
}

public interface IFriendService
{
    public ValueTask<FriendAskResultKind> AskAsync(Zone zone, PlayerRuntimeState asker, string targetAvatarName,
        CancellationToken cancellationToken);

    public void Answer(int targetId, int answerCode);

    public void Cancel(int askerId);

    public ValueTask<FriendLocateResult> LocateAsync(PlayerRuntimeState asker, int index,
        CancellationToken cancellationToken);

    public ValueTask<FriendAddResult>
        AddAsync(PlayerRuntimeState state, int index, CancellationToken cancellationToken);

    public ValueTask<FriendRemoveResultKind> RemoveAsync(PlayerRuntimeState state, int index,
        CancellationToken cancellationToken);
}

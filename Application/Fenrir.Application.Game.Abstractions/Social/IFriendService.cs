using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Outcome of CZ_FRIEND_ASK_SEND's pre-checks, as branched on by <see cref="FriendAskHandler" />.</summary>
public enum FriendAskResultKind
{
    MapForbidden, // map 124 silently ignored (scripted-duel server)
    TargetNotFound,
    AlreadyFriendOrFull,
    TribeMismatch,
    AskerBusy,
    TargetBusy,
    Sent
}

/// <summary>Outcome of CZ_FRIEND_FIND_SEND, as branched on by <see cref="FriendLocateHandler" />.</summary>
public enum FriendLocateResultKind
{
    IndexOutOfRange, // silent no-op, not Abort
    SlotEmpty, // Abort
    Found
}

public readonly record struct FriendLocateResult(FriendLocateResultKind Kind, int ZoneNumber = -1);

/// <summary>Outcome of CZ_FRIEND_MAKE_SEND, as branched on by <see cref="FriendAddHandler" />.</summary>
public enum FriendAddResultKind
{
    InvalidSlot, // Abort
    NoPendingAccept, // silent no-op
    Added
}

public readonly record struct FriendAddResult(FriendAddResultKind Kind, string OtherName = "");

/// <summary>Outcome of CZ_FRIEND_DELETE_SEND, as branched on by <see cref="FriendRemoveHandler" />.</summary>
public enum FriendRemoveResultKind
{
    InvalidSlot, // Abort
    Removed
}

/// <summary>Business logic behind the CZ_FRIEND_* opcode family, extracted from the Friend*Handlers.</summary>
public interface IFriendService
{
    public FriendAskResultKind Ask(Zone zone, PlayerRuntimeState asker, string targetAvatarName);

    public void Answer(int targetId, int answerCode);

    public void Cancel(int askerId);

    public FriendLocateResult Locate(PlayerRuntimeState asker, int index);

    public ValueTask<FriendAddResult>
        AddAsync(PlayerRuntimeState state, int index, CancellationToken cancellationToken);

    public ValueTask<FriendRemoveResultKind> RemoveAsync(PlayerRuntimeState state, int index,
        CancellationToken cancellationToken);
}

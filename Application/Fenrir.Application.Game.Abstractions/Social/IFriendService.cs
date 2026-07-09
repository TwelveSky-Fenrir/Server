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
    Sent,

    /// <summary>
    ///     WS1.4: the target was not found on this shard's own <c>ZoneRegistry</c> but WAS resolved on a
    ///     different live shard via <c>ICharacterShardLocationRepository</c> -- the ask has been handed to
    ///     <c>ISocialCrossShardRelayQueue</c> for asynchronous cross-shard delivery instead of the immediate
    ///     local <see cref="Sent" /> notification. The caller (<see cref="FriendAskHandler" />) sends nothing
    ///     further; any reply (accept/decline/target-unreachable) arrives later via
    ///     <c>FriendCrossShardRelayHandler.HandleAnswerAsync</c>.
    /// </summary>
    SentCrossShard
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
    IndexOutOfRange, // silent no-op, not Abort
    SlotEmpty, // Abort
    Removed
}

/// <summary>Business logic behind the CZ_FRIEND_* opcode family, extracted from the Friend*Handlers.</summary>
public interface IFriendService
{
    /// <summary>
    ///     Same-shard lookup first (within <paramref name="zone" />), falling back to the cross-shard
    ///     character-location directory on a miss -- see <see cref="FriendAskResultKind.SentCrossShard" />.
    /// </summary>
    public ValueTask<FriendAskResultKind> AskAsync(Zone zone, PlayerRuntimeState asker, string targetAvatarName,
        CancellationToken cancellationToken);

    public void Answer(int targetId, int answerCode);

    public void Cancel(int askerId);

    /// <summary>
    ///     Same-shard lookup first (<see cref="ZoneRegistry.TryGetPlayer" />), falling back to the cross-shard
    ///     character-location directory on a miss and re-applying the same same-tribe gate against the
    ///     directory row's own denormalized <c>Tribe</c> column -- no second query needed.
    /// </summary>
    public ValueTask<FriendLocateResult> LocateAsync(PlayerRuntimeState asker, int index,
        CancellationToken cancellationToken);

    public ValueTask<FriendAddResult>
        AddAsync(PlayerRuntimeState state, int index, CancellationToken cancellationToken);

    public ValueTask<FriendRemoveResultKind> RemoveAsync(PlayerRuntimeState state, int index,
        CancellationToken cancellationToken);
}

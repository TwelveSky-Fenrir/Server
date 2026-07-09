using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Discriminator for how a CZ_PARTY_ASK_SEND attempt resolved.</summary>
public enum PartyInviteResultKind
{
    InviterMustDisconnect,
    TargetNotFound,
    InviterBusy,
    TargetBusy,
    TargetAlreadyPartied,
    Sent,

    /// <summary>
    ///     WS1.4: the target was not found on this shard's own <c>ZoneRegistry</c> but WAS resolved on a
    ///     different live shard via <c>ICharacterShardLocationRepository</c> -- the invite has been handed to
    ///     <c>ISocialCrossShardRelayQueue</c> for asynchronous cross-shard delivery instead of the immediate
    ///     local <see cref="Sent" /> notification. The caller (<see cref="PartyInviteHandler" />) sends
    ///     nothing further; any reply (accept/decline/target-unreachable) arrives later via
    ///     <c>PartyCrossShardRelayHandler.HandleAnswerAsync</c>.
    /// </summary>
    SentCrossShard
}

public readonly record struct PartyInviteResult(
    PartyInviteResultKind Kind,
    int TargetCharacterId = 0,
    string? TargetName = null,
    string? InviterName = null);

public interface IPartyInviteService
{
    /// <summary>
    ///     Same-shard lookup first (within <paramref name="zone" />), falling back to the cross-shard
    ///     character-location directory on a miss -- see <see cref="PartyInviteResultKind.SentCrossShard" />.
    /// </summary>
    public ValueTask<PartyInviteResult> InviteAsync(Zone zone, PlayerRuntimeState inviter, string targetAvatarName,
        CancellationToken cancellationToken);
}

namespace Fenrir.Application.Game.Domain.Social;

/// <summary>
///     This shard's own outstanding ASK sent to a character resolved on a different live shard (via
///     <c>ICharacterShardLocationRepository.FindByNameAsync</c>) -- held on the ASKER's own shard only, from
///     the moment the Ask is handed to <c>ISocialCrossShardRelayQueue</c> until the matching Answer is
///     delivered back (<see cref="CrossShardNegotiationTracker.TryConsumeOutbound" />) or the asker cancels.
/// </summary>
public readonly record struct CrossShardOutboundAsk(
    byte TargetShardId,
    int TargetCharacterId,
    string TargetAvatarName);

/// <summary>
///     This shard's own held record of a cross-shard ASK delivered in from a remote asker -- held on the
///     TARGET's own shard only, from the moment <c>SocialCrossShardRelayHost</c> delivers the Ask (and this
///     shard's own target-side pre-checks pass, so a real prompt is shown) until the local target answers
///     (<see cref="CrossShardNegotiationTracker.TryConsumeInbound" />). <see cref="RelayId" /> is threaded
///     back onto the eventual Answer row's own <c>AskRelayId</c> so the asker's shard can (for diagnostics/
///     audit only -- matching back to the held outbound entry itself is done by character id, not RelayId,
///     since an asker can only ever have one outstanding ask at a time, same invariant every one of the six
///     negotiation registries' own same-shard <c>IsNegotiating</c>/<c>IsBusy</c> gate already enforces).
/// </summary>
public readonly record struct CrossShardInboundAsk(
    long RelayId,
    byte SourceShardId,
    int SourceCharacterId,
    string SourceAvatarName);

/// <summary>
///     Shared cross-shard half of every one of the six character-to-character negotiation registries
///     (<c>PartyRegistry</c>, <c>Friends.FriendRegistry</c>, <c>Mentor.MentorRegistry</c>,
///     <c>Duel.DuelRegistry</c>, <c>Trade.TradeRegistry</c>, <c>Guilds.GuildInviteRegistry</c>) -- WS1.4
///     design. Composed as a field on each registry, not inherited: each registry keeps its own same-shard
///     dictionaries/locking exactly as they were before this design, and only folds
///     <see cref="IsPending" /> into its own <c>IsNegotiating</c>/<c>IsBusy</c> check so a character with
///     either a same-shard OR a cross-shard pending ask is "busy" identically either way. Each registry also
///     exposes its own thinly-named wrapper methods over this type (matching that registry's own existing
///     <c>TryAsk</c>/<c>TryCancel</c>/<c>TryAnswer</c> naming) rather than exposing this type's instance
///     directly, to keep every registry's public surface self-describing.
///     <para>
///         Independent lock from whatever registry composes it: an outbound/inbound registration never needs
///         to also touch that registry's own same-shard dictionaries atomically (the two are only ever
///         combined by a plain OR in <c>IsNegotiating</c>/<c>IsBusy</c>, never mutated together), so there is
///         no lock-ordering hazard between this type's own <see cref="Lock" /> and a composing registry's.
///     </para>
/// </summary>
public sealed class CrossShardNegotiationTracker
{
    private readonly Dictionary<int, CrossShardInboundAsk> _inboundByTarget = new();
    private readonly Lock _lock = new();
    private readonly Dictionary<int, CrossShardOutboundAsk> _outboundByAsker = new();

    /// <summary>True if <paramref name="characterId" /> is either side of a still-open cross-shard ask.</summary>
    public bool IsPending(int characterId)
    {
        lock (_lock)
        {
            return _outboundByAsker.ContainsKey(characterId) || _inboundByTarget.ContainsKey(characterId);
        }
    }

    /// <summary>
    ///     Registers the local asker's own outbound cross-shard ask. False (and registers nothing) if
    ///     <paramref name="askerId" /> already has one pending -- the caller is expected to have already
    ///     checked <see cref="IsPending" /> (composed into the owning registry's own busy gate) before
    ///     reaching this point; this is a defensive re-check under the same lock, not the primary gate.
    /// </summary>
    public bool TryRegisterOutbound(int askerId, CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            return _outboundByAsker.TryAdd(askerId, ask);
        }
    }

    /// <summary>
    ///     Consumes the local asker's own outbound cross-shard ask -- called once, either when the matching
    ///     Answer is delivered back (accept or decline) or when the asker cancels. False if nothing was
    ///     pending for <paramref name="askerId" /> (already answered, cancelled, or never cross-shard).
    /// </summary>
    public bool TryConsumeOutbound(int askerId, out CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            return _outboundByAsker.Remove(askerId, out ask);
        }
    }

    /// <summary>
    ///     Registers the local target's own held record of a delivered-in cross-shard ask, once this shard's
    ///     own target-side pre-checks have passed and a real prompt has been shown. False (and registers
    ///     nothing) if <paramref name="targetId" /> already has one pending.
    /// </summary>
    public bool TryRegisterInbound(int targetId, CrossShardInboundAsk ask)
    {
        lock (_lock)
        {
            return _inboundByTarget.TryAdd(targetId, ask);
        }
    }

    /// <summary>
    ///     Consumes the local target's own held record of a delivered-in cross-shard ask -- called when the
    ///     local target answers. False if nothing was pending for <paramref name="targetId" /> (already
    ///     answered, or this target has no cross-shard ask at all -- the caller falls back to its own
    ///     same-shard <c>TryAnswer</c> in that case).
    /// </summary>
    public bool TryConsumeInbound(int targetId, out CrossShardInboundAsk ask)
    {
        lock (_lock)
        {
            return _inboundByTarget.Remove(targetId, out ask);
        }
    }
}

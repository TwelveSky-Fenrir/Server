using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     The <c>*.Services</c> party-reconciliation authority <c>PartyResyncRelayHost</c>'s own remarks call out
///     as "a follow-up not yet wired" -- registered as <see cref="IPartyResyncRelayHandler" />, every relayed
///     party-resync row lands here. Mirrors the legacy hub's case-108 reconciliation
///     (Server/ts25center/S04_MyWork02.cpp:1443-1494): "does THIS shard currently host a live party whose
///     leader is named <see cref="PartyResyncRelayDto.PartyName" />" -- composed from two pieces that already
///     exist but had never been wired together for this purpose: <see cref="ZoneRegistry.TryGetPlayerByName" />
///     (the same process-wide by-name lookup <c>WhisperService</c>/<c>FindGuildMemberService</c> already use
///     for their own name-addressed features) resolves the name to a live character, and
///     <see cref="PartyRegistry.IsLeader" />/<see cref="PartyRegistry.GetMembers" /> confirm that character is
///     still a live party leader here and fetch the roster -- the piece <see cref="PartyRegistry" /> itself
///     cannot provide alone, being keyed by CharacterId, not by name (see that type's own remarks).
/// </summary>
/// <remarks>
///     <para>
///         <b>Sort 108 is shared by <see cref="PartyResyncRelaySort.Request" /> and
///         <see cref="PartyResyncRelaySort.PartyInfo" /></b> (both carry the same numeric value, see
///         <see cref="PartyResyncRelaySort" />'s own remarks) -- a delivered row cannot be told apart as
///         "a fresh request" vs. "a reply to my own earlier request" by its <see cref="PartyResyncRelayDto.Sort" />
///         byte alone. This is not actually ambiguous in practice: character names are unique cluster-wide, so
///         at most one live shard can ever answer "yes" to "do I host a live party led by this exact name" for
///         a given <see cref="PartyResyncRelayDto.PartyName" /> at any instant. Running the same hosting-check
///         unconditionally on every delivered sort-108 row is therefore safe and self-terminating: the true
///         host answers once (fan-out delivers its own reply to every other shard, but none of them will also
///         host a leader with that exact name, so they no-op on it), and the row can never come back to the
///         shard that answered it (<c>usp_PartyResyncRelay_Poll</c> excludes the caller's own
///         <see cref="PartyResyncRelayDto.SourceShardId" />).
///     </para>
///     <para>
///         <b>Out of scope, deliberately:</b> the reverse leg -- applying a delivered
///         <see cref="PartyResyncRelaySort.PartyInfo" />/<see cref="PartyResyncRelaySort.PartyBreak" /> RESULT
///         to the reconnecting member's own client UI on the shard where they are actually connected -- needs
///         an outbound wire push this reconciliation leg has no confirmed packet/call site for yet, and the
///         producer side of this whole flow (a resync-request trigger firing on a reconnecting member's first
///         zone entry, gated on a persisted party-name anchor -- see <see cref="IPartyResyncRelayQueue" />'s own
///         remarks) does not exist yet either. Both are separate, still-open follow-ups; this handler only
///         closes the "can a hosting shard now actually answer" half that was blocking on the missing
///         name-lookup wiring.
///     </para>
/// </remarks>
public sealed class PartyResyncRelayHandler(
    ZoneRegistry zones,
    PartyRegistry parties,
    Lazy<IPartyResyncRelayQueue> relay,
    IOptions<GameServerOptions> options,
    ILogger<PartyResyncRelayHandler> logger) : IPartyResyncRelayHandler
{
    public ValueTask HandleAsync(PartyResyncRelayDto row, CancellationToken ct)
    {
        if (row.Sort != (byte)PartyResyncRelaySort.Request)
        {
            // Sort 109 (PartyBreak) -- see this type's own remarks for why applying it to a locally-present
            // subject's UI is a separate, not-yet-wired follow-up rather than something to invent here.
            logger.LogDebug(
                "Relayed party-resync row {RelayId} (sort {Sort}, party {PartyName}) is a result leg with no " +
                "local UI-apply wired yet; no-op",
                row.RelayId, row.Sort, row.PartyName);
            return ValueTask.CompletedTask;
        }

        if (!zones.TryGetPlayerByName(row.PartyName, out var leaderCandidate) ||
            !parties.IsLeader(leaderCandidate.CharacterId))
        {
            // Not hosted on this shard -- the overwhelmingly common outcome in a fan-out broadcast to every
            // OTHER live shard (at most one shard can ever answer "yes"). Debug, not a fault.
            logger.LogDebug(
                "Relayed party-resync request {RelayId} for party {PartyName} (subject {SourceCharacterId}) " +
                "is not hosted on shard {ShardId}",
                row.RelayId, row.PartyName, row.SourceCharacterId, options.Value.ShardId);
            return ValueTask.CompletedTask;
        }

        var roster = parties.GetMembers(leaderCandidate.CharacterId);
        if (roster.Count == 0)
        {
            // IsLeader and GetMembers each take PartyRegistry's own lock independently -- the party could have
            // been disbanded (last member left) in between the two calls. Treat exactly like "not hosted here".
            logger.LogDebug(
                "Relayed party-resync request {RelayId} for party {PartyName} raced a disband on shard " +
                "{ShardId} between the leader check and the roster fetch; treated as not hosted",
                row.RelayId, row.PartyName, options.Value.ShardId);
            return ValueTask.CompletedTask;
        }

        // Re-published with THIS shard's own ShardId (the responder) so the fan-out poll excludes it from our
        // own next cycle but still delivers it to every other live shard -- including the original requester,
        // wherever it is. SourceCharacterId/AvatarName are preserved from the incoming row (the reconnecting
        // subject's own identity, not this shard's leader) so the requester can match the reply to its own
        // outstanding claim; PartyName is reaffirmed unchanged (the party's canonical name never changes
        // mid-life short of a full disband, per PartyIdentityResolver's own remarks).
        relay.Value.Enqueue(new PartyResyncRelayEntry(
            (byte)PartyResyncRelaySort.PartyInfo,
            options.Value.ShardId,
            row.SourceCharacterId,
            row.PartyName,
            row.AvatarName));

        logger.LogDebug(
            "Relayed party-resync request {RelayId} for party {PartyName} confirmed on shard {ShardId} " +
            "({MemberCount} members); result republished for subject {SourceCharacterId}",
            row.RelayId, row.PartyName, options.Value.ShardId, roster.Count, row.SourceCharacterId);

        return ValueTask.CompletedTask;
    }
}

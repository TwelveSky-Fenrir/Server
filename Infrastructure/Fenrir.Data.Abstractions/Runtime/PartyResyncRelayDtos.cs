using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Legacy relay sort for a <c>runtime.PartyResyncRelay</c> row (Server/ts25center/S04_MyWork02.cpp:
///     1443-1494's case-108 reconciliation and its 108/109 broadcast results). Numeric values ARE the legacy
///     sorts, carried verbatim so the reconciling handler can switch on them: <see cref="Request" />/
///     <see cref="PartyInfo" /> share 108 (the resync request and its found-roster resync result), while
///     <see cref="PartyBreak" /> is 109 (the party is dissolved).
/// </summary>
public enum PartyResyncRelaySort : byte
{
    /// <summary>108 -- "does this claimed party still exist?" published on a reconnecting member's first entry.</summary>
    Request = 108,

    /// <summary>108 -- found: the party's full roster (up to five names) resyncs the member (shares the request sort).</summary>
    PartyInfo = 108,

    /// <summary>109 -- not found / member matched some other party: the party is broken and its UI cleared.</summary>
    PartyBreak = 109
}

/// <summary>
///     Everything <c>PartyResyncRelayHost</c>'s outbound drain loop needs to call
///     <c>usp_PartyResyncRelay_Publish</c> for one cross-shard party-resync row -- see
///     <see cref="IPartyResyncRelayQueue" /> for where this is constructed and enqueued. Fan-out (every OTHER
///     live shard's own poll picks it up), mirroring the legacy hub's broadcast of its reconciliation result
///     to every connected zone (Server/ts25center/S04_MyWork02.cpp:1443-1494), not a point reply.
///     <see cref="Sort" /> carries the legacy relay sort (<see cref="PartyResyncRelaySort" />);
///     <see cref="PartyName" /> is the party the <see cref="SourceCharacterId" />/<see cref="AvatarName" />
///     character claims (its persisted party-name anchor), bounded by the 13-char name limit.
/// </summary>
public sealed record PartyResyncRelayEntry(
    byte Sort,
    byte SourceShardId,
    int SourceCharacterId,
    string PartyName,
    string AvatarName);

// Ordinal-mapped: ctor order must match usp_PartyResyncRelay_Poll's SELECT order.
[GenerateDto]
public sealed partial record PartyResyncRelayDto(
    long RelayId,
    byte Sort,
    byte SourceShardId,
    int SourceCharacterId,
    string PartyName,
    string AvatarName);

using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Everything <c>RvrSiegeEventRelayHost</c>'s outbound drain loop needs to call
///     <c>usp_RvrSiegeEventRelay_Publish</c> -- constructed by <c>ZoneCenterBroadcastIngestor.Ingest</c> (Zone049
///     siege-zone-slot events, sub-codes 1-9) and by <c>ZoneEventBroadcaster</c>'s tSort 38/39/40/42/45/46/47
///     tribe-symbol/alliance methods, immediately after each one's own unchanged, synchronous same-shard
///     mutation/broadcast (see <see cref="IRvrSiegeEventRelayQueue" /> for that boundary). <see cref="Sort" />/
///     <see cref="Data" /> are the exact <c>ZoneEventInfoResponse</c> (op94) fields the origin shard already
///     broadcast to its own locally-hosted players -- this table carries the raw, opaque wire pair rather than a
///     per-field breakdown (unlike <c>runtime.GuildTribeBroadcastRelay</c>'s typed columns) because every sort
///     this relay carries already funnels through that one generic 130-byte payload shape on the wire itself, so
///     decomposing it here would just be re-deriving what <see cref="Data" /> already holds.
/// </summary>
public sealed record RvrSiegeEventRelayEntry(
    byte SourceShardId,
    int Sort,
    byte[] Data);

// Ordinal-mapped: ctor order must match usp_RvrSiegeEventRelay_Poll's SELECT order.
[GenerateDto]
public sealed partial record RvrSiegeEventRelayDto(
    long RelayId,
    int Sort,
    byte[] Data);

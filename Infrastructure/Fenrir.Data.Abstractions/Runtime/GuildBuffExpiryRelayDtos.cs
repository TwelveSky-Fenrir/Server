using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Runtime;

/// <summary>
///     Everything <c>GuildBuffExpiryRelayHost</c>'s outbound drain loop needs to call
///     <c>usp_GuildBuffExpiryRelay_Publish</c> -- see <see cref="IGuildBuffExpiryRelayQueue" /> for where this
///     is constructed (<c>Hosting.Guilds.GuildBuffDecayHost</c>'s own edge-triggered reserve-exhaustion
///     detection, immediately after that same pass's own same-shard delivery) and enqueued.
/// </summary>
public sealed record GuildBuffExpiryRelayEntry(byte SourceShardId, int GuildId, int NewBuffTime);

// Ordinal-mapped: ctor order must match usp_GuildBuffExpiryRelay_Poll's SELECT order.
[GenerateDto]
public sealed partial record GuildBuffExpiryRelayDto(long RelayId, int GuildId, int NewBuffTime);

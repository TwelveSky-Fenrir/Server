using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Game;

// Mirrors game.tvp_EventLogEntry column order. OccurredAtUtc is the producer-captured event time (NOT the
// flush time) -- Fenrir.Data.WriteBehind.EventLogQueue's background drain loop stamps this at Enqueue time,
// not at flush time, so a buffered high-frequency event's CreatedAtUtc in game.EventLog reflects when it
// actually happened, not when the batch got flushed. See tvp_EventLogEntry.sql for the full reasoning.
[GenerateTvp(Schema = "game", TvpName = "tvp_EventLogEntry")]
public sealed partial record EventLogEntryTvp(
    short EventCode,
    byte Category,
    int? ActorAccountId,
    int? ActorCharacterId,
    int? TargetAccountId,
    int? TargetCharacterId,
    short? ShardId,
    long? DeltaMoney,
    long? DeltaBigMoney,
    int? ItemId,
    int? Quantity,
    byte? Outcome,
    string? Payload,
    DateTime OccurredAtUtc);

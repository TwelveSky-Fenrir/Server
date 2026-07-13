using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Game;

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

namespace Fenrir.Data.Abstractions.Game;

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

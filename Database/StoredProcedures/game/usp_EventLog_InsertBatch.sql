CREATE PROCEDURE game.usp_EventLog_InsertBatch @Entries game.tvp_EventLogEntry READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    INSERT INTO game.EventLog
    (EventCode, Category, ActorAccountId, ActorCharacterId, TargetAccountId, TargetCharacterId,
     ShardId, DeltaMoney, DeltaBigMoney, ItemId, Quantity, Outcome, Payload, CreatedAtUtc)
    SELECT EventCode,
           Category,
           ActorAccountId,
           ActorCharacterId,
           TargetAccountId,
           TargetCharacterId,
           ShardId,
           DeltaMoney,
           DeltaBigMoney,
           ItemId,
           Quantity,
           Outcome,
           Payload,
           OccurredAtUtc
    FROM @Entries;
END;

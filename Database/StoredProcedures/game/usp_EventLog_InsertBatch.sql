-- Multi-row write-behind flush for the high-frequency EventLog path (enchant/refine/socket rolls) --
-- see game.tvp_EventLogEntry for why OccurredAtUtc is threaded through explicitly instead of relying on
-- game.EventLog.CreatedAtUtc's DEFAULT. Never called with zero rows -- Fenrir.Data.EventLogRepository
-- guards on Count == 0 before building the call, same empty-TVP-omission rule as every other batched write
-- in this codebase.
CREATE PROCEDURE game.usp_EventLog_InsertBatch
    @Entries game.tvp_EventLogEntry READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Single INSERT ... SELECT is already atomic -- no explicit BEGIN TRANSACTION needed.
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

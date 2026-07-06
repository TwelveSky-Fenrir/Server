-- TVP for usp_EventLog_InsertBatch: the high-frequency write-behind path (enchant/refine/socket rolls),
-- fed by Fenrir.Data.WriteBehind.EventLogQueue's bounded-channel drain loop. OccurredAtUtc is deliberately
-- a separate, explicit column rather than relying on game.EventLog.CreatedAtUtc's DEFAULT SYSUTCDATETIME() --
-- these rows are buffered in memory for up to a few seconds before the batch flushes, so the DEFAULT would
-- stamp every row in a batch with the FLUSH time instead of the moment the event actually happened.
-- usp_EventLog_InsertBatch inserts this value explicitly into CreatedAtUtc, overriding the column default.
CREATE TYPE game.tvp_EventLogEntry AS TABLE
    (
    EventCode         SMALLINT      NOT NULL,
    Category          TINYINT       NOT NULL,
    ActorAccountId    INT           NULL,
    ActorCharacterId  INT           NULL,
    TargetAccountId   INT           NULL,
    TargetCharacterId INT           NULL,
    ShardId           SMALLINT      NULL,
    DeltaMoney        BIGINT        NULL,
    DeltaBigMoney     BIGINT        NULL,
    ItemId            INT           NULL,
    Quantity          INT           NULL,
    Outcome           TINYINT       NULL,
    Payload           NVARCHAR(MAX) NULL,
    OccurredAtUtc     DATETIME2(3)  NOT NULL
    );

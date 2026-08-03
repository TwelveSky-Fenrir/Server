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

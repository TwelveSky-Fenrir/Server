CREATE PROCEDURE game.usp_EventLog_Insert @EventCode SMALLINT,
                                          @Category TINYINT,
                                          @ActorAccountId INT = NULL,
                                          @ActorCharacterId INT = NULL,
                                          @TargetAccountId INT = NULL,
                                          @TargetCharacterId INT = NULL,
                                          @ShardId SMALLINT = NULL,
                                          @DeltaMoney BIGINT = NULL,
                                          @DeltaBigMoney BIGINT = NULL,
                                          @ItemId INT = NULL,
                                          @Quantity INT = NULL,
                                          @Outcome TINYINT = NULL,
                                          @Payload NVARCHAR(MAX) = NULL
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    INSERT INTO game.EventLog
    (EventCode, Category, ActorAccountId, ActorCharacterId, TargetAccountId, TargetCharacterId,
     ShardId, DeltaMoney, DeltaBigMoney, ItemId, Quantity, Outcome, Payload)
    VALUES (@EventCode, @Category, @ActorAccountId, @ActorCharacterId, @TargetAccountId, @TargetCharacterId,
            @ShardId, @DeltaMoney, @DeltaBigMoney, @ItemId, @Quantity, @Outcome, @Payload);
END;

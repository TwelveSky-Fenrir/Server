CREATE OR ALTER PROCEDURE game.usp_EconomyOperation_BeginOrRead @ActorAccountId INT,
                                                                 @ActorCharacterId INT = NULL,
                                                                 @OperationKind TINYINT,
                                                                 @Cause TINYINT,
                                                                 @IdempotencyKeyHash BINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ActorCharacterId IS NOT NULL AND NOT EXISTS (SELECT 1
                                                      FROM game.Characters
                                                      WHERE CharacterId = @ActorCharacterId
                                                        AND AccountId = @ActorAccountId)
        THROW 50366, N'The economy operation actor character does not belong to the actor account.', 1;

    DECLARE @OperationId UNIQUEIDENTIFIER;
    DECLARE @CorrelationId UNIQUEIDENTIFIER;
    DECLARE @Status TINYINT;
    DECLARE @CreatedAtUtc DATETIME2(3);
    DECLARE @CompletedAtUtc DATETIME2(3);
    DECLARE @Begun BIT = 0;

    BEGIN TRANSACTION;

    SELECT @OperationId = OperationId,
           @CorrelationId = CorrelationId,
           @Status = Status,
           @CreatedAtUtc = CreatedAtUtc,
           @CompletedAtUtc = CompletedAtUtc
    FROM game.EconomyOperationLedger WITH (UPDLOCK, HOLDLOCK)
    WHERE ActorAccountId = @ActorAccountId
      AND IdempotencyKeyHash = @IdempotencyKeyHash;

    IF @OperationId IS NULL
        BEGIN
            INSERT INTO game.EconomyOperationLedger
                (ActorAccountId, ActorCharacterId, OperationKind, Cause, IdempotencyKeyHash)
            VALUES
                (@ActorAccountId, @ActorCharacterId, @OperationKind, @Cause, @IdempotencyKeyHash);

            SELECT @OperationId = OperationId,
                   @CorrelationId = CorrelationId,
                   @Status = Status,
                   @CreatedAtUtc = CreatedAtUtc,
                   @CompletedAtUtc = CompletedAtUtc
            FROM game.EconomyOperationLedger
            WHERE ActorAccountId = @ActorAccountId
              AND IdempotencyKeyHash = @IdempotencyKeyHash;

            SET @Begun = 1;
        END;

    COMMIT TRANSACTION;

    SELECT @OperationId AS OperationId,
           @CorrelationId AS CorrelationId,
           @Status AS Status,
           @CreatedAtUtc AS CreatedAtUtc,
           @CompletedAtUtc AS CompletedAtUtc,
           @Begun AS Begun;
END;

CREATE OR ALTER PROCEDURE game.usp_EconomyOperation_Complete @OperationId UNIQUEIDENTIFIER,
                                                              @ActorAccountId INT,
                                                              @FinalStatus TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @FinalStatus NOT BETWEEN 1 AND 3
        THROW 50367, N'An economy operation can only complete with a terminal status.', 1;

    DECLARE @CorrelationId UNIQUEIDENTIFIER;
    DECLARE @Status TINYINT;
    DECLARE @CreatedAtUtc DATETIME2(3);
    DECLARE @CompletedAtUtc DATETIME2(3);
    DECLARE @CompletedNow BIT = 0;

    BEGIN TRANSACTION;

    UPDATE game.EconomyOperationLedger
    SET Status = @FinalStatus,
        CompletedAtUtc = SYSUTCDATETIME()
    WHERE OperationId = @OperationId
      AND ActorAccountId = @ActorAccountId
      AND Status = 0;

    IF @@ROWCOUNT = 1
        SET @CompletedNow = 1;

    SELECT @CorrelationId = CorrelationId,
           @Status = Status,
           @CreatedAtUtc = CreatedAtUtc,
           @CompletedAtUtc = CompletedAtUtc
    FROM game.EconomyOperationLedger WITH (UPDLOCK, HOLDLOCK)
    WHERE OperationId = @OperationId
      AND ActorAccountId = @ActorAccountId;

    IF @CorrelationId IS NULL
        THROW 50368, N'The economy operation does not exist for the specified actor account.', 1;

    COMMIT TRANSACTION;

    SELECT @OperationId AS OperationId,
           @CorrelationId AS CorrelationId,
           @Status AS Status,
           @CreatedAtUtc AS CreatedAtUtc,
           @CompletedAtUtc AS CompletedAtUtc,
           @CompletedNow AS CompletedNow;
END;

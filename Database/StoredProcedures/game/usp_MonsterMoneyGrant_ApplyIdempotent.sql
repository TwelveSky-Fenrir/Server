CREATE PROCEDURE game.usp_MonsterMoneyGrant_ApplyIdempotent @CorrelationId UNIQUEIDENTIFIER,
                                                            @CharacterId INT,
                                                            @Amount BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @CorrelationId = '00000000-0000-0000-0000-000000000000'
        THROW 50369, N'A monster-money grant requires a non-empty correlation identifier.', 1;

    IF @CharacterId <= 0 OR @Amount NOT BETWEEN 1 AND 2000000000
        THROW 50369, N'A monster-money grant requires a positive character identifier and an amount within the money cap.', 1;

    DECLARE @StoredCharacterId INT;
    DECLARE @StoredAmount BIGINT;
    DECLARE @AccountId INT;
    DECLARE @AuditEventLogId BIGINT;
    DECLARE @MoneyCap BIGINT = 2000000000;

    BEGIN TRANSACTION;

    SELECT @StoredCharacterId = CharacterId,
           @StoredAmount = Amount
    FROM game.MonsterMoneyGrantLedger WITH (UPDLOCK, HOLDLOCK)
    WHERE CorrelationId = @CorrelationId;

    IF @StoredCharacterId IS NOT NULL
        BEGIN
            IF @StoredCharacterId <> @CharacterId OR @StoredAmount <> @Amount
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 50370, N'The monster-money grant correlation identifier was reused with different input.', 1;
                END;

            COMMIT TRANSACTION;

            SELECT CAST(0 AS BIT) AS WasApplied,
                   CAST(1 AS BIT) AS WasAlreadyApplied;
            RETURN;
        END;

    DECLARE @Credited TABLE
                           (
                               AccountId INT NOT NULL
                           );

    UPDATE c
    SET Money        = c.Money + @Amount,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT inserted.AccountId INTO @Credited (AccountId)
    FROM game.Characters AS c
    WHERE c.CharacterId = @CharacterId
      AND c.Money BETWEEN 0 AND @MoneyCap - @Amount;

    IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;

            IF EXISTS (SELECT 1
                       FROM game.Characters
                       WHERE CharacterId = @CharacterId
                         AND Money > @MoneyCap - @Amount)
                THROW 50372, N'The monster-money grant would exceed the configured money cap (2,000,000,000).', 1;

            THROW 50371, N'The monster-money grant requires an existing character.', 1;
        END;

    SELECT @AccountId = AccountId FROM @Credited;

    DECLARE @AuditEvent TABLE
                              (
                                  EventLogId BIGINT NOT NULL
                              );

    INSERT INTO game.EventLog
        (EventCode, Category, ActorAccountId, ActorCharacterId, DeltaMoney, Outcome, Payload)
    OUTPUT inserted.EventLogId INTO @AuditEvent (EventLogId)
    VALUES
        (5, 1, @AccountId, @CharacterId, @Amount, 1,
         CONCAT(N'CorrelationId=', CONVERT(NVARCHAR(36), @CorrelationId), N';Source=MonsterLoot'));

    SELECT @AuditEventLogId = EventLogId FROM @AuditEvent;

    INSERT INTO game.MonsterMoneyGrantLedger
        (CorrelationId, CharacterId, AccountId, Amount, AuditEventLogId)
    VALUES
        (@CorrelationId, @CharacterId, @AccountId, @Amount, @AuditEventLogId);

    COMMIT TRANSACTION;

    SELECT CAST(1 AS BIT) AS WasApplied,
           CAST(0 AS BIT) AS WasAlreadyApplied;
END;

CREATE PROCEDURE admin.usp_Ban_Create @AccountId INT NULL,
                                      @CharacterId INT NULL,
                                      @Reason TINYINT,
                                      @ExpiresAtUtc DATETIME2(3) = NULL,
                                      @ActorAccountId INT,
                                      @ActorCharacterId INT,
                                      @CorrelationId UNIQUEIDENTIFIER,
                                      @AuditPayload NVARCHAR(512)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AccountId IS NULL AND @CharacterId IS NULL
        THROW 50301, N'A ban must target at least one of @AccountId or @CharacterId.', 1;

    IF @ActorAccountId IS NULL OR @ActorAccountId <= 0 OR @ActorCharacterId IS NULL OR @ActorCharacterId <= 0
        THROW 50375, N'A ban requires authenticated actor provenance.', 1;

    IF @CorrelationId IS NULL OR @CorrelationId = '00000000-0000-0000-0000-000000000000'
        THROW 50376, N'A ban requires a non-empty correlation identifier.', 1;

    IF @AuditPayload IS NULL OR LEN(LTRIM(RTRIM(@AuditPayload))) = 0
        THROW 50377, N'A ban requires a non-empty audit payload.', 1;

    IF @Reason IS NULL OR @Reason < 1 OR @Reason > 32
        THROW 50378, N'A ban reason is outside the supported range.', 1;

    DECLARE @BanId INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1
                       FROM auth.Accounts AS actorAccount WITH (UPDLOCK, HOLDLOCK)
                                INNER JOIN game.Characters AS actorCharacter WITH (UPDLOCK, HOLDLOCK)
                                           ON actorCharacter.AccountId = actorAccount.AccountId
                       WHERE actorAccount.AccountId = @ActorAccountId
                         AND actorCharacter.CharacterId = @ActorCharacterId
                         AND actorAccount.AccountGrade >= 1)
            THROW 50379, N'The ban actor provenance is not a current Basic-tier GM account and character.', 1;

        IF @CharacterId IS NOT NULL AND @AccountId IS NOT NULL AND NOT EXISTS (SELECT 1
                                                                                FROM game.Characters WITH (UPDLOCK, HOLDLOCK)
                                                                                WHERE CharacterId = @CharacterId
                                                                                  AND AccountId = @AccountId)
            THROW 50380, N'The ban target character does not belong to the target account.', 1;

        SELECT @BanId = BanId
        FROM admin.Bans WITH (UPDLOCK, HOLDLOCK)
        WHERE CorrelationId = @CorrelationId;

        IF @BanId IS NOT NULL
        BEGIN
            IF NOT EXISTS (SELECT 1
                           FROM admin.Bans
                           WHERE BanId = @BanId
                             AND ((AccountId = @AccountId) OR (AccountId IS NULL AND @AccountId IS NULL))
                             AND ((CharacterId = @CharacterId) OR (CharacterId IS NULL AND @CharacterId IS NULL))
                             AND Reason = @Reason
                             AND ((ExpiresAtUtc = @ExpiresAtUtc) OR (ExpiresAtUtc IS NULL AND @ExpiresAtUtc IS NULL))
                             AND ActorAccountId = @ActorAccountId
                             AND ActorCharacterId = @ActorCharacterId)
                THROW 50381, N'The ban correlation identifier was reused with a different command.', 1;

            IF NOT EXISTS (SELECT 1
                           FROM admin.BanAudit
                           WHERE BanId = @BanId
                             AND CorrelationId = @CorrelationId
                             AND ActorAccountId = @ActorAccountId
                             AND ActorCharacterId = @ActorCharacterId
                             AND AuditPayload = @AuditPayload)
                THROW 50382, N'The ban audit does not match the existing ban correlation.', 1;

            COMMIT TRANSACTION;
            SELECT @BanId;
            RETURN;
        END;

        INSERT INTO admin.Bans (AccountId, CharacterId, Reason, ExpiresAtUtc, CorrelationId, ActorAccountId,
                                ActorCharacterId)
        VALUES (@AccountId, @CharacterId, @Reason, @ExpiresAtUtc, @CorrelationId, @ActorAccountId,
                @ActorCharacterId);

        SET @BanId = CONVERT(INT, SCOPE_IDENTITY());

        INSERT INTO admin.BanAudit (BanId, CorrelationId, ActorAccountId, ActorCharacterId, AuditPayload)
        VALUES (@BanId, @CorrelationId, @ActorAccountId, @ActorCharacterId, @AuditPayload);

        COMMIT TRANSACTION;
        SELECT @BanId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;

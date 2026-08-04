CREATE PROCEDURE game.usp_PvpKillCooldown_TryClaim @ClaimId UNIQUEIDENTIFIER,
                                                    @AttackerCharacterId INT,
                                                    @DefenderCharacterId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ClaimId = '00000000-0000-0000-0000-000000000000' OR
       @AttackerCharacterId <= 0 OR @DefenderCharacterId <= 0 OR
       @AttackerCharacterId = @DefenderCharacterId
        THROW 50373, N'A PvP kill cooldown claim requires a non-empty identifier and two distinct positive character identifiers.', 1;

    DECLARE @RecordedAttackerCharacterId INT;
    DECLARE @RecordedDefenderCharacterId INT;
    DECLARE @RecordedWasAccepted BIT;
    DECLARE @CooldownExpiresAtUtc DATETIME2(3);
    DECLARE @NowUtc DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @NewCooldownExpiresAtUtc DATETIME2(3) = DATEADD(MINUTE, 10, @NowUtc);

    BEGIN TRANSACTION;

    SELECT @RecordedAttackerCharacterId = AttackerCharacterId,
           @RecordedDefenderCharacterId = DefenderCharacterId,
           @RecordedWasAccepted = WasAccepted
    FROM game.PvpKillCooldownLedger WITH (UPDLOCK, HOLDLOCK)
    WHERE ClaimId = @ClaimId;

    IF @RecordedAttackerCharacterId IS NOT NULL
        BEGIN
            IF @RecordedAttackerCharacterId <> @AttackerCharacterId OR
               @RecordedDefenderCharacterId <> @DefenderCharacterId
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 50374, N'A PvP kill cooldown claim identifier was replayed with different characters.', 1;
                END;

            COMMIT TRANSACTION;

            SELECT @RecordedWasAccepted AS WasAccepted,
                   CAST(1 AS BIT) AS WasAlreadyAccepted;
            RETURN;
        END;

    SELECT @CooldownExpiresAtUtc = CooldownExpiresAtUtc
    FROM game.PvpKillCooldownState WITH (UPDLOCK, HOLDLOCK)
    WHERE AttackerCharacterId = @AttackerCharacterId
      AND DefenderCharacterId = @DefenderCharacterId;

    IF @CooldownExpiresAtUtc IS NOT NULL AND @CooldownExpiresAtUtc > @NowUtc
        BEGIN
            INSERT INTO game.PvpKillCooldownLedger
                (ClaimId, AttackerCharacterId, DefenderCharacterId, WasAccepted, CooldownExpiresAtUtc)
            VALUES
                (@ClaimId, @AttackerCharacterId, @DefenderCharacterId, 0, NULL);

            COMMIT TRANSACTION;

            SELECT CAST(0 AS BIT) AS WasAccepted,
                   CAST(0 AS BIT) AS WasAlreadyAccepted;
            RETURN;
        END;

    IF @CooldownExpiresAtUtc IS NULL
        INSERT INTO game.PvpKillCooldownState
            (AttackerCharacterId, DefenderCharacterId, LastClaimId, CooldownExpiresAtUtc)
        VALUES
            (@AttackerCharacterId, @DefenderCharacterId, @ClaimId, @NewCooldownExpiresAtUtc);
    ELSE
        UPDATE game.PvpKillCooldownState
        SET LastClaimId = @ClaimId,
            CooldownExpiresAtUtc = @NewCooldownExpiresAtUtc,
            UpdatedAtUtc = @NowUtc
        WHERE AttackerCharacterId = @AttackerCharacterId
          AND DefenderCharacterId = @DefenderCharacterId;

    INSERT INTO game.PvpKillCooldownLedger
        (ClaimId, AttackerCharacterId, DefenderCharacterId, WasAccepted, CooldownExpiresAtUtc)
    VALUES
        (@ClaimId, @AttackerCharacterId, @DefenderCharacterId, 1, @NewCooldownExpiresAtUtc);

    COMMIT TRANSACTION;

    SELECT CAST(1 AS BIT) AS WasAccepted,
           CAST(0 AS BIT) AS WasAlreadyAccepted;
END;

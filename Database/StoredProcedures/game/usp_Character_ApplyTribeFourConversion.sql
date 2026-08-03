CREATE PROCEDURE game.usp_Character_ApplyTribeFourConversion @CharacterId INT,
                                                             @NewTribe TINYINT,
                                                             @StepPermanent INT,
                                                             @ActiveQuestId INT,
                                                             @QSort INT,
                                                             @TargetPhase INT,
                                                             @KillCounter INT,
                                                             @ConsumeQuota BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @NewTribe > 3
        THROW 50335, N'usp_Character_ApplyTribeFourConversion: NewTribe is outside the legal 0-3 range.', 1;

    BEGIN TRANSACTION;

    IF @ConsumeQuota = 1
        BEGIN
            UPDATE admin.TribeFourQuota
            SET CurrentCount = CurrentCount + 1
            WHERE Id = 1
              AND CurrentCount < MaxCount;

            IF @@ROWCOUNT = 0
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 50355,
                        N'usp_Character_ApplyTribeFourConversion: shared daily fourth-tribe quota exhausted.', 1;
                END;
        END;

    UPDATE game.Characters
    SET Tribe        = @NewTribe,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50334, N'usp_Character_ApplyTribeFourConversion: unknown CharacterId.', 1;
        END;

    UPDATE game.CharacterQuests
    SET StepPermanent = @StepPermanent,
        ActiveQuestId = @ActiveQuestId,
        QSort         = @QSort,
        TargetPhase   = @TargetPhase,
        KillCounter   = @KillCounter
    WHERE CharacterId = @CharacterId;

    IF @@ROWCOUNT = 0
        BEGIN
            BEGIN TRY
                INSERT INTO game.CharacterQuests (CharacterId, StepPermanent, ActiveQuestId, QSort, TargetPhase,
                                                  KillCounter)
                VALUES (@CharacterId, @StepPermanent, @ActiveQuestId, @QSort, @TargetPhase, @KillCounter);
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2627, 2601)
                    THROW;

                ROLLBACK TRANSACTION;

                BEGIN TRANSACTION;

                IF @ConsumeQuota = 1
                    BEGIN
                        UPDATE admin.TribeFourQuota
                        SET CurrentCount = CurrentCount + 1
                        WHERE Id = 1
                          AND CurrentCount < MaxCount;

                        IF @@ROWCOUNT = 0
                            BEGIN
                                ROLLBACK TRANSACTION;
                                THROW 50355,
                                    N'usp_Character_ApplyTribeFourConversion: shared daily fourth-tribe quota exhausted (retry).', 1;
                            END;
                    END;

                UPDATE game.Characters
                SET Tribe        = @NewTribe,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE CharacterId = @CharacterId;

                UPDATE game.CharacterQuests
                SET StepPermanent = @StepPermanent,
                    ActiveQuestId = @ActiveQuestId,
                    QSort         = @QSort,
                    TargetPhase   = @TargetPhase,
                    KillCounter   = @KillCounter
                WHERE CharacterId = @CharacterId;

                COMMIT TRANSACTION;

                RETURN;
            END CATCH;
        END;

    COMMIT TRANSACTION;
END;

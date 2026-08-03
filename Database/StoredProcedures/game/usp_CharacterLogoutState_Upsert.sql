CREATE PROCEDURE game.usp_CharacterLogoutState_Upsert @CharacterId INT,
                                                      @LastZone INT,
                                                      @PosX INT,
                                                      @PosY INT,
                                                      @PosZ INT,
                                                      @Life INT,
                                                      @Mana INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    UPDATE game.CharacterLogoutState
    SET LastZone      = @LastZone,
        PosX          = @PosX,
        PosY          = @PosY,
        PosZ          = @PosZ,
        Life          = @Life,
        Mana          = @Mana,
        CapturedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId;

    IF @@ROWCOUNT = 0
        BEGIN
            BEGIN TRY
                INSERT INTO game.CharacterLogoutState (CharacterId, LastZone, PosX, PosY, PosZ, Life, Mana, CapturedAtUtc)
                VALUES (@CharacterId, @LastZone, @PosX, @PosY, @PosZ, @Life, @Mana, SYSUTCDATETIME());
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2627, 2601)
                    THROW;

                ROLLBACK TRANSACTION;

                UPDATE game.CharacterLogoutState
                SET LastZone      = @LastZone,
                    PosX          = @PosX,
                    PosY          = @PosY,
                    PosZ          = @PosZ,
                    Life          = @Life,
                    Mana          = @Mana,
                    CapturedAtUtc = SYSUTCDATETIME()
                WHERE CharacterId = @CharacterId;

                RETURN;
            END CATCH;
        END;

    COMMIT TRANSACTION;
END;

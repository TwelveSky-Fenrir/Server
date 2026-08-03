CREATE PROCEDURE auth.usp_AccountPin_Set @AccountId INT,
                                         @PinHash VARBINARY(32),
                                         @PinSalt VARBINARY(16)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE auth.AccountPins
    SET PinHash      = @PinHash,
        PinSalt      = @PinSalt,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE AccountId = @AccountId;

    IF @@ROWCOUNT = 0
        BEGIN
            BEGIN TRY
                INSERT INTO auth.AccountPins (AccountId, PinHash, PinSalt)
                VALUES (@AccountId, @PinHash, @PinSalt);
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2627, 2601)
                    THROW;

                UPDATE auth.AccountPins
                SET PinHash      = @PinHash,
                    PinSalt      = @PinSalt,
                    UpdatedAtUtc = SYSUTCDATETIME()
                WHERE AccountId = @AccountId;
            END CATCH;
        END;
END;

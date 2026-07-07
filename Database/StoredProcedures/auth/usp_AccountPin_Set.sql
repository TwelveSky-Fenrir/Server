-- Upsert: serves both first-PIN creation and PIN replacement. "PIN already exists" / "no PIN to
-- change" preconditions are enforced by the caller before this proc runs.
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

    IF
        @@ROWCOUNT = 0
        INSERT INTO auth.AccountPins (AccountId, PinHash, PinSalt)
        VALUES (@AccountId, @PinHash, @PinSalt);
END;

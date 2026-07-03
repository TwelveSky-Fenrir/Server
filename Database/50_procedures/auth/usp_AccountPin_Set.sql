-- Contract: @AccountId INT, @PinHash VARBINARY(32), @PinSalt VARBINARY(16) -> no result set.
-- Upsert: serves both W_CREATE_MOUSE_PASSWORD_SEND (first PIN) and W_CHANGE_MOUSE_PASSWORD_SEND (replacement) --
-- the legacy mDB.UpdateMousePassword is a single write path for both too. Idempotent for identical inputs
-- (replay-safe); "PIN already exists" / "no PIN to change" preconditions are enforced by the HANDLERS (they
-- Quit/abort the session, mirroring the legacy guards) -- by the time this proc runs, the write is legitimate.
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
